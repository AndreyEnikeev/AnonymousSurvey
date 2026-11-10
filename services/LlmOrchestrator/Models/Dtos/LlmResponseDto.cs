namespace LlmOrchestrator.Models.Dtos
{
    /// <summary>
    /// DTO ответа от внешнего провайдера LLM.
    /// Содержит результаты анализа текста.
    /// </summary>
    public class LlmResponseDto
    {
        /// <summary>
        /// Тональность текста: "positive", "neutral" или "negative".
        /// </summary>
        public string Sentiment { get; init; } = string.Empty;

        /// <summary>
        /// Список выявленных ключевых тем (например, "workload", "practice").
        /// </summary>
        public List<string> Topics { get; init; } = new();

        /// <summary>
        /// Уверенность модели в результате (от 0.0 до 1.0).
        /// </summary>
        public decimal Confidence { get; init; }
    }
}