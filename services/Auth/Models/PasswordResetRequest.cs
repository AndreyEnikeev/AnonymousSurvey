namespace Auth.Models
{
    /// <summary>
    /// Модель данных (DTO) для запроса на восстановление пароля (O-02).
    /// Содержит только идентификатор пользователя (логин или email),
    /// </summary>
    public class PasswordResetRequest
    {
        /// <summary>
        /// Логин или адрес электронной почты пользователя, 
        /// который хочет восстановить доступ к системе.
        /// </summary>
        public string LoginOrEmail { get; init; } = string.Empty;
    }
}