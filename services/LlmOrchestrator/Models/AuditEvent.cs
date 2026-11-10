namespace LlmOrchestrator.Models
{
    /// <summary>
    /// Сущность для таблицы audit_events.
    /// Используется для записи логов успешных или неуспешных вызовов LLM.
    /// </summary>
    public class AuditEvent
    {
        /// <summary>
        /// Уникальный идентификатор события (bigserial).
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Идентификатор пользователя. Для фоновых процессов (как наш) всегда null.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Тип события (например, llm_classification_failed).
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Тип сущности, над которой производилось действие (например, answer).
        /// </summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Идентификатор сущности в виде строки.
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// Детали события в формате JSON (jsonb).
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// IP-адрес источника. Для фоновых процессов ставим заглушку.
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Время события (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}