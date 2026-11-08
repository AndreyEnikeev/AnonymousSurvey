using Gateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;

namespace Gateway
{
    /// <summary>
    /// √лавный класс приложени€ Gateway (C-02).
    /// ќтвечает за: валидацию JWT, rate limiting через Redis,
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
            // ѕараметры (Issuer, Audience, SigningKey) ƒќЋ∆Ќџ совпадать с Auth.
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
            // 2. Redis Ч дл€ распределЄнного Rate Limiting
            // ==========================================================
            var redisConnection = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis не настроен");

            builder.Services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnection));

            // ==========================================================
            // 3. YARP: маршруты и кластеры из appsettings.json
            // ==========================================================
            builder.Services
                .AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            // ==========================================================
            // 4. HTTP-конвейер (пор€док критичен)
            // ==========================================================
            app.UseHttpsRedirection();

            // 1) јутентификаци€ Ч Ђкто тыї. Ќевалидный/просроченный токен ? 401.
            app.UseAuthentication();

            // 2) Rate Limiter Ч лимит на пользовател€ через Redis.
            //    ѕосле UseAuthentication, потому что читает userId из токена.
            app.UseMiddleware<RedisRateLimitMiddleware>();

            // 3) јвторизаци€ Ч Ђможно ли тебеї. –оли в Gateway Ќ≈ провер€ютс€:
            //    это делает Auth. «десь Ч на будущее дл€ эндпоинтов самого Gateway.
            app.UseAuthorization();

            // 4) ѕрокси Ч передаЄт запрос в целевой сервис.
            app.MapReverseProxy();

            app.Run();
        }
    }
}