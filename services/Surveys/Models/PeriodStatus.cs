namespace Surveys.Models
{
    /// <summary>
    /// Справочник статусов периода анкетирования (таблица period_statuses).
    /// Значения: draft / open / closed / archived.
    /// </summary>
    public class PeriodStatus
    {
        /// <summary>Идентификатор статуса (1=draft, 2=open, 3=closed, 4=archived).</summary>
        public byte Id { get; set; }

        /// <summary>Строковый код статуса.</summary>
        public string Code { get; set; } = string.Empty;
    }
}