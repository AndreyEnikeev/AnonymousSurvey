namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-13: структура анкеты для заполнения студентом.
    /// Содержание ранее отправленных анкет не возвращается.
    /// </summary>
    public class SurveyDetailsDto
    {
        /// <summary>Идентификатор анкеты.</summary>
        public Guid SurveyId { get; init; }

        /// <summary>
        /// Название анкеты для отображения студенту.
        /// Формируется как "{Дисциплина} — {Логин преподавателя}".
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Идентификатор периода анкетирования.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Статус анкеты: "active" или "closed".</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Список вопросов анкеты.
        /// Пустой список, если вопросов нет (не должно быть на практике, но безопасно).
        /// </summary>
        public List<QuestionDto> Questions { get; init; } = new();

        /// <summary>
        /// Признак: отправлял ли уже текущий студент эту анкету.
        /// Если true — клиент показывает «вы уже ответили» вместо формы.
        /// </summary>
        public bool AlreadySubmitted { get; init; }
    }
}