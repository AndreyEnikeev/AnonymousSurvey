namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO ответа от POST /api/auth/refresh.
    /// Структура совпадает с LoginResponse — это сделано намеренно,
    /// чтобы клиентский код одинаково обрабатывал оба ответа.
    /// </summary>
    public class RefreshResponse
    {
        /// <summary>Новый access-токен.</summary>
        public string AccessToken { get; init; } = string.Empty;

        /// <summary>Новый refresh-токен (ротация: старый отозван сервером).</summary>
        public string RefreshToken { get; init; } = string.Empty;
    }
}
