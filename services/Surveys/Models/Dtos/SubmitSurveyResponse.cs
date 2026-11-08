using System;

namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-05: результат успешной отправки анкеты.
    /// </summary>
    public class SubmitSurveyResponse
    {
        /// <summary>Идентификатор созданного обезличенного бюллетеня.</summary>
        public Guid SubmissionId { get; init; }

        /// <summary>Время, когда анкета была принята сервером (UTC).</summary>
        public DateTime SubmittedAt { get; init; }

        /// <summary>Статус обработки.</summary>
        public string Status { get; init; } = string.Empty;
    }
}