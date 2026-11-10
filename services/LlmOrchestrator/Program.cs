using LlmOrchestrator.Consumers;
using LlmOrchestrator.DataDb;
using LlmOrchestrator.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LlmOrchestrator
{
    /// <summary>
    /// Главный класс приложения LLM-оркестратора (C-06).
    /// Запускает как фоновый консьюмер RabbitMQ, так и HTTP-контроллер для эндпоинта O-11.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Точка входа в приложение.
        /// </summary>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Встроенные сервисы
            builder.Services.AddControllers();

            // 2. Подключение к БД
            builder.Services.AddDbContext<LlmDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("LlmDb"))
                       .UseSnakeCaseNamingConvention());

            // 3. HTTP-клиент (через фабрику)
            builder.Services.AddHttpClient<LlmClient>();

            // 4. Настройка MassTransit + RabbitMQ
            var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
            var rabbitUser = builder.Configuration["RabbitMq:Username"]!;
            var rabbitPass = builder.Configuration["RabbitMq:Password"]!;

            builder.Services.AddMassTransit(x =>
            {
                x.AddConsumer<LlmClassificationConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitHost, "/", h =>
                    {
                        h.Username(rabbitUser);
                        h.Password(rabbitPass);
                        h.Heartbeat(TimeSpan.FromSeconds(30));
                    });

                    // Настраиваем очередь для получения задач.
                    cfg.ReceiveEndpoint("llm-classification-queue", e =>
                    {
                        e.ConfigureConsumer<LlmClassificationConsumer>(context);

                        // Ретраи: если LLM недоступна, попробуем еще раз через 5 и 10 секунд.
                        e.UseMessageRetry(r => r.Intervals(5000, 10000));
                    });
                });
            });

            var app = builder.Build();

            // HTTP-конвейер
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}