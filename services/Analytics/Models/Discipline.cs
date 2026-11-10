namespace Analytics.Models
{
    /// <summary>Учебная дисциплина (таблица disciplines).</summary>
    public class Discipline
    {
        /// <summary>Уникальный идентификатор дисциплины.</summary>
        public Guid Id { get; set; }

        /// <summary>Код дисциплины (например, CS101).</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Название дисциплины.</summary>
        public string Name { get; set; } = string.Empty;
    }
}