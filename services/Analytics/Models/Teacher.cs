namespace Analytics.Models
{
    /// <summary>Профиль преподавателя (таблица teachers).</summary>
    public class Teacher
    {
        /// <summary>Идентификатор профиля преподавателя.</summary>
        public Guid Id { get; set; }

        /// <summary>Идентификатор связанного пользователя.</summary>
        public Guid UserId { get; set; }

        /// <summary>Название кафедры.</summary>
        public string Department { get; set; } = string.Empty;

        /// <summary>Навигационное свойство: связанный пользователь.</summary>
        public User User { get; set; } = null!;
    }
}