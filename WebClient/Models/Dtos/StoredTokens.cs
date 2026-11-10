namespace WebClient.Models.Dtos
{
    /// <summary>
    /// Снимок пары токенов, привязанный к пользователю.
    /// Хранится в Redis сервисом TokenStore (см. Services/TokenStore.cs).
    /// </summary>
    public class StoredTokens
    {
        /// <summary>Идентификатор пользователя, которому принадлежат токены.</summary>
        public Guid UserId { get; init; }

        /// <summary>Текущий access-токен.</summary>
        public string AccessToken { get; init; } = string.Empty;

        /// <summary>Текущий refresh-токен.</summary>
        public string RefreshToken { get; init; } = string.Empty;
    }
}
