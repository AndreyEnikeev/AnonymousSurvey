using StackExchange.Redis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
    /// В случае недоступности Redis используется стратегия Fail-Open
    /// (запросы пропускаются без ограничений), чтобы не ронять систему.
    /// </summary>
    public class RedisRateLimitMiddleware
    {
        /// <summary>Следующий middleware в конвейере.</summary>
        private readonly RequestDelegate _next;

        /// <summary>Подключение к Redis (внедряется через DI).</summary>
        private readonly IConnectionMultiplexer _redis;

        /// <summary>Логгер для записи ошибок подключения к Redis.</summary>
        private readonly ILogger<RedisRateLimitMiddleware> _logger;

        /// <summary>Лимиты по префиксам путей.</summary>
        private static readonly (string Prefix, int Limit)[] Limits =
        {
            ("/api/auth/login", 10),           // защита от брутфорса пароля
            ("/api/auth/password-reset", 5),   // защита от массовой рассылки писем
            ("/api/audit", 60),                // просмотр журнала — щедрее
        };

        /// <summary>Лимит по умолчанию для всех остальных путей.</summary>
        private const int DefaultLimit = 60;

        /// <summary>Длина окна (в секундах).</summary>
        private const int WindowSeconds = 60;

        /// <summary>
        /// Конструктор middleware.
        /// </summary>
        public RedisRateLimitMiddleware(
            RequestDelegate next,
            IConnectionMultiplexer redis,
            ILogger<RedisRateLimitMiddleware> logger)
        {
            _next = next;
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Основная логика: определить лимит для пути, инкрементировать счётчик
        /// в Redis, при превышении — вернуть 429, иначе пропустить запрос дальше.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // 1) Ключ партиции:
            //    - аутентифицирован -> userId (у каждого свой лимит)
            //    - анонимный -> IP + хеш(User-Agent) — разные браузеры = разные ключи
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
                    SHA256.HashData(Encoding.UTF8.GetBytes(userAgent)))[..16];
                partitionKey = $"anon:{ip}:{uaHash}";
            }

            // 2) Определяем лимит для текущего пути.
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
            var minuteBucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var redisKey = $"rate:{partitionKey}:{minuteBucket}";

            // 4) Атомарный инкремент счётчика с защитой от падения (Fail-Open).
            try
            {
                var db = _redis.GetDatabase();
                var count = await db.StringIncrementAsync(redisKey);

                // 5) Первое обращение в окне — ставим TTL.
                if (count == 1)
                    await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(WindowSeconds * 2));

                // 6) Отдаём клиенту информацию о лимите.
                context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - (int)count).ToString();

                // 7) Превышение лимита -> 429 Too Many Requests.
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
            }
            catch (Exception ex)
            {
                // Если Redis недоступен, логируем ошибку и пропускаем запрос дальше.
                // Это предотвращает падение всего API Gateway при сбоях кэша.
                _logger.LogWarning(ex, "Redis недоступен для Rate Limit. Запрос пропущен без ограничений (Fail-Open).");
            }

            // 8) Передаём запрос следующему middleware.
            await _next(context);
        }
    }
}