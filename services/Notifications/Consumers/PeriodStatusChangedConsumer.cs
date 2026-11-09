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
    /// Consumer события PeriodStatusChanged.
    /// 
    /// Получает событие из RabbitMQ (MassTransit), определяет получателей
    /// по новому статусу периода и рассылает email-уведомления через MailHog.
    /// 
    /// Правила маршрутизации (вариант A):
    /// - open     → письмо студентам (role_id = 1): «анкеты открыты»;
    /// - closed   → письмо преподавателям (2) и админам (3): «результаты доступны»;
    /// - archived → письмо только админам (3): «период в архиве»;
    /// - draft и прочие → никому (непубличное состояние).
    /// </summary>
    public class PeriodStatusChangedConsumer : IConsumer<PeriodStatusChanged>
    {
        private readonly NotificationsDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<PeriodStatusChangedConsumer> _logger;

        /// <summary>Конструктор: зависимости внедряются через DI.</summary>
        public PeriodStatusChangedConsumer(
            NotificationsDbContext db,
            IEmailSender emailSender,
            ILogger<PeriodStatusChangedConsumer> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<PeriodStatusChanged> context)
        {
            var evt = context.Message;

            _logger.LogInformation(
                "Получено событие PeriodStatusChanged: PeriodId={PeriodId}, {Old}→{New}",
                evt.PeriodId, evt.OldStatus, evt.NewStatus);

            // 1. Определяем, каким ролям нужно письмо.
            var targetRoleIds = GetTargetRoles(evt.NewStatus);
            if (targetRoleIds.Length == 0)
            {
                _logger.LogInformation(
                    "Для статуса {NewStatus} рассылка не предусмотрена, пропускаем.",
                    evt.NewStatus);
                return;
            }

            // 2. Загружаем адреса получателей одним запросом.
            //    Нужен SELECT на id, email, role_id — поэтому в GRANT
            //    обязательно должен быть указан role_id.
            var recipients = await _db.Users
                .AsNoTracking()
                .Where(u => targetRoleIds.Contains(u.RoleId))
                .Select(u => u.Email)
                .ToListAsync(context.CancellationToken);

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "Получателей для статуса {NewStatus} не найдено (роли: {Roles}).",
                    evt.NewStatus, string.Join(",", targetRoleIds));
                await LogAuditAsync(evt, recipients.Count, 0, new List<string>());
                return;
            }

            // 3. Формируем письмо один раз — все получатели получат одинаковый текст.
            var subject = BuildSubject(evt);
            var body = BuildBody(evt);

            // 4. Отправляем последовательно, собираем ошибки.
            //    Параллельная отправка не нужна: 7 получателей, выигрыш ничтожен,
            //    а ошибки обрабатывать сложнее.
            var sentCount = 0;
            var errors = new List<string>();

            foreach (var email in recipients)
            {
                try
                {
                    await _emailSender.SendAsync(email, subject, body, context.CancellationToken);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось отправить письмо на {Email}", email);
                    errors.Add($"{email}: {ex.Message}");
                }
            }

            _logger.LogInformation(
                "Рассылка завершена: успешно {Sent}, ошибок {Errors}",
                sentCount, errors.Count);

            // 5. Пишем запись в audit_events (append-only).
            //    Оборачиваем в try/catch: если аудит упадет (например, нет прав в БД),
            //    мы НЕ должны вызывать ретрай MassTransit, иначе он повторно отправит письма.
            try
            {
                await LogAuditAsync(evt, recipients.Count, sentCount, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось записать событие в audit_events. Письма отправлены, но факт не залогирован.");
            }
        }

        /// <summary>
        /// Возвращает набор role_id получателей для указанного статуса периода.
        /// </summary>
        private static int[] GetTargetRoles(string newStatus) => newStatus switch
        {
            "open" => new[] { 1 },             // студенты
            "closed" => new[] { 2, 3 },        // преподаватели + админы
            "archived" => new[] { 3 },         // только админы
            _ => Array.Empty<int>()
        };

        /// <summary>Формирует тему письма по новому статусу.</summary>
        private static string BuildSubject(PeriodStatusChanged evt) => evt.NewStatus switch
        {
            "open" => $"[AnonSurvey] Период «{evt.PeriodName}» открыт",
            "closed" => $"[AnonSurvey] Период «{evt.PeriodName}» закрыт",
            "archived" => $"[AnonSurvey] Период «{evt.PeriodName}» архивирован",
            _ => $"[AnonSurvey] Изменение статуса периода «{evt.PeriodName}»"
        };

        /// <summary>Формирует HTML-тело письма.</summary>
        private static string BuildBody(PeriodStatusChanged evt)
        {
            var actionText = evt.NewStatus switch
            {
                "open" => "Анкетирование открыто. Вы можете заполнить доступные анкеты через веб-интерфейс.",
                "closed" => "Анкетирование закрыто. Результаты стали доступны для просмотра в отчётах.",
                "archived" => "Период переведён в архив. Данные доступны только для чтения.",
                _ => "Статус периода был изменён."
            };

            return $"""
                <html>
                <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                    <h2 style="color: #2c3e50;">Уведомление от системы AnonymousSurvey</h2>
                    <table style="border-collapse: collapse; width: 100%;">
                        <tr>
                            <td style="padding: 8px; border: 1px solid #ccc;"><strong>Период</strong></td>
                            <td style="padding: 8px; border: 1px solid #ccc;">{evt.PeriodName}</td>
                        </tr>
                        <tr>
                            <td style="padding: 8px; border: 1px solid #ccc;"><strong>Новый статус</strong></td>
                            <td style="padding: 8px; border: 1px solid #ccc;">{evt.NewStatus}</td>
                        </tr>
                        <tr>
                            <td style="padding: 8px; border: 1px solid #ccc;"><strong>Время события</strong></td>
                            <td style="padding: 8px; border: 1px solid #ccc;">{evt.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                        </tr>
                    </table>
                    <p style="margin-top: 16px;">{actionText}</p>
                    <hr style="margin: 24px 0; border: none; border-top: 1px solid #eee;">
                    <p style="color: #888; font-size: 12px;">
                        Это автоматическое сообщение, отвечать на него не нужно.
                    </p>
                </body>
                </html>
                """;
        }

        /// <summary>
        /// Записывает в audit_events факт рассылки.
        /// 
        /// user_id = NULL — событие инициировано системой, не человеком.
        /// ip_address = "127.0.0.1" — фоновый процесс без сетевого источника;
        /// колонка NOT NULL, значение-заглушка.
        /// </summary>
        private async Task LogAuditAsync(
            PeriodStatusChanged evt,
            int recipientsCount,
            int sentCount,
            List<string> errors)
        {
            var details = JsonSerializer.Serialize(new
            {
                periodName = evt.PeriodName,
                oldStatus = evt.OldStatus,
                newStatus = evt.NewStatus,
                recipientsCount,
                sentCount,
                errorCount = errors.Count,
                errors = errors.Count > 0 ? errors : null
            });

            _db.AuditEvents.Add(new AuditEvent
            {
                UserId = null,
                EventType = errors.Count == 0
                    ? "email_notification_sent"
                    : "email_notification_partial_failure",
                EntityType = "period",
                EntityId = evt.PeriodId.ToString(),
                Details = details,
                IpAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}