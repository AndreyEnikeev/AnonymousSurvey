namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO запроса O-09: изменение статуса периода.
    /// </summary>
    public class UpdatePeriodStatusRequest
    {
        /// <summary>Новый статус: open / closed / archived.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Необязательный комментарий к изменению (для журнала).</summary>
        public string? Comment { get; init; }
    }
}