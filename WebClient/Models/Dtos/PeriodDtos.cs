namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-08: информация о периоде анкетирования.
    /// Используется для отображения в таблице периодов.
    /// </summary>
    public class PeriodDto
    {
        /// <summary>Уникальный идентификатор периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Название периода (например, "Осенний семестр 2026").</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Дата начала периода.</summary>
        public DateOnly StartDate { get; init; }

        /// <summary>Дата окончания периода.</summary>
        public DateOnly EndDate { get; init; }

        /// <summary>
        /// Текущий статус: "draft", "open", "closed" или "archived".
        /// Определяет, можно ли сейчас голосовать (только в open).
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO запроса O-09: изменение статуса периода.
    /// </summary>
    public class UpdatePeriodStatusRequest
    {
        /// <summary>
        /// Новый статус: open, closed или archived.
        /// Переход в draft из других статусов не предусмотрен бизнес-логикой.
        /// </summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Комментарий к изменению (необязательно). Сохраняется в аудите.
        /// </summary>
        public string? Comment { get; init; }
    }

    /// <summary>
    /// DTO ответа O-09: результат смены статуса.
    /// </summary>
    public class UpdatePeriodStatusResponse
    {
        /// <summary>Идентификатор изменённого периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Установленный новый статус.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Время изменения по UTC.</summary>
        public DateTime UpdatedAt { get; init; }
    }
}