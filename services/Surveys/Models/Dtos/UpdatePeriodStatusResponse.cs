using System;

namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-09: результат успешного изменения статуса.
    /// </summary>
    public class UpdatePeriodStatusResponse
    {
        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Установленный статус.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Время изменения в UTC.</summary>
        public DateTime UpdatedAt { get; init; }
    }
}