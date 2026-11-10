using Auth.DataDb;
using Auth.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Auth
{
    /// <summary>
    /// Главный класс приложения Auth (C-03), точка входа.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Основной метод, выполняющий настройку и запуск сервиса аутентификации.
        /// </summary>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. НАСТРОЙКА ВСТРОЕННЫХ СЕРВИСОВ ASP.NET CORE
            builder.Services.AddControllers();

            // 2. ПРОВЕРКА JWT ВНУТРИ AUTH (defense in depth)
            var jwtKey = builder.Configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey не настроен (User Secrets или Environment)");

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

            // 3. ПОДКЛЮЧЕНИЕ ВНЕШНИХ РЕСУРСОВ
            var connectionString = builder.Configuration.GetConnectionString("AuthDb");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis не настроен (User Secrets или Environment)");

            // Настраиваем параметры подключения к Redis.
            var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;

            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions));

            // 4. РЕГИСТРАЦИЯ НАШИХ СЕРВИСОВ
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<TokenService>();

            // ================================================================
            // 5. MASS TRANSIT (публикация событий в RabbitMQ)
            // ================================================================
            builder.Services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
                    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq не настроен (User Secrets или Environment)");

                    cfg.Host(new Uri(rabbitConnectionString), h =>
                    {
                        // Heartbeat каждые 30 секунд — иначе брокер закрывает простаивающее TCP-соединение
                        h.Heartbeat(TimeSpan.FromSeconds(30));
                    });
                });
            });

            var app = builder.Build();

            // 6. АВТОМАТИЧЕСКОЕ ОБНОВЛЕНИЕ ПАРОЛЕЙ (Seed)
            // ВАЖНО: убрано #if DEBUG, иначе при сборке в Release (Docker) пароли не захешируются.
            // Метод сам проверяет наличие заглушек и выходит, если их нет.
            SeedPasswordsAutomatically(app);

            // 7. HTTP-КОНВЕЙЕР (порядок критичен)
            // В Docker контейнер общается по HTTP (внутри изолированной сети),
            // поэтому UseHttpsRedirection применяем ТОЛЬКО в локальной разработке.
            // В Production (Docker) TLS-терминация выполняется на Gateway (см. D-03, D-21).
            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }

        /// <summary>
        /// Проверяет наличие "заглушек" паролей в БД и автоматически генерирует для них Argon2id-хэши.
        /// 
        /// Устойчив к ситуации "Postgres ещё не готов принимать соединения":
        /// делаем несколько попыток с паузой, и только потом сдаёмся.
        /// Это позволяет запускать Auth раньше контейнера postgres — 
        /// сервис подождёт инициализации, а не упадёт с SocketException.
        /// </summary>
        private static void SeedPasswordsAutomatically(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // 5 попыток с интервалом 10 секунд = до 50 секунд ожидания.
            // Этого достаточно, чтобы дождаться старта Postgres в Docker Compose.
            const int maxAttempts = 5;
            const int delaySeconds = 10;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Проверяем, есть ли хотя бы один пользователь с "заглушкой"
                    bool hasPlaceholders = db.Users.Any(u => u.PasswordHash.StartsWith("PLACEHOLDER_"));

                    if (!hasPlaceholders)
                    {
                        // Заглушек нет — пароли уже сгенерированы, выходим.
                        return;
                    }

                    Console.WriteLine("=== Обнаружены заглушки паролей. Генерация реальных Argon2id-хэшей... ===");

                    var testUsers = new Dictionary<string, string>
                    {
                        { "student1", "Student1!Pass" },
                        { "student2", "Student2!Pass" },
                        { "student3", "Student3!Pass" },
                        { "teacher1", "Teacher1!Pass" },
                        { "teacher2", "Teacher2!Pass" },
                        { "admin1",   "Admin1!Pass"   },
                        { "tech1",    "Tech1!Pass"    }
                    };

                    foreach (var kvp in testUsers)
                    {
                        var user = db.Users.FirstOrDefault(u => u.Login == kvp.Key);
                        if (user != null)
                        {
                            user.PasswordSalt = hasher.GenerateSalt();
                            user.PasswordHash = hasher.HashPassword(kvp.Value, user.PasswordSalt);
                        }
                    }

                    db.SaveChanges();
                    Console.WriteLine("=== Все хэши успешно сгенерированы и сохранены в БД! ===");
                    return; // успех — выходим из цикла
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    // Логируем и ждём перед следующей попыткой.
                    logger.LogWarning(ex,
                        "Попытка {Attempt}/{Max} подключиться к БД для seed паролей не удалась. " +
                        "Ждём {Delay} сек и пробуем снова.",
                        attempt, maxAttempts, delaySeconds);
                    Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                }
                catch (Exception ex)
                {
                    // Последняя попытка тоже упала — логируем и НЕ валим приложение.
                    logger.LogError(ex,
                        "Не удалось выполнить seed паролей после {Max} попыток. " +
                        "Сервис запустится, но пароли останутся заглушками — " +
                        "вход в систему будет невозможен до успешного seed.",
                        maxAttempts);
                }
            }
        }
    }
}