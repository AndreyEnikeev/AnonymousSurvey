using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models;
using WebClient.Models.Dtos;

namespace WebClient.Services
{
    /// <summary>
    /// Типизированный HTTP-клиент к Gateway (C-02, порт 8080).
    /// 
    /// Все обращения WebClient к микросервисам идут ТОЛЬКО через Gateway —
    /// прямых вызовов Auth/Surveys/Analytics нет. Это соответствует ЛР2
    /// (Gateway — единственная точка входа).
    /// 
    /// Методы возвращают null при HTTP-ошибке клиента (4xx) или сети.
    /// 5xx пробрасывают HttpRequestException — это серверная проблема, страница её залогирует.
    /// </summary>
    public class GatewayApiClient
    {
        /// <summary>HttpClient с BaseAddress, указывающим на Gateway.</summary>
        private readonly HttpClient _http;

        /// <summary>Логгер для диагностики.</summary>
        private readonly ILogger<GatewayApiClient> _logger;

        /// <summary>
        /// Конструктор. IHttpClientFactory создаёт клиент с настроенным BaseAddress,
        /// таймаутом и заголовком Accept (см. Program.cs).
        /// </summary>
        public GatewayApiClient(IHttpClientFactory factory, ILogger<GatewayApiClient> logger)
        {
            _http = factory.CreateClient("GatewayClient");
            _logger = logger;
        }

        /// <summary>
        /// O-01: вход. При успехе возвращает пару токенов; при 401 — null.
        /// </summary>
        public async Task<LoginResponse?> LoginAsync(string login, string password, CancellationToken ct = default)
        {
            var body = new { login, password };
            var response = await _http.PostAsJsonAsync("api/auth/login", body, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Login для {Login} вернул {StatusCode}", login, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        }

        /// <summary>
        /// D-13: обновление пары токенов. При успехе — новые токены; при 401 — null.
        /// </summary>
        public async Task<RefreshResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            var body = new { refreshToken };
            var response = await _http.PostAsJsonAsync("api/auth/refresh", body, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh вернул {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RefreshResponse>(cancellationToken: ct);
        }

        /// <summary>
        /// O-14: выход. Сервер отзовёт refresh-токен из Redis.
        /// Ошибки сети игнорируются: даже если logout не дошёл, WebClient всё равно
        /// удалит свою сессию, а refresh-токен истечёт по TTL.
        /// </summary>
        public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
        {
            try
            {
                var body = new { refreshToken };
                await _http.PostAsJsonAsync("api/auth/logout", body, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Logout на Gateway не удался (сессия всё равно будет удалена локально)");
            }
        }


        /// <summary>
        /// O-02: запрос восстановления пароля. Возвращает true, если запрос принят
        /// (сервер всегда отвечает 200 — не палим, существует ли пользователь).
        /// </summary>
        /// <param name="loginOrEmail">Логин или email.</param>
        public async Task<bool> RequestPasswordResetAsync(string loginOrEmail, CancellationToken ct = default)
        {
            var body = new { loginOrEmail };
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/password-reset", body, ct);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "RequestPasswordReset на Gateway не удался");
                return false;
            }
        }

        /// <summary>
        /// O-02 (подтверждение): смена пароля по одноразовому токену.
        /// Возвращает кортеж (Success, Message): при ошибке (400) Message содержит
        /// текст от Auth («Токен недействителен или истёк»), при успехе — сообщение об успехе.
        /// </summary>
        /// <param name="token">Токен из ссылки в письме.</param>
        /// <param name="newPassword">Новый пароль в открытом виде (уйдёт по HTTPS в Auth).</param>
        public async Task<(bool Success, string? Message)> ConfirmPasswordResetAsync(
            string token, string newPassword, CancellationToken ct = default)
        {
            var body = new { token, newPassword };
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/password-reset/confirm", body, ct);

                // Пытаемся прочитать message из тела — оно есть в любом ответе Auth
                // (и при 200, и при 400).
                string? message = null;
                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                    if (payload != null && payload.TryGetValue("message", out var m)) message = m;
                }
                catch { /* тело не JSON — не критично, вернём null */ }

                return (response.IsSuccessStatusCode, message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "ConfirmPasswordReset на Gateway не удался");
                return (false, "Сервер недоступен. Попробуйте позже.");
            }
        }

        /// <summary>
        /// O-04: список активных анкет для текущего студента.
        ///
        /// Сервер (Surveys) сам определяет, кто именно спрашивает, по JWT —
        /// заголовок Authorization подставляет RefreshTokenHandler (см. Program.cs).
        /// Возвращает null, если запрос не удался (нет сети, 4xx/5xx после
        /// неудачного refresh). Страница обработает null как «не удалось загрузить».
        /// </summary>
        public async Task<List<SurveyListItemDto>?> GetActiveSurveysAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("api/surveys/active", ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetActiveSurveys вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                // Сервер возвращает массив (даже если он пустой — пустой массив, не null).
                // Поэтому десериализуем в List<T> без null-обёртки.
                return await response.Content.ReadFromJsonAsync<List<SurveyListItemDto>>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetActiveSurveys: сеть недоступна");
                return null;
            }
        }


        /// <summary>
        /// O-13: получение деталей анкеты по ID.
        /// </summary>
        public async Task<SurveyDetailsDto?> GetSurveyAsync(Guid surveyId, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/surveys/{surveyId}", ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetSurvey вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<SurveyDetailsDto>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetSurvey: сеть недоступна");
                return null;
            }
        }

        /// <summary>
        /// O-05: отправка заполненной анкеты.
        /// Возвращает кортеж (Success, Message): при ошибке Message содержит текст от сервера.
        /// </summary>
        public async Task<(bool Success, string? Message)> SubmitSurveyAsync(
            Guid surveyId, SubmitSurveyRequest request, string idempotencyKey, CancellationToken ct = default)
        {
            try
            {
                // Используем HttpRequestMessage, чтобы добавить кастомный заголовок Idempotency-Key
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/surveys/{surveyId}/submit");
                httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _http.SendAsync(httpRequest, ct);

                if (response.IsSuccessStatusCode)
                    return (true, null);

                // Читаем сообщение об ошибке (400, 409 и т.д.)
                string? errorMessage = null;
                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                    if (payload != null && payload.TryGetValue("message", out var m)) errorMessage = m;
                }
                catch { /* тело не JSON */ }

                return (false, errorMessage ?? $"Ошибка сервера ({(int)response.StatusCode})");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "SubmitSurvey: сеть недоступна");
                return (false, "Сервер недоступен. Попробуйте позже.");
            }
        }

        /// <summary>
        /// O-06: список отправленных анкет текущего студента (история).
        ///
        /// Сервер (Surveys) сам определяет, кто именно спрашивает, по JWT.
        /// Возвращает null, если запрос не удался (нет сети, 4xx/5xx).
        /// Страница обработает null как «не удалось загрузить».
        /// </summary>
        public async Task<List<SurveyListItemDto>?> GetMySurveysAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("api/my-surveys", ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetMySurveys вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<List<SurveyListItemDto>>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetMySurveys: сеть недоступна");
                return null;
            }
        }

        /// <summary>
        /// O-07: получение агрегированного отчёта.
        /// Преподаватель видит только свои данные (фильтрация на сервере по JWT).
        /// </summary>
        public async Task<AggregatedReportResponseDto?> GetAggregatedReportAsync(CancellationToken ct = default)
        {
            try
            {
                // Пока делаем запрос без фильтров, чтобы сервер отдал всё,
                // что положено текущему пользователю по его роли.
                var response = await _http.GetAsync("api/reports/aggregated", ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetAggregatedReport вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<AggregatedReportResponseDto>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetAggregatedReport: сеть недоступна");
                return null;
            }
        }

        /// <summary>
        /// O-08: получение списка периодов анкетирования.
        /// </summary>
        /// <param name="statusFilter">Необязательный фильтр по статусу (open, closed...).</param>
        public async Task<List<PeriodDto>?> GetPeriodsAsync(string? statusFilter = null, CancellationToken ct = default)
        {
            try
            {
                var url = "api/admin/periods";
                if (!string.IsNullOrWhiteSpace(statusFilter))
                {
                    url += $"?status={statusFilter}";
                }

                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetPeriods вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<List<PeriodDto>>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetPeriods: сеть недоступна");
                return null;
            }
        }

        /// <summary>
        /// O-09: смена статуса периода.
        /// Возвращает кортеж (Success, Message).
        /// При ошибке Message содержит текст от сервера (например, «Недопустимый переход»).
        /// </summary>
        public async Task<(bool Success, string? Message)> UpdatePeriodStatusAsync(
            Guid periodId, string newStatus, string? comment, CancellationToken ct = default)
        {
            try
            {
                var body = new UpdatePeriodStatusRequest { Status = newStatus, Comment = comment };

                // PATCH-запрос через HttpRequestMessage, так как PatchAsJsonAsync 
                // в некоторых версиях требует дополнительных манипуляций.
                var request = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/periods/{periodId}")
                {
                    Content = JsonContent.Create(body)
                };

                var response = await _http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return (true, null);

                string? errorMessage = null;
                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                    if (payload != null && payload.TryGetValue("message", out var m)) errorMessage = m;
                }
                catch { /* тело не JSON */ }

                return (false, errorMessage ?? $"Ошибка сервера ({(int)response.StatusCode})");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "UpdatePeriodStatus: сеть недоступна");
                return (false, "Сервер недоступен. Попробуйте позже.");
            }
        }

        /// <summary>
        /// O-10: запрос на формирование отчета и получение ссылки на скачивание.
        /// Возвращает кортеж (Success, Result, Message).
        /// </summary>
        public async Task<(bool Success, ExportResponseDto? Result, string? Message)> ExportReportAsync(
            Guid periodId, string idempotencyKey, CancellationToken ct = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/admin/export");

                // Заголовок идемпотентности — защита от двойного нажатия кнопки "Скачать"
                request.Headers.Add("Idempotency-Key", idempotencyKey);

                var body = new ExportRequestDto { PeriodId = periodId, Format = "xlsx" };
                request.Content = JsonContent.Create(body);

                var response = await _http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ExportResponseDto>(cancellationToken: ct);
                    return (true, result, null);
                }

                // Обработка ошибок (404 - нет анкет, 403 - нет прав, 429 - слишком часто)
                string? errorMessage = null;
                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                    if (payload != null && payload.TryGetValue("message", out var m)) errorMessage = m;
                }
                catch { /* тело не JSON */ }

                return (false, null, errorMessage ?? $"Ошибка сервера ({(int)response.StatusCode})");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "ExportReport: сеть недоступна");
                return (false, null, "Сервер недоступен. Попробуйте позже.");
            }
        }


        /// <summary>
        /// O-12: получение журнала событий безопасности.
        /// </summary>
        public async Task<AuditEventsPage?> GetAuditEventsAsync(
            string? eventType, DateTime? from, DateTime? to, int limit, int offset, CancellationToken ct = default)
        {
            try
            {
                var queryParams = new List<string>
        {
            $"limit={limit}",
            $"offset={offset}"
        };

                if (!string.IsNullOrWhiteSpace(eventType))
                    queryParams.Add($"eventType={Uri.EscapeDataString(eventType)}");

                if (from.HasValue)
                    queryParams.Add($"from={from.Value:O}"); // :O - формат ISO 8601

                if (to.HasValue)
                    queryParams.Add($"to={to.Value:O}");

                var url = $"api/audit/events?{string.Join("&", queryParams)}";

                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetAuditEvents вернул {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<AuditEventsPage>(cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "GetAuditEvents: сеть недоступна");
                return null;
            }
        }

        /// <summary>
        /// Получение списка всех шаблонов анкет для админки.
        /// </summary>
        public async Task<List<AdminSurveyListDto>?> GetAdminSurveysAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("api/admin/surveys", ct);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<List<AdminSurveyListDto>>(cancellationToken: ct);
            }
            catch (HttpRequestException ex) { _logger.LogWarning(ex, "GetAdminSurveys failed"); return null; }
        }

        /// <summary>
        /// Получение справочников для выпадающих списков.
        /// </summary>
        public async Task<AdminDictionariesDto?> GetAdminDictionariesAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync("api/admin/surveys/dictionaries", ct);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<AdminDictionariesDto>(cancellationToken: ct);
            }
            catch (HttpRequestException ex) { _logger.LogWarning(ex, "GetAdminDictionaries failed"); return null; }
        }

        /// <summary>
        /// Получение деталей анкеты для редактирования.
        /// </summary>
        public async Task<AdminSurveyDetailsDto?> GetAdminSurveyAsync(Guid id, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/admin/surveys/{id}", ct);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<AdminSurveyDetailsDto>(cancellationToken: ct);
            }
            catch (HttpRequestException ex) { _logger.LogWarning(ex, "GetAdminSurvey failed"); return null; }
        }

        /// <summary>
        /// Создание нового шаблона анкеты.
        /// </summary>
        public async Task<(bool Success, string? Message)> CreateAdminSurveyAsync(SaveSurveyTemplateRequest request, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/admin/surveys", request, ct);
                if (response.IsSuccessStatusCode) return (true, null);

                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                var msg = payload != null && payload.TryGetValue("message", out var m) ? m : "Ошибка создания анкеты";
                return (false, msg);
            }
            catch (HttpRequestException ex) { _logger.LogWarning(ex, "CreateAdminSurvey failed"); return (false, "Сеть недоступна"); }
        }

        /// <summary>
        /// Обновление существующего шаблона анкеты.
        /// </summary>
        public async Task<(bool Success, string? Message)> UpdateAdminSurveyAsync(Guid id, SaveSurveyTemplateRequest request, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.PutAsJsonAsync($"api/admin/surveys/{id}", request, ct);
                if (response.IsSuccessStatusCode) return (true, null);

                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                var msg = payload != null && payload.TryGetValue("message", out var m) ? m : "Ошибка обновления анкеты";
                return (false, msg);
            }
            catch (HttpRequestException ex) { _logger.LogWarning(ex, "UpdateAdminSurvey failed"); return (false, "Сеть недоступна"); }
        }

        public async Task<(bool Success, string? Message)> CreatePeriodAsync(string name, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        {
            try
            {
                var request = new { Name = name, StartDate = startDate, EndDate = endDate };
                var response = await _http.PostAsJsonAsync("api/admin/periods", request, ct);

                if (response.IsSuccessStatusCode) return (true, null);

                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                var msg = payload != null && payload.TryGetValue("message", out var m) ? m : "Ошибка создания";
                return (false, msg);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "CreatePeriod failed");
                return (false, "Сеть недоступна");
            }
        }

        /// <summary>
        /// Смена статуса анкеты (например, публикация: draft -> active).
        /// </summary>
        public async Task<(bool Success, string? Message)> ChangeSurveyStatusAsync(Guid id, short statusId, CancellationToken ct = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/surveys/{id}/status")
                {
                    Content = JsonContent.Create(new { StatusId = statusId })
                };
                var response = await _http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return (true, null);

                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
                var msg = payload != null && payload.TryGetValue("message", out var m) ? m : "Ошибка";
                return (false, msg);
            }
            catch (HttpRequestException) { return (false, "Сеть недоступна"); }
        }
    }
}