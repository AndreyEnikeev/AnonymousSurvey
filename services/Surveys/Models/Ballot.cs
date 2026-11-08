using System;

namespace Surveys.Models
{
    /// <summary>
    /// Обезличенный факт отправки анкеты (таблица ballots).
    /// ВАЖНО: здесь намеренно нет student_id! Связь с тем, кто проголосовал,
    /// хранится в таблице voter_checkins, и между ними НЕТ внешнего ключа.
    /// Это главный механизм обеспечения анонимности.
    /// </summary>
    public class Ballot
    {
        /// <summary>Уникальный идентификатор бюллетеня (GUID).</summary>
        public Guid Id { get; set; }

        /// <summary>Ссылка на шаблон анкеты, по которому голосовали (survey_templates.id).</summary>
        public Guid SurveyTemplateId { get; set; }

        /// <summary>Дата отправки анкеты (без времени, чтобы нельзя было сопоставить по секундам).</summary>
        public DateOnly SubmittedDate { get; set; }
    }
}