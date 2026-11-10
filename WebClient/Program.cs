using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using StackExchange.Redis;
using WebClient.Services;

namespace WebClient
{
    /// <summary>
    /// Точка входа веб-клиента (C-01).
    /// 
    /// Схема аутентификации — BFF (Backend-for-Frontend):
    ///   - JWT-токены хранятся НЕ в браузере, а в серверном Redis (см. TokenStore на следующем шаге);
    ///   - в браузер уходит только HttpOnly cookie с идентификатором сессии и claims для UI;
    ///   - так JavaScript не может украсть ни токен, ни cookie (защита от XSS).
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Настраивает DI-контейнер и конвейер обработки запросов.
        /// </summary>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ================================================================
            // 1. Razor Pages
            // ================================================================
            builder.Services.AddRazorPages();

            // ================================================================
            // 2. Cookie-аутентификация (BFF)
            // ================================================================
            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = ".AnonSurvey.WebClient.Auth";
                    options.Cookie.HttpOnly = true;                       // JS не прочитает cookie
                    options.Cookie.SameSite = SameSiteMode.Lax;           // защита от CSRF
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // dev: http, prod: https
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/Denied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });

            // ================================================================
            // 2.1. Подключение к Redis (для TokenStore)
            // ================================================================
            // Строка подключения хранится в User Secrets (ключ Redis:ConnectionString).
            // Если не задана — приложение не должно стартовать: без Redis сессии не работают.
            var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
                ?? throw new InvalidOperationException(
                    "Не задана строка подключения к Redis. Добавь Redis:ConnectionString в User Secrets проекта WebClient.");

            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false; // не падать, если Redis ещё не поднялся
                return ConnectionMultiplexer.Connect(options);
            });

            // HttpContextAccessor — нужен RefreshTokenHandler'у, чтобы читать claims текущего пользователя.
            builder.Services.AddHttpContextAccessor();

            // Handler регистрируется как Transient: IHttpClientFactory создаёт
            // новый экземпляр на каждый scope запроса, состояние не перетекает.
            builder.Services.AddTransient<RefreshTokenHandler>();


            // ================================================================
            // 3. IHttpClientFactory — клиент Gateway
            // ================================================================
            // BaseAddress читается из appsettings.json -> Gateway:BaseUrl.
            // Все обращения к API — только через Gateway (8080), не напрямую в сервисы.

            // Основной клиент: с авто-refresh при 401 (BFF-паттерн).
            builder.Services.AddHttpClient("GatewayClient", client =>
            {
                var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:8080/";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<RefreshTokenHandler>();

            // «Сырой» клиент: БЕЗ handler'а. Используется только самим handler'ом
            // для вызова /api/auth/refresh — иначе была бы бесконечная рекурсия при 401.
            builder.Services.AddHttpClient("GatewayClientRaw", client =>
            {
                var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:8080/";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // ================================================================
            // 3.1. Сервисы WebClient
            // ================================================================
            builder.Services.AddScoped<TokenStore>();
            builder.Services.AddScoped<GatewayApiClient>();

            // ================================================================
            // 4. Antiforgery (защита от CSRF)
            // ================================================================
            // Без этого POST-формы Razor Pages не пройдут валидацию.
            builder.Services.AddAntiforgery(options =>
            {
                options.Cookie.Name = ".AnonSurvey.WebClient.Antiforgery";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            var app = builder.Build();

            // ================================================================
            // 5. Конвейер обработки запросов
            // ================================================================
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
                app.UseHttpsRedirection(); // в dev без https-эндпоинта редирект ломал бы страницы
            }

            // Отдаёт все ответы с кодом 4xx/5xx через страницу /Error.
            // ВАЖНО: ставим ДО маршрутизации и статики — так middleware ловит
            // статусы из любого слоя ниже, включая 404 на несуществующих страницах.
            app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

            // UseStaticFiles отдаёт содержимое wwwroot (css, js, картинки).
            app.UseStaticFiles();

            app.UseRouting();

            // UseAuthentication строго ДО UseAuthorization:
            // сначала «кто ты», потом «можно ли тебе».
            app.UseAuthentication();

            // ================================================================
            // Middleware проверки сессии в Redis (D-16)
            // ================================================================
            // Если cookie аутентификации есть, а сессии в Redis нет —
            // (Redis перезапустили, TTL истёк, сессия отозвана) —
            // разлогиниваем и редиректим на логин. Без этого пользователь
            // видел бы 401 от API без объяснений.
            //
            // Пропускаем /Account/* — иначе пользователь с протухшей cookie
            // не смог бы дойти до /Account/ForgotPassword и /Account/ResetPassword.
            app.Use(async (ctx, next) =>
            {
                if (ctx.User.Identity?.IsAuthenticated == true
                    && !ctx.Request.Path.StartsWithSegments("/Account"))
                {
                    var sessionId = ctx.User.FindFirst("session_id")?.Value;
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        var store = ctx.RequestServices.GetRequiredService<TokenStore>();
                        if (!await store.ExistsAsync(sessionId))
                        {
                            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            ctx.Response.Redirect("/Account/Login");
                            return;
                        }
                    }
                }
                await next();
            });

            app.UseAuthorization();

            // UseAntiforgery — после аутентификации, до маппинга страниц.
            app.UseAntiforgery();

            app.MapRazorPages();

            app.Run();
        }
    }
}