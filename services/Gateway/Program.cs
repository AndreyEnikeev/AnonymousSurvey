using Gateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;

namespace Gateway
{
    /// <summary>
    /// Главный класс приложения Gateway (C-02).
    /// Отвечает за: валидацию JWT, rate limiting через Redis,
    /// маршрутизацию запросов к внутренним сервисам (YARP).
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ==========================================================
            // 1. JWT: валидируем токены, которые выпустил Auth (C-03)
            // ==========================================================
            // Параметры (Issuer, Audience, SigningKey) ДОЛЖНЫ совпадать с Auth.
            var jwtKey = builder.Configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey не настроен в User Secrets");

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "anon-survey",
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "anon-survey-client",
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ClockSkew = TimeSpan.Zero,
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });

            builder.Services.AddAuthorization();

            // ==========================================================
            // 2. Redis — для распределённого Rate Limiting
            // ==========================================================
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis не настроен в User Secrets");

            // Настраиваем параметры подключения к Redis.
            // abortConnect=false (AbortOnConnectFail) критически важен:
            // он позволяет сервису запуститься, даже если Redis временно недоступен,
            // и переподключиться позже (защита от порядка запуска контейнеров).
            var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;

            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions));

            // ==========================================================
            // 3. YARP: маршруты и кластеры из appsettings.json
            // ==========================================================
            builder.Services
                .AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            // ==========================================================
            // 4. HTTP-конвейер (порядок критичен)
            // ==========================================================
            app.UseHttpsRedirection();

            // 1) Аутентификация — «кто ты». Невалидный/просроченный токен ? 401.
            app.UseAuthentication();

            // 2) Rate Limiter — лимит на пользователя через Redis.
            //    После UseAuthentication, потому что читает userId из токена.
            app.UseMiddleware<RedisRateLimitMiddleware>();

            // 3) Авторизация — «можно ли тебе». Роли в Gateway НЕ проверяются:
            //    это делает Auth. Здесь — на будущее для эндпоинтов самого Gateway.
            app.UseAuthorization();

            // 4) Прокси — передаёт запрос в целевой сервис.
            app.MapReverseProxy();

            app.Run();
        }
    }
}