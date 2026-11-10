namespace LlmOrchestrator.Models
{
    /// <summary>
    /// Сущность для таблицы llm_classifications.
    /// Хранит результаты классификации текстовых ответов (тональность, темы).
    /// </summary>
    public class LlmClassification
    {
        /// <summary>
        /// Уникальный идентификатор записи (bigserial).
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Идентификатор ответа из таблицы answers, к которому относится классификация.
        /// </summary>
        public long AnswerId { get; set; }

        /// <summary>
        /// Идентификатор типа тональности (1 - positive, 2 - neutral, 3 - negative).
        /// </summary>
        public short SentimentId { get; set; }

        /// <summary>
        /// Список выявленных тем в формате JSON (jsonb).
        /// </summary>
        public string Topics { get; set; } = string.Empty;

        /// <summary>
        /// Уверенность модели в результате (от 0.00 до 1.00).
        /// </summary>
        public decimal Confidence { get; set; }

        /// <summary>
        /// Время проведения классификации (UTC).
        /// </summary>
        public DateTime ClassifiedAt { get; set; }
    }
}