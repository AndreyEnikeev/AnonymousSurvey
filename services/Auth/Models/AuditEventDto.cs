namespace Auth.Models
{
    /// <summary>
    /// Модель данных (DTO) для одной записи журнала аудита.
    /// Используется в ответе эндпоинта O-12 (GET /api/audit/events).
    /// Свойства инициализируются только при создании
    /// </summary>
    public class AuditEventDto
    {
        /// <summary>Уникальный идентификатор записи.</summary>
        public long Id { get; init; }

        /// <summary>Идентификатор пользователя или null для системных событий.</summary>
        public Guid? UserId { get; init; }

        /// <summary>Тип события (login_success, login_failed и т.д.).</summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>Тип сущности (user, period, survey).</summary>
        public string EntityType { get; init; } = string.Empty;

        /// <summary>Идентификатор сущности в виде строки или null.</summary>
        public string? EntityId { get; init; }

        /// <summary>Дополнительные детали в формате JSON или null.</summary>
        public string? Details { get; init; }

        /// <summary>IP-адрес клиента.</summary>
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>Дата и время события в UTC.</summary>
        public DateTime CreatedAt { get; init; }
    }

    /// <summary>
    /// Модель данных (DTO) для ответа эндпоинта O-12 (GET /api/audit/events).
    /// Содержит страницу записей журнала аудита с метаданными пагинации.
    /// </summary>
    public class AuditEventsPage
    {
        /// <summary>Список записей журнала на текущей странице.</summary>
        public List<AuditEventDto> Items { get; init; } = new();

        /// <summary>Общее количество записей по фильтру (без учёта пагинации).</summary>
        public int Total { get; init; }

        /// <summary>Применённый размер страницы (1–100).</summary>
        public int Limit { get; init; }

        /// <summary>Применённое смещение.</summary>
        public int Offset { get; init; }
    }
}