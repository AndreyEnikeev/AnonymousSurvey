using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Surveys.DataDb;
using System.Security.Claims;
using System.Text;

namespace Surveys
{
    /// <summary>
    /// Главный класс сервиса анкетирования (C-04).
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Настройка встроенных сервисов ASP.NET Core
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            // 2. Настройка JWT-аутентификации
            // Ключ подписи должен совпадать с Auth и Gateway, иначе токены не пройдут.
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
                        ClockSkew = TimeSpan.Zero, // Отключаем стандартный 5-минутный запас
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });

            builder.Services.AddAuthorization();

            // 3. Подключение к PostgreSQL
            // Используем роль surveys_service (принцип наименьших привилегий).
            var connectionString = builder.Configuration.GetConnectionString("SurveysDb");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention()); // Маппинг PascalCase -> snake_case

            // 4. Подключение к Redis (нужен для Idempotency-Key в O-05)
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis не настроен в User Secrets");

            builder.Services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));

            // 5. Подключение к RabbitMQ через MassTransit (для отправки событий в O-09)
            // Все настройки (хост, логин, пароль) читаются из User Secrets.
            var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
            var rabbitVHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
            var rabbitUser = builder.Configuration["RabbitMq:Username"]
                ?? throw new InvalidOperationException("RabbitMq:Username не настроен в User Secrets");
            var rabbitPass = builder.Configuration["RabbitMq:Password"]
                ?? throw new InvalidOperationException("RabbitMq:Password не настроен в User Secrets");

            builder.Services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    // Указываем, куда подключаться (localhost - проброшенный порт из Docker)
                    cfg.Host(rabbitHost, rabbitVHost, h =>
                    {
                        h.Username(rabbitUser);
                        h.Password(rabbitPass);
                    });
                });
            });

            var app = builder.Build();

            // 6. HTTP-конвейер (порядок middleware критичен)
            app.UseHttpsRedirection();

            // Кто ты? (проверка JWT)
            app.UseAuthentication();

            // Можно ли тебе? (проверка ролей)
            app.UseAuthorization();

            // Маршрутизация на контроллеры
            app.MapControllers();

            app.Run();
        }
    }
}