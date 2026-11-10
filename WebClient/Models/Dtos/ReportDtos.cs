namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-07: агрегированный отчёт.
    /// </summary>
    public class AggregatedReportResponseDto
    {
        /// <summary>Список строк отчёта.</summary>
        public IEnumerable<AggregatedReportItemDto> Items { get; init; } = new List<AggregatedReportItemDto>();

        /// <summary>Общее количество записей (для пагинации).</summary>
        public int Total { get; init; }

        /// <summary>Примененный лимит.</summary>
        public int Limit { get; init; }

        /// <summary>Примененное смещение.</summary>
        public int Offset { get; init; }
    }

    /// <summary>
    /// Одна строка агрегированного отчёта.
    /// </summary>
    public class AggregatedReportItemDto
    {
        public Guid DisciplineId { get; init; }
        public string DisciplineName { get; init; } = string.Empty;

        public Guid TeacherId { get; init; }
        public string TeacherName { get; init; } = string.Empty;

        public Guid PeriodId { get; init; }
        public string PeriodName { get; init; } = string.Empty;

        /// <summary>Средняя оценка от 1 до 5.</summary>
        public double AverageScore { get; init; }

        /// <summary>Количество числовых ответов (оценок).</summary>
        public int ResponseCount { get; init; }

        /// <summary>Количество текстовых комментариев.</summary>
        public int TextResponsesCount { get; init; }
    }
}