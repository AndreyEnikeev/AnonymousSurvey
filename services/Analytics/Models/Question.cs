namespace Analytics.Models
{
    /// <summary>Вопрос внутри анкеты (таблица questions).</summary>
    public class Question
    {
        /// <summary>Уникальный идентификатор вопроса.</summary>
        public long Id { get; set; }

        /// <summary>Идентификатор шаблона анкеты.</summary>
        public Guid SurveyTemplateId { get; set; }

        /// <summary>Текст вопроса.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Тип вопроса: 1 - scale, 2 - text.</summary>
        public short TypeId { get; set; }
    }
}