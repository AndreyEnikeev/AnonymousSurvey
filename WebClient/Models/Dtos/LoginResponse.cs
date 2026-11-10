namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO ответа от POST /api/auth/login.
    /// Совпадает по структуре с ответом Auth (C-03): пара токенов.
    /// </summary>
    public class LoginResponse
    {
        /// <summary>Короткоживущий JWT access-токен (подписывается в Auth).</summary>
        public string AccessToken { get; init; } = string.Empty;

        /// <summary>Долгоживущий refresh-токен (хранится в Redis сервиса Auth).</summary>
        public string RefreshToken { get; init; } = string.Empty;
    }
}
