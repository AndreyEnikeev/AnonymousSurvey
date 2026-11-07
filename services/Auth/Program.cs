using Auth.Data;
using Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Auth
{
    /// <summary>
    /// Главный класс приложения, точка входа (Entry Point).
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. НАСТРОЙКА ВСТРОЕННЫХ СЕРВИСОВ ASP.NET CORE
            builder.Services.AddControllers();
#if DEBUG
            // Swagger с поддержкой JWT: добавляет кнопку Authorize,
            // чтобы можно было проверять защищённые эндпоинты прямо в UI.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Вставьте JWT access-токен."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
#endif
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

            var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString!));

            // 4. РЕГИСТРАЦИЯ НАШИХ СЕРВИСОВ
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<TokenService>();

            var app = builder.Build();
#if DEBUG
            if (app.Environment.IsDevelopment() && args.Contains("--seed"))
            {
                SeedPasswords(app);
                return;
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
#endif
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
#if DEBUG
        private static void SeedPasswords(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

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

            Console.WriteLine("=== Начинаем генерацию хешей для тестовых пользователей ===");

            foreach (var kvp in testUsers)
            {
                var login = kvp.Key;
                var password = kvp.Value;

                var user = db.Users.FirstOrDefault(u => u.Login == login);
                if (user != null)
                {
                    user.PasswordSalt = hasher.GenerateSalt();
                    user.PasswordHash = hasher.HashPassword(password, user.PasswordSalt);
                    Console.WriteLine($"Пароль для {login} успешно обновлен.");
                }
                else
                {
                    Console.WriteLine($"ВНИМАНИЕ: Пользователь {login} не найден в базе!");
                }
            }

            db.SaveChanges();
            Console.WriteLine("=== Все хеши успешно сохранены в БД! ===");
        }
#endif
    }
}