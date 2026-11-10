namespace Analytics.Models
{
    /// <summary>Реестр выгрузок (таблица exports).</summary>
    public class Export
    {
        /// <summary>Уникальный идентификатор выгрузки.</summary>
        public Guid Id { get; set; }

        /// <summary>Идентификатор создателя.</summary>
        public Guid CreatedBy { get; set; }

        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; set; }

        /// <summary>Идентификатор шаблона анкеты (опционально).</summary>
        public Guid? SurveyTemplateId { get; set; }

        /// <summary>Идентификатор формата (1 - csv).</summary>
        public short FormatId { get; set; }

        /// <summary>Идентификатор статуса (2 - ready).</summary>
        public short StatusId { get; set; }

        /// <summary>Ключ идемпотентности.</summary>
        public Guid IdempotencyKey { get; set; }

        /// <summary>Ключ объекта в MinIO.</summary>
        public string ObjectKey { get; set; } = string.Empty;

        /// <summary>Время истечения ссылки.</summary>
        public DateTime ExpiresAt { get; set; }
    }
}