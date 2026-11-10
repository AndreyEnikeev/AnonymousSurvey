namespace Analytics.Models
{
    /// <summary>
    /// Проекция таблицы users.
    /// Содержит только те поля, которые аналитика имеет право читать.
    /// </summary>
    public class User
    {
        /// <summary>Уникальный идентификатор пользователя.</summary>
        public Guid Id { get; set; }

        /// <summary>Логин пользователя.</summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>ФИО пользователя.</summary>
        public string FullName { get; set; } = string.Empty;
    }
}