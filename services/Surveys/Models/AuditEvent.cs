using System;

namespace Surveys.Models
{
    /// <summary>
    /// Запись журнала событий безопасности (таблица audit_events).
    /// Используется для фиксации административных действий (например, смены статуса периода).
    /// </summary>
    public class AuditEvent
    {
        /// <summary>Уникальный идентификатор записи (bigserial).</summary>
        public long Id { get; set; }

        /// <summary>Идентификатор пользователя, совершившего действие (может быть null для системных событий).</summary>
        public Guid? UserId { get; set; }

        /// <summary>Тип события (например, "period_status_changed").</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Тип сущности, к которой относится событие (например, "period").</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>Идентификатор сущности в виде строки.</summary>
        public string? EntityId { get; set; }

        /// <summary>Дополнительные детали события в формате JSON.</summary>
        public string? Details { get; set; }

        /// <summary>IP-адрес клиента, с которого пришёл запрос.</summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>Дата и время события в UTC.</summary>
        public DateTime CreatedAt { get; set; }
    }
}