using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using WebClient.Services;

namespace WebClient.Pages.Account
{
    /// <summary>
    /// Выход из системы.
    /// 
    /// Порядок действий (важен именно такой):
    ///   1) достать sessionId из cookie-claims;
    ///   2) найти refresh-токен в Redis и отозвать его на сервере Auth (O-14);
    ///   3) удалить сессию из Redis WebClient;
    ///   4) удалить cookie;
    ///   5) на страницу логина.
    /// 
    /// Если сеть до Gateway недоступна (logout на сервере не прошёл),
    /// WebClient всё равно удалит свою сессию и cookie: refresh истечёт по TTL.
    /// Это сознательный fail-open только для logout, не для login.
    /// </summary>
    public class LogoutModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly TokenStore _tokenStore;
        private readonly ILogger<LogoutModel> _logger;

        /// <summary>Конструктор с DI.</summary>
        public LogoutModel(GatewayApiClient gateway, TokenStore tokenStore, ILogger<LogoutModel> logger)
        {
            _gateway = gateway;
            _tokenStore = tokenStore;
            _logger = logger;
        }

        /// <summary>
        /// GET /Account/Logout. Логаут через GET делать нельзя (CSRF), поэтому —
        /// только редирект на главную. Реальный выход — POST из формы в layout.
        /// </summary>
        public IActionResult OnGet() => RedirectToPage("/Index");

        /// <summary>
        /// POST /Account/Logout. Выполняет выход.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            // 1. sessionId лежит в claims аутентифицированного пользователя.
            var sessionId = User.FindFirst("session_id")?.Value;

            if (!string.IsNullOrEmpty(sessionId))
            {
                // 2. Найти refresh-токен, чтобы корректно отозвать его на сервере Auth.
                var tokens = await _tokenStore.GetAsync(sessionId);
                if (tokens is not null)
                {
                    await _gateway.LogoutAsync(tokens.RefreshToken, ct);
                }
                else
                {
                    _logger.LogWarning("Сессия {SessionId} не найдена в Redis при logout", sessionId);
                }

                // 3. Удалить сессию из Redis WebClient.
                await _tokenStore.DeleteAsync(sessionId);
            }

            // 4. Удалить cookie аутентификации.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("Пользователь {Login} вышел", User.Identity?.Name ?? "unknown");

            // 5. На страницу логина.
            return RedirectToPage("/Account/Login");
        }
    }
}