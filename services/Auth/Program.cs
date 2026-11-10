using Auth.DataDb;
using Auth.Services;
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

            // 3. ПОДКЛЮЧЕНИЕ ВНЕШНИХ РЕСУРСОВ
            var connectionString = builder.Configuration.GetConnectionString("AuthDb");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis не настроен в User Secrets");

            // Настраиваем параметры подключения к Redis.
            var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;

            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions));

            // 4. РЕГИСТРАЦИЯ НАШИХ СЕРВИСОВ
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<TokenService>();

            var app = builder.Build();

#if DEBUG
            // 5. АВТОМАТИЧЕСКОЕ ОБНОВЛЕНИЕ ПАРОЛЕЙ
            // Проверяем базу: если там лежат заглушки из seed.sql, заменяем их на реальные хэши.
            if (app.Environment.IsDevelopment())
            {
                SeedPasswordsAutomatically(app);
            }
#endif
            // 6. HTTP-КОНВЕЙЕР (порядок критичен)
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }

#if DEBUG
        /// <summary>
        /// Проверяет наличие "заглушек" паролей в БД и автоматически генерирует для них Argon2id-хэши.
        /// Выполняется только при запуске, не требует ручного вмешательства.
        /// </summary>
        private static void SeedPasswordsAutomatically(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

            // Проверяем, есть ли хотя бы один пользователь с "заглушкой"
            bool hasPlaceholders = db.Users.Any(u => u.PasswordHash.StartsWith("PLACEHOLDER_"));

            if (!hasPlaceholders)
            {
                // Если заглушек нет, значит пароли уже сгенерированы, ничего не делаем.
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
                var login = kvp.Key;
                var password = kvp.Value;

                var user = db.Users.FirstOrDefault(u => u.Login == login);
                if (user != null)
                {
                    user.PasswordSalt = hasher.GenerateSalt();
                    user.PasswordHash = hasher.HashPassword(password, user.PasswordSalt);
                }
            }

            db.SaveChanges();
            Console.WriteLine("=== Все хэши успешно сгенерированы и сохранены в БД! ===");
        }
#endif
    }
}