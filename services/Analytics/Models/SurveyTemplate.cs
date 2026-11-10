namespace Analytics.Models
{
    /// <summary>Шаблон анкеты (таблица survey_templates).</summary>
    public class SurveyTemplate
    {
        /// <summary>Уникальный идентификатор шаблона.</summary>
        public Guid Id { get; set; }

        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; set; }

        /// <summary>Идентификатор дисциплины.</summary>
        public Guid DisciplineId { get; set; }

        /// <summary>Идентификатор преподавателя.</summary>
        public Guid TeacherId { get; set; }

        /// <summary>Идентификатор статуса.</summary>
        public short StatusId { get; set; }

        /// <summary>Навигационное свойство: период.</summary>
        public Period Period { get; set; } = null!;

        /// <summary>Навигационное свойство: дисциплина.</summary>
        public Discipline Discipline { get; set; } = null!;

        /// <summary>Навигационное свойство: преподаватель.</summary>
        public Teacher Teacher { get; set; } = null!;
    }
}