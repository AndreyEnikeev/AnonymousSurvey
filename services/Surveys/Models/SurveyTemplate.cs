namespace Surveys.Models
{
    /// <summary>
    /// Шаблон анкеты (таблица survey_templates).
    /// Привязывает период, дисциплину и преподавателя к конкретной анкете.
    /// </summary>
    public class SurveyTemplate
    {
        /// <summary>Уникальный идентификатор анкеты.</summary>
        public Guid Id { get; set; }

        /// <summary>Ссылка на период анкетирования (periods.id).</summary>
        public Guid PeriodId { get; set; }

        /// <summary>Ссылка на дисциплину (disciplines.id).</summary>
        public Guid DisciplineId { get; set; }

        /// <summary>Ссылка на преподавателя (teachers.id).</summary>
        public Guid TeacherId { get; set; }

        /// <summary>Ссылка на статус анкеты (survey_statuses.id).</summary>
        public byte StatusId { get; set; }

        /// <summary>Навигационное свойство: период анкетирования.</summary>
        public Period Period { get; set; } = null!;

        /// <summary>Навигационное свойство: дисциплина.</summary>
        public Discipline Discipline { get; set; } = null!;

        /// <summary>Навигационное свойство: преподаватель.</summary>
        public Teacher Teacher { get; set; } = null!;

        /// <summary>Навигационное свойство: статус анкеты.</summary>
        public SurveyStatus Status { get; set; } = null!;

        /// <summary>Навигационное свойство: список вопросов анкеты.</summary>
        public List<Question> Questions { get; set; } = new();
    }
}