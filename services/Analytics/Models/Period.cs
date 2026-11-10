namespace Analytics.Models
{
    /// <summary>Период анкетирования (таблица periods).</summary>
    public class Period
    {
        /// <summary>Уникальный идентификатор периода.</summary>
        public Guid Id { get; set; }

        /// <summary>Название периода (например, "Осень 2026").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Дата начала периода.</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>Дата окончания периода.</summary>
        public DateOnly EndDate { get; set; }

        /// <summary>Идентификатор статуса периода.</summary>
        public short StatusId { get; set; }
    }
}