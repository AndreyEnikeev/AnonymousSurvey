namespace Surveys.Models
{
    /// <summary>Период анкетирования (таблица periods).</summary>
    public class Period
    {
        /// <summary>Уникальный идентификатор периода.</summary>
        public Guid Id { get; set; }

        /// <summary>Название периода (например, "Осенний семестр 2026").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Дата начала периода.</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>Дата окончания периода.</summary>
        public DateOnly EndDate { get; set; }

        /// <summary>Ссылка на статус периода (period_statuses.id).</summary>
        public byte StatusId { get; set; }

        /// <summary>Ссылка на пользователя, создавшего период (users.id).</summary>
        public Guid CreatedBy { get; set; }

        /// <summary>Навигационное свойство: статус периода.</summary>
        public PeriodStatus Status { get; set; } = null!;
    }
}