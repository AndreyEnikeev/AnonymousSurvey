namespace Surveys.Models
{
    /// <summary>
    /// Профиль преподавателя (таблица teachers).
    /// Расширяет запись пользователя подразделением.
    /// </summary>
    public class Teacher
    {
        /// <summary>Уникальный идентификатор профиля преподавателя.</summary>
        public Guid Id { get; set; }

        /// <summary>Ссылка на учётную запись пользователя (users.id).</summary>
        public Guid UserId { get; set; }

        /// <summary>Подразделение (кафедра, факультет).</summary>
        public string Department { get; set; } = string.Empty;

        /// <summary>Навигационное свойство: учётная запись преподавателя.</summary>
        public User User { get; set; } = null!;
    }
}