namespace Auth.Models
{
    /// <summary>
    /// Модель данных (DTO) для запроса на вход в систему.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Логин пользователя.
        /// </summary>
        public string Login { get; init; } = string.Empty;

        /// <summary>
        /// Пароль в открытом виде (передается по защищенному HTTPS).
        /// </summary>
        public string Password { get; init; } = string.Empty;
    }
}