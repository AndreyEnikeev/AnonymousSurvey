namespace LlmOrchestrator.Models.Dtos
{
    /// <summary>
    /// DTO запроса к внешнему провайдеру LLM (C-12).
    /// Содержит только обезличенный текст комментария.
    /// </summary>
    public class LlmRequestDto
    {
        /// <summary>
        /// Обезличенный текст комментария студента.
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }
}