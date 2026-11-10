using Analytics.DataDb;
using Analytics.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;

namespace Analytics
{
    /// <summary>
    /// Главный класс приложения (точка входа) для сервиса аналитики (C-05).
    /// Настраивает DI-контейнер, middleware и запускает веб-сервер.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Основной метод, выполняющий настройку и запуск приложения.
        /// </summary>
        /// <param name="args">Аргументы командной строки.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. НАСТРОЙКА ВСТРОЕННЫХ СЕРВИСОВ ASP.NET CORE
            builder.Services.AddControllers(); // Подключаем контроллеры

            // 2. НАСТРОЙКА JWT-АУТЕНТИФИКАЦИИ
            // Ключ должен совпадать с Auth, Gateway и Surveys.
            var jwtKey = builder.Configuration["Jwt:SigningKey"]!;

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ClockSkew = TimeSpan.Zero, // Убираем стандартный запас времени в 5 минут
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });

            builder.Services.AddAuthorization(); // Подключаем проверку политик/ролей

            // 3. ПОДКЛЮЧЕНИЕ К POSTGRESQL
            // Используем роль analytics_service (принцип наименьших привилегий).
            builder.Services.AddDbContext<AnalyticsDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("AnalyticsDb"))
                       .UseSnakeCaseNamingConvention()); // Маппинг PascalCase -> snake_case

            // 4. ПОДКЛЮЧЕНИЕ К REDIS
            // Redis используется как кэш для агрегированных отчетов (O-07).
            builder.Services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

            // 5. РЕГИСТРАЦИЯ НАШИХ СЕРВИСОВ
            // MinioService будет жить всё время работы приложения (Singleton).
            builder.Services.AddSingleton<MinioService>();

            var app = builder.Build();

            // 6. НАСТРОЙКА HTTP-КОНВЕЙЕРА (MIDDLEWARE)

            // Проверка: кто ты? (валидация токена)
            app.UseAuthentication();

            // Проверка: можно ли тебе? (проверка ролей)
            app.UseAuthorization();

            // Маршрутизация на контроллеры
            app.MapControllers();

            // Запуск сервера (Kestrel) на порту 5137.
            app.Run();
        }
    }
}