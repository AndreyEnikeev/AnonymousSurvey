namespace Analytics.Models
{
    /// <summary>
    /// Обезличенный бюллетень с ответами (таблица ballots).
    /// Содержит идентификатор отправки, но НЕ содержит идентификатор студента.
    /// </summary>
    public class Ballot
    {
        /// <summary>Уникальный идентификатор бюллетеня.</summary>
        public Guid Id { get; set; }

        /// <summary>Идентификатор шаблона анкеты, к которому относится бюллетень.</summary>
        public Guid SurveyTemplateId { get; set; }

        /// <summary>Дата отправки анкеты (без точного времени для защиты от деанонимизации).</summary>
        public DateOnly SubmittedDate { get; set; }
    }
}