namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO одной записи журнала аудита (O-12).
    /// </summary>
    public class AuditEventDto
    {
        /// <summary>Уникальный идентификатор записи.</summary>
        public long Id { get; init; }

        /// <summary>Идентификатор пользователя (null для системных событий).</summary>
        public Guid? UserId { get; init; }

        /// <summary>Тип события (login_success, period_status_changed и т.д.).</summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>Тип сущности (user, period, survey).</summary>
        public string EntityType { get; init; } = string.Empty;

        /// <summary>Идентификатор сущности в виде строки.</summary>
        public string? EntityId { get; init; }

        /// <summary>Дополнительные детали в формате JSON.</summary>
        public string? Details { get; init; }

        /// <summary>IP-адрес клиента.</summary>
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>Дата и время события в UTC.</summary>
        public DateTime CreatedAt { get; init; }
    }

    /// <summary>
    /// DTO страницы журнала аудита (O-12).
    /// </summary>
    public class AuditEventsPage
    {
        /// <summary>Список записей на текущей странице.</summary>
        public List<AuditEventDto> Items { get; init; } = new();

        /// <summary>Общее количество записей по фильтру.</summary>
        public int Total { get; init; }

        /// <summary>Примененный лимит (размер страницы).</summary>
        public int Limit { get; init; }

        /// <summary>Примененное смещение (сколько пропущено).</summary>
        public int Offset { get; init; }
    }
}