namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO для списка анкет (используется в O-04 и O-06).
    /// Содержит только сводку, без самих вопросов и ответов.
    /// </summary>
    public class SurveyListItemDto
    {
        /// <summary>Идентификатор анкеты.</summary>
        public Guid SurveyId { get; init; }

        /// <summary>Название анкеты (Дисциплина — Преподаватель).</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Отправлял ли текущий студент эту анкету.</summary>
        public bool AlreadySubmitted { get; init; }

        /// <summary>
        /// Название периода анкетирования (например, "Осень 2026").
        /// Расширение контракта O-04/O-06 для удобства отображения (D-17).
        /// </summary>
        public string PeriodName { get; init; } = string.Empty;

    }
}