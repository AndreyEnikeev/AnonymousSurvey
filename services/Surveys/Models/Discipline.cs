namespace Surveys.Models
{
    /// <summary>Учебная дисциплина (таблица disciplines).</summary>
    public class Discipline
    {
        /// <summary>Уникальный идентификатор дисциплины.</summary>
        public Guid Id { get; set; }

        /// <summary>Короткий код дисциплины (например, "CS101").</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Полное название дисциплины.</summary>
        public string Name { get; set; } = string.Empty;
    }
}