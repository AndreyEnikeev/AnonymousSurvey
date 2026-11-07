namespace Auth.Models
{
    /// <summary>
    /// Модель данных (DTO) для запроса на выход из системы (Logout).
    public class LogoutRequest
    {
        /// <summary>
        /// Refresh-токен пользователя, который нужно отозвать (удалить из Redis).
        /// Без этого токена сессию завершить невозможно.
        /// </summary>
        public string RefreshToken { get; init; } = string.Empty;
    }
}