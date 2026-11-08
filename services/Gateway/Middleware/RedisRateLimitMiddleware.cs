using StackExchange.Redis;
using System.Security.Claims;

namespace Gateway.Middleware
{
    /// <summary>
    /// Распределённый Rate Limiting через Redis.
    /// Ключ партиции — идентификатор пользователя из JWT (а не IP!),
    /// чтобы за NAT или в корпоративной сети разные пользователи
    /// не «съедали» лимит друг друга.
    ///
    /// Алгоритм: фиксированное окно 1 минута.
    /// Каждая минута — новый ключ в Redis; старые ключи удаляются по TTL.
    /// </summary>
    public class RedisRateLimitMiddleware
    {
        /// <summary>
        /// Следующий middleware в конвейере.
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// Подключение к Redis (внедряется через DI).
        /// </summary>
        private readonly IConnectionMultiplexer _redis;

        /// <summary>
        /// Лимиты по префиксам путей.
        /// </summary>
        private static readonly (string Prefix, int Limit)[] Limits =
        {
            ("/api/auth/login", 10),           // защита от брутфорса пароля
            ("/api/auth/password-reset", 5),   // защита от массовой рассылки писем
            ("/api/audit", 60),                // просмотр журнала — щедрее
        };

        /// <summary>
        /// Лимит по умолчанию для всех остальных путей.
        /// </summary>
        private const int DefaultLimit = 60;

        /// <summary>
        /// Длина окна (в секундах).
        /// </summary>
        private const int WindowSeconds = 60;

        public RedisRateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _redis = redis;
        }

        /// <summary>
        /// Основная логика: определить лимит для пути, инкрементировать счётчик
        /// в Redis, при превышении — вернуть 429, иначе пропустить запрос дальше.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // 1) Ключ партиции: userId из токена, если аутентифицирован,
            //    иначе IP (для анонимных запросов к /api/auth/login).
            //    NameClaimType настроен в Program.cs как ClaimTypes.NameIdentifier,
            //    но оставляем fallback на "sub" на случай изменения настроек.// Ключ партиции:
            // - аутентифицирован → userId (у каждого свой лимит)
            // - анонимный → IP + хеш(User-Agent) — разные браузеры = разные ключи
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            string partitionKey;
            if (userId != null)
            {
                partitionKey = $"user:{userId}";
            }
            else
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = context.Request.Headers.UserAgent.ToString();
                // Короткий хеш UA, чтобы ключ не был гигантским
                var uaHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(userAgent)))[..16];
                partitionKey = $"anon:{ip}:{uaHash}";
            }

            // 2) Определяем лимит для текущего пути.
            //    Идём по списку Limits, берём первый подходящий префикс.
            //    Если ни один не подошёл — DefaultLimit.
            var path = context.Request.Path.Value ?? "/";
            var limit = DefaultLimit;
            foreach (var (prefix, value) in Limits)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    limit = value;
                    break;
                }
            }

            // 3) Ключ Redis вида "rate:user:<id>:<yyyyMMddHHmm>".
            //    Смена минуты = новый ключ, старое окно «сбрасывается» само.
            var minuteBucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var redisKey = $"rate:user:{partitionKey}:{minuteBucket}";

            // 4) Атомарный инкремент счётчика. StringIncrementAsync
            //    выполняет INCR в Redis — гонок между параллельными запросами нет.
            var db = _redis.GetDatabase();
            var count = await db.StringIncrementAsync(redisKey);

            // 5) Первое обращение в окне — ставим TTL, чтобы ключ
            //    автоматически удалился через 2 минуты (после окна он не нужен).
            if (count == 1)
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(WindowSeconds * 2));

            // 6) Отдаём клиенту информацию о лимите (полезно для отладки и UI).
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] =
                Math.Max(0, limit - (int)count).ToString();

            // 7) Превышение лимита → 429 Too Many Requests.
            //    Тело ответа нейтральное: не раскрываем деталей.
            if (count > limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Превышено ограничение частоты запросов. Повторите позже."
                });
                return;
            }

            // 8) Лимит не превышен — передаём запрос следующему middleware.
            await _next(context);
        }
    }
}