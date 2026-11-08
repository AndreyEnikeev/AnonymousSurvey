namespace Surveys.Models
{
    /// <summary>
    /// Минимальная проекция таблицы users.
    /// Surveys читает только login и email — хеши паролей ему не нужны
    /// </summary>
    public class User
    {
        /// <summary>Уникальный идентификатор пользователя (GUID).</summary>
        public Guid Id { get; set; }

        /// <summary>Логин пользователя.</summary>
        public string Login { get; set; } = string.Empty;
    }
}