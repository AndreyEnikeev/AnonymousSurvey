using StackExchange.Redis;
using System.Text.Json;
using WebClient.Models;
using WebClient.Models.Dtos;

namespace WebClient.Services
{
    /// <summary>
    /// Серверное хранилище пары JWT-токенов, привязанное к идентификатору сессии.
    /// 
    /// Зачем это нужно (BFF-паттерн):
    ///   - браузер не должен видеть access/refresh-токены (защита от XSS);
    ///   - в браузер уходит только непрозрачный sessionId в HttpOnly cookie;
    ///   - настоящие токены лежат в Redis, доступном только серверу WebClient.
    /// 
    /// Ключ в Redis: "web:session:{sessionId}", значение: JSON от StoredTokens.
    /// Время жизни записи — 8 часов (совпадает с ExpireTimeSpan cookie).
    /// </summary>
    public class TokenStore
    {
        /// <summary>Подключение к Redis (одно на приложение, зарегистрировано как Singleton).</summary>
        private readonly IConnectionMultiplexer _redis;

        /// <summary>Логгер для диагностики.</summary>
        private readonly ILogger<TokenStore> _logger;

        /// <summary>Префикс ключей. Отделяет пространство WebClient от ключей Auth (refresh:*).</summary>
        private const string KeyPrefix = "web:session:";

        /// <summary>TTL сессии. Совпадает с ExpireTimeSpan cookie — чтобы Redis не держал мусор.</summary>
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

        /// <summary>
        /// Конструктор. Зависимости внедряются через DI.
        /// </summary>
        public TokenStore(IConnectionMultiplexer redis, ILogger<TokenStore> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Создаёт новую сессию и сохраняет пару токенов в Redis.
        /// Возвращает сгенерированный sessionId, который вызывающий код
        /// должен положить в HttpOnly cookie.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя (из ответа Auth).</param>
        /// <param name="accessToken">Access-токен.</param>
        /// <param name="refreshToken">Refresh-токен.</param>
        /// <returns>Строка sessionId (GUID без дефисов).</returns>
        public async Task<string> CreateSessionAsync(Guid userId, string accessToken, string refreshToken)
        {
            // sessionId — случайный идентификатор. Без дефисов — чтобы не ломать cookie.
            var sessionId = Guid.NewGuid().ToString("N");

            var payload = new StoredTokens
            {
                UserId = userId,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var json = JsonSerializer.Serialize(payload);
            await _redis.GetDatabase().StringSetAsync(KeyPrefix + sessionId, json, Ttl);

            _logger.LogInformation("Создана сессия {SessionId} для пользователя {UserId}", sessionId, userId);
            return sessionId;
        }

        /// <summary>
        /// Читает пару токенов по sessionId. Возвращает null, если записи нет
        /// (истёк TTL, был logout, или sessionId подделан).
        /// </summary>
        public async Task<StoredTokens?> GetAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            var value = await _redis.GetDatabase().StringGetAsync(KeyPrefix + sessionId);
            if (!value.HasValue)
                return null;

            try
            {
                return JsonSerializer.Deserialize<StoredTokens>(value.ToString());
            }
            catch (JsonException ex)
            {
                // Битые данные в Redis — не должны валить запрос, но это подозрительно.
                _logger.LogWarning(ex, "Не удалось разобрать токены для сессии {SessionId}", sessionId);
                return null;
            }
        }

        /// <summary>
        /// Обновляет токены в существующей сессии. Используется после успешного refresh.
        /// Если записи уже нет (например, TTL истёк между запросами) — создаёт заново.
        /// </summary>
        public async Task UpdateAsync(string sessionId, string accessToken, string refreshToken)
        {
            var existing = await GetAsync(sessionId);
            if (existing is null)
            {
                _logger.LogWarning("Попытка обновить несуществующую сессию {SessionId}", sessionId);
                return;
            }

            var payload = new StoredTokens
            {
                UserId = existing.UserId,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var json = JsonSerializer.Serialize(payload);
            // Ключ сохраняется — продлеваем TTL до исходных 8 часов.
            await _redis.GetDatabase().StringSetAsync(KeyPrefix + sessionId, json, Ttl);
        }

        /// <summary>
        /// Удаляет сессию. Вызывается при logout.
        /// </summary>
        public async Task DeleteAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            await _redis.GetDatabase().KeyDeleteAsync(KeyPrefix + sessionId);
        }

        /// <summary>
        /// Проверяет, есть ли сессия в Redis. Дешевле, чем GetAsync:
        /// не вычитывает JSON, а только проверяет существование ключа.
        /// Используется middleware проверки сессии в Program.cs.
        /// </summary>
        public async Task<bool> ExistsAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return false;

            return await _redis.GetDatabase().KeyExistsAsync(KeyPrefix + sessionId);
        }
    }
}