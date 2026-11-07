namespace Auth.Models
{
    /// <summary>
    /// Модель данных (DTO) для подтверждения сброса пароля.
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Одноразовый токен, который был сгенерирован при запросе сброса
        /// </summary>
        public string Token { get; init; } = string.Empty;

        /// <summary>
        /// Новый пароль в открытом виде, который пользователь хочет установить.
        /// Перед сохранением в БД он будет захеширован с помощью Argon2id.
        /// </summary>
        public string NewPassword { get; init; } = string.Empty;
    }
}