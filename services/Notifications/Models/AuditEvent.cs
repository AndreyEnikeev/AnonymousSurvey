namespace Notifications.Models
{
    /// <summary>
    /// Запись в журнале событий безопасности (таблица audit_events).
    /// 
    /// Notifications_service имеет право только на INSERT —
    /// append-only лог, менять и удалять записи нельзя.
    /// </summary>
    public class AuditEvent
    {
        /// <summary>Суррогатный первичный ключ (bigserial).</summary>
        public long Id { get; set; }

        /// <summary>
        /// Идентификатор пользователя-инициатора события.
        /// NULL для системных событий (в нашем случае — рассылки фоновым воркером).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>Тип события: email_notification_sent / email_notification_partial_failure.</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Тип сущности, над которой произошло событие: period.</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>Идентификатор сущности (для period — GUID периода).</summary>
        public string? EntityId { get; set; }

        /// <summary>Детали события в формате JSON (jsonb в БД).</summary>
        public string? Details { get; set; }

        /// <summary>IP-адрес источника события. Обязателен (NOT NULL в схеме).</summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>Момент события (UTC).</summary>
        public DateTime CreatedAt { get; set; }
    }
}