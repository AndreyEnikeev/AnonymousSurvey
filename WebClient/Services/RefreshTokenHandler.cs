using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebClient.Models;
using WebClient.Models.Dtos;

namespace WebClient.Services
{
    /// <summary>
    /// HTTP-обработчик, который автоматически обновляет access-токен при 401.
    ///
    /// Как работает:
    ///   1. До отправки — читает sessionId из cookie-claims и access-токен из Redis;
    ///   2. Ставит заголовок Authorization: Bearer <access>;
    ///   3. Отправляет запрос;
    ///   4. Если 401 — вызывает /api/auth/refresh со своим refresh-токеном;
    ///   5. При успехе — обновляет Redis и ПОВТОРЯЕТ исходный запрос с новым access.
    ///
    /// Пользователь ничего не замечает: если refresh жив, UI работает непрерывно.
    /// На логин выкидывает только когда refresh тоже протух или отозван.
    ///
    /// ВАЖНО: refresh-запрос идёт через клиент "GatewayClientRaw" — БЕЗ этого handler'а.
    /// Иначе при 401 от refresh была бы бесконечная рекурсия.
    /// </summary>
    public class RefreshTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TokenStore _tokenStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RefreshTokenHandler> _logger;

        /// <summary>Ключ в HttpContext.Items — флаг «это retry после refresh».</summary>
        private const string RetryFlagKey = "__refresh_retry";

        /// <summary>Конструктор с DI.</summary>
        public RefreshTokenHandler(
            IHttpContextAccessor httpContextAccessor,
            TokenStore tokenStore,
            IHttpClientFactory httpClientFactory,
            ILogger<RefreshTokenHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenStore = tokenStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            // 1. Достать sessionId из claims текущего пользователя.
            //    Если null — запрос анонимный (логин, forgot-password).
            //    Такие запросы идут без Authorization и без авто-refresh.
            var sessionId = _httpContextAccessor.HttpContext?.User
                .FindFirst("session_id")?.Value;

            if (string.IsNullOrEmpty(sessionId))
                return await base.SendAsync(request, ct);

            // 2. Достать токены из Redis. Если сессии нет —
            //    middleware в Program.cs разлогинит пользователя ещё до сюда.
            //    Но на всякий случай — отправим как есть, получим 401.
            var tokens = await _tokenStore.GetAsync(sessionId);
            if (tokens is null)
                return await base.SendAsync(request, ct);

            // 3. Поставить актуальный access-токен.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            // 4. Буферизировать тело — иначе после SendAsync его нельзя
            //    прочитать повторно (stream будет исчерпан). Нужно на случай 401+retry.
            byte[]? contentBytes = null;
            string? contentType = null;
            if (request.Content is not null)
            {
                contentBytes = await request.Content.ReadAsByteArrayAsync(ct);
                contentType = request.Content.Headers.ContentType?.ToString();
            }

            // 5. Отправить.
            var response = await base.SendAsync(request, ct);

            // 6. Не 401 — выходим. Или это уже retry — второй раз не пытаемся.
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            var isRetry = _httpContextAccessor.HttpContext?.Items[RetryFlagKey] as bool? == true;
            if (isRetry)
            {
                _logger.LogWarning("Повторный 401 после refresh — refresh-токен недействителен");
                return response;
            }

            // 7. Пробуем refresh через RAW-клиент (без этого handler'а — иначе рекурсия).
            _logger.LogInformation("Access-токен истёк (sessionId={SessionId}), пробуем refresh", sessionId);

            var rawClient = _httpClientFactory.CreateClient("GatewayClientRaw");
            var refreshResponse = await rawClient.PostAsJsonAsync(
                "api/auth/refresh",
                new { refreshToken = tokens.RefreshToken },
                ct);

            if (!refreshResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh вернул {StatusCode} — сессия недействительна",
                    (int)refreshResponse.StatusCode);
                return response; // отдаём исходный 401 клиенту
            }

            var newTokens = await refreshResponse.Content
                .ReadFromJsonAsync<RefreshResponse>(cancellationToken: ct);

            if (newTokens is null || string.IsNullOrEmpty(newTokens.AccessToken))
            {
                _logger.LogError("Refresh вернул 200 без accessToken — это баг");
                return response;
            }

            // 8. Обновить Redis: новый access, новый refresh (ротация — старый отозван Auth'ом).
            await _tokenStore.UpdateAsync(sessionId, newTokens.AccessToken, newTokens.RefreshToken);

            // 9. Повторить исходный запрос с новым access-токеном.
            //    Создаём НОВЫЙ HttpRequestMessage — старый уже отправлен.
            var retryRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            if (contentBytes is not null)
            {
                retryRequest.Content = new ByteArrayContent(contentBytes);
                if (contentType is not null)
                    retryRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
            foreach (var h in request.Headers)
            {
                if (h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) continue;
                retryRequest.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);

            // 10. Установить флаг retry — если снова 401, второго refresh не будет.
            // ВАЖНО: через ?. нельзя присваивать. Нужна явная проверка на null.
            var httpCtx = _httpContextAccessor.HttpContext;
            if (httpCtx is not null)
            {
                httpCtx.Items[RetryFlagKey] = true;
            }

            _logger.LogInformation("Refresh успешен, повторяем исходный запрос");
            return await base.SendAsync(retryRequest, ct);
        }
    }
}