namespace Analytics.Models.Dtos
{
    /// <summary>
    /// Элемент агрегированного обезличенного отчета (DTO).
    /// Представляет собой снимок данных за определенный период по конкретной дисциплине и преподавателю.
    /// Использует init-сеттеры, так как данные не должны меняться после создания.
    /// </summary>
    public class AggregatedReportItemDto
    {
        /// <summary>
        /// Уникальный идентификатор дисциплины.
        /// </summary>
        public Guid DisciplineId { get; init; }

        /// <summary>
        /// Уникальный идентификатор преподавателя.
        /// </summary>
        public Guid TeacherId { get; init; }

        /// <summary>
        /// Уникальный идентификатор периода анкетирования.
        /// </summary>
        public Guid PeriodId { get; init; }

        /// <summary>
        /// Средняя оценка по всем вопросам шкалы (1–5).
        /// Округляется до двух знаков после запятой.
        /// </summary>
        public double AverageScore { get; init; }

        /// <summary>
        /// Общее количество полученных ответов на вопросы с оценкой.
        /// </summary>
        public int ResponseCount { get; init; }

        /// <summary>Количество текстовых комментариев (добавлено).</summary>
        public int TextResponsesCount { get; init; }

        /// <summary>
        /// Результаты классификации комментариев (тональность).
        /// Всегда null в текущей реализации, так как LLM исключен из задания (D-01).
        /// </summary>
        public object? Sentiment { get; init; }
    }

    /// <summary>
    /// Ответ на запрос агрегированного отчета (DTO).
    /// Содержит список записей и метаданные пагинации.
    /// </summary>
    public class AggregatedReportResponseDto
    {
        /// <summary>
        /// Список агрегированных записей.
        /// </summary>
        public IEnumerable<AggregatedReportItemDto> Items { get; init; } = new List<AggregatedReportItemDto>();

        /// <summary>
        /// Общее количество записей в базе данных, удовлетворяющих фильтрам (для пагинации).
        /// </summary>
        public int Total { get; init; }

        /// <summary>
        /// Примененное ограничение на количество возвращаемых записей.
        /// </summary>
        public int Limit { get; init; }

        /// <summary>
        /// Примененное смещение для пагинации.
        /// </summary>
        public int Offset { get; init; }
    }
}