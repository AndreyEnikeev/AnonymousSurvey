using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notifications.Contracts;
using Notifications.DataDb;
using Notifications.Models;
using Notifications.Services;
using System.Text.Json;

namespace Notifications.Consumers
{
    /// <summary>
    /// Consumer события PasswordResetRequested.
    ///
    /// Задача: сформировать письмо со ссылкой на страницу сброса пароля
    /// и отправить его через IEmailSender (в dev — в MailHog).
    ///
    /// Не выполняет бизнес-логику восстановления. Токен уже сгенерирован
    /// и сохранён в БД сервисом Auth (в виде хеша). Здесь — только доставка.
    ///
    /// Если отправка письма упадёт, MassTransit выполнит ретрай согласно
    /// политике RabbitMQ. Но на защите важно объяснить: возможна повторная
    /// доставка, поэтому consumer должен быть идемпотентным по эффекту —
    /// тут это обеспечено тем, что дубль письма не критичен (пользователь
    /// получит два письма с одним токеном, использовать сможет любой из них
    /// до истечения TTL или до первого успешного confirm).
    /// </summary>
    public class PasswordResetRequestedConsumer : IConsumer<PasswordResetRequested>
    {
        private readonly NotificationsDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;
        private readonly ILogger<PasswordResetRequestedConsumer> _logger;

        /// <summary>Конструктор: зависимости внедряются через DI.</summary>
        public PasswordResetRequestedConsumer(
            NotificationsDbContext db,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger<PasswordResetRequestedConsumer> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _config = config;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<PasswordResetRequested> context)
        {
            var evt = context.Message;

            _logger.LogInformation(
                "Получено событие PasswordResetRequested для {Email} (UserId={UserId})",
                evt.Email, evt.UserId);

            // 1. Строим ссылку на страницу сброса пароля WebClient (C-01).
            //    BaseUrl — в конфигурации Notifications (appsettings.json),
            //    потому что адрес WebClient зависит от окружения (dev / docker).
            //    Uri.EscapeDataString — на случай спецсимволов в токене
            //    (сейчас там GUID, но defensive coding).
            var webClientBaseUrl = _config["WebClient:BaseUrl"] ?? "http://localhost:5000";
            var resetUrl = $"{webClientBaseUrl.TrimEnd('/')}/Account/ResetPassword?token={Uri.EscapeDataString(evt.ResetToken)}";

            // 2. Формируем письмо.
            var subject = "[AnonSurvey] Восстановление пароля";
            var body = BuildBody(evt, resetUrl);

            // 3. Отправляем.
            try
            {
                await _emailSender.SendAsync(evt.Email, subject, body, context.CancellationToken);
                _logger.LogInformation("Письмо о сбросе пароля отправлено на {Email}", evt.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось отправить письмо о сбросе пароля на {Email}", evt.Email);
                // Пробрасываем — пусть MassTransit сделает ретрай по политике.
                // Если политика исчерпает попытки, сообщение уйдёт в _error-очередь,
                // и его можно разобрать вручную.
                throw;
            }

            // 4. Пишем в audit_events (append-only).
            //    Оборачиваем в try/catch — если аудит упадёт (нет прав в БД),
            //    НЕ вызываем ретрай MassTransit: письмо уже отправлено,
            //    повторная отправка нежелательна.
            try
            {
                await LogAuditAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Не удалось записать password_reset_notification_sent в audit_events. Письмо отправлено, но факт не залогирован.");
            }
        }

        /// <summary>
        /// Строит HTML-тело письма. Ссылка сброса пароля кликабельна.
        /// Не содержит логина и полного имени — только обращение.
        /// </summary>
        private static string BuildBody(PasswordResetRequested evt, string resetUrl)
        {
            // Экранируем ФИО: вдруг там окажется < или & — сломает HTML или станет XSS.
            var safeName = System.Net.WebUtility.HtmlEncode(evt.FullName);

            return $"""
                <html>
                <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                    <h2 style="color: #2c3e50;">Восстановление пароля в AnonymousSurvey</h2>
                    <p>Здравствуйте, {safeName}!</p>
                    <p>Вы запросили восстановление пароля. Чтобы задать новый пароль, перейдите по ссылке:</p>
                    <p>
                        <a href="{resetUrl}"
                           style="display: inline-block; padding: 10px 20px; background: #0d6efd; color: #fff;
                                  text-decoration: none; border-radius: 4px;">
                            Сбросить пароль
                        </a>
                    </p>
                    <p style="color: #666;">
                        Ссылка действует до {evt.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC.
                        Если вы не запрашивали сброс — просто проигнорируйте это письмо.
                    </p>
                    <p style="color: #666; font-size: 12px; word-break: break-all;">
                        Если кнопка не работает, скопируйте в адресную строку:<br />
                        {resetUrl}
                    </p>
                    <hr style="margin: 24px 0; border: none; border-top: 1px solid #eee;">
                    <p style="color: #888; font-size: 12px;">
                        Это автоматическое сообщение, отвечать на него не нужно.
                    </p>
                </body>
                </html>
                """;
        }

        /// <summary>
        /// Пишет в audit_events факт отправки письма.
        /// user_id = NULL — инициатор здесь фоновый процесс; в Auth уже есть запись
        /// password_reset_requested с реальным user_id, так что связь не теряется.
        /// </summary>
        private async Task LogAuditAsync(PasswordResetRequested evt)
        {
            var details = JsonSerializer.Serialize(new
            {
                userId = evt.UserId,
                email = evt.Email,
                expiresAt = evt.ExpiresAt
            });

            _db.AuditEvents.Add(new AuditEvent
            {
                UserId = null,
                EventType = "password_reset_notification_sent",
                EntityType = "user",
                EntityId = evt.UserId.ToString(),
                Details = details,
                IpAddress = "127.0.0.1", // NOT NULL в схеме, фоновый процесс без сетевого источника
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}