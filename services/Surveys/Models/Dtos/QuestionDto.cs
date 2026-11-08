namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO одного вопроса в ответе O-13.
    /// Это снимок данных — создаётся контроллером один раз
    /// </summary>
    public class QuestionDto
    {
        /// <summary>Уникальный идентификатор вопроса.</summary>
        public long QuestionId { get; init; }

        /// <summary>Текст вопроса.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Тип вопроса: "scale" или "text".
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Варианты ответа. Для типа "scale" — [1,2,3,4,5].
        /// Для типа "text" — null (свободный ввод, вариантов нет).
        /// </summary>
        public List<int>? Options { get; init; }
    }
}