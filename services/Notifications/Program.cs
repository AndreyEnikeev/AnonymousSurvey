using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notifications.Consumers;
using Notifications.DataDb;
using Notifications.Services;

namespace Notifications
{
    /// <summary>
    /// Точка входа фонового сервиса уведомлений (C-07).
    /// 
    /// Не является ASP.NET Core-приложением: HTTP-контроллеров нет,
    /// снаружи сервис не вызывается. Единственный входной канал — RabbitMQ:
    /// Consumer забирает события PeriodStatusChanged и отправляет письма.
    /// </summary>
    public class Program
    {
        /// <summary>Главный метод. Настраивает DI-контейнер и запускает хост.</summary>
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // 1. PostgreSQL — для чтения users (id, email, role_id) и записи audit_events.
            var connectionString = builder.Configuration.GetConnectionString("NotificationsDb")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:NotificationsDb не настроен в User Secrets");

            builder.Services.AddDbContext<NotificationsDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention());

            // 2. Отправитель писем.
            //    Singleton: SmtpEmailSender не хранит состояния, а SmtpClient создаётся
            //    на каждый вызов (внутри SendAsync) — гонок за соединение нет.
            builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

            // 3. MassTransit + RabbitMQ.
            var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
            var rabbitVHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
            var rabbitUser = builder.Configuration["RabbitMq:Username"]
                ?? throw new InvalidOperationException("RabbitMq:Username не настроен в User Secrets");
            var rabbitPass = builder.Configuration["RabbitMq:Password"]
                ?? throw new InvalidOperationException("RabbitMq:Password не настроен в User Secrets");
            builder.Services.AddMassTransit(x =>
            {
                x.AddConsumer<PeriodStatusChangedConsumer>();
                x.AddConsumer<PasswordResetRequestedConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitHost, rabbitVHost, h =>
                    {
                        h.Username(rabbitUser);
                        h.Password(rabbitPass);
                        h.Heartbeat(TimeSpan.FromSeconds(30));
                    });

                    // ============================================================
                    // Endpoint 1: смена статуса периода
                    // ============================================================
                    cfg.ReceiveEndpoint("notifications-period-status-changed", e =>
                    {
                        e.ConfigureConsumer<PeriodStatusChangedConsumer>(context);

                        e.UseMessageRetry(r => r.Intervals(
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(15),
                            TimeSpan.FromSeconds(30)));
                    });

                    // ============================================================
                    // Endpoint 2: восстановление пароля (D-14)
                    // ============================================================
                    cfg.ReceiveEndpoint("notifications-password-reset-requested", e =>
                    {
                        e.ConfigureConsumer<PasswordResetRequestedConsumer>(context);

                        // Ретраи: если SMTP временно недоступен, MassTransit повторит
                        // обработку 3 раза с паузами. После — сообщение уйдёт в
                        // notifications-password-reset-requested_error.
                        e.UseMessageRetry(r => r.Intervals(
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(15),
                            TimeSpan.FromSeconds(30)));
                    });
                });
            });
            var host = builder.Build();
            host.Run();
        }
    }
}