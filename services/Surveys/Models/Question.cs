namespace Surveys.Models
{
    /// <summary>Вопрос анкеты (таблица questions).</summary>
    public class Question
    {
        /// <summary>Уникальный идентификатор вопроса (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Ссылка на шаблон анкеты, к которому относится вопрос.</summary>
        public Guid SurveyTemplateId { get; set; }

        /// <summary>Текст вопроса.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Ссылка на тип вопроса (question_types.id).</summary>
        public byte TypeId { get; set; }

        /// <summary>Навигационное свойство: тип вопроса.</summary>
        public QuestionType Type { get; set; } = null!;
    }
}