namespace Surveys.Models
{
    /// <summary>
    /// Справочник статусов шаблона анкеты (таблица survey_statuses).
    /// Значения: draft / active / closed.
    /// </summary>
    public class SurveyStatus
    {
        /// <summary>Идентификатор статуса (1=draft, 2=active, 3=closed).</summary>
        public byte Id { get; set; }

        /// <summary>Строковый код статуса.</summary>
        public string Code { get; set; } = string.Empty;
    }
}