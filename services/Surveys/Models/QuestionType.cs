namespace Surveys.Models
{
    /// <summary>
    /// Справочник типов вопросов (таблица question_types).
    /// Значения: scale / text.
    /// </summary>
    public class QuestionType
    {
        /// <summary>Идентификатор типа (1=scale, 2=text).</summary>
        public byte Id { get; set; }

        /// <summary>Строковый код типа.</summary>
        public string Code { get; set; } = string.Empty;
    }
}