using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebClient.Services;

namespace WebClient.Pages.Account
{
    /// <summary>
    /// Страница входа.
    /// 
    /// Шаги обработчика POST:
    ///   1) вызвать Gateway /api/auth/login;
    ///   2) разобрать полученный access-токен, вытащить userId и roleId;
    ///   3) положить пару токенов в Redis (TokenStore) → получить sessionId;
    ///   4) установить HttpOnly cookie с claims + sessionId;
    ///   5) редирект на главную.
    /// </summary>
    public class LoginModel : PageModel
    {
        /// <summary>Клиент Gateway (вызов O-01).</summary>
        private readonly GatewayApiClient _gateway;

        /// <summary>Серверное хранилище токенов.</summary>
        private readonly TokenStore _tokenStore;

        /// <summary>Логгер для диагностики.</summary>
        private readonly ILogger<LoginModel> _logger;

        /// <summary>
        /// Конструктор. Все зависимости внедряются через DI.
        /// </summary>
        public LoginModel(GatewayApiClient gateway, TokenStore tokenStore, ILogger<LoginModel> logger)
        {
            _gateway = gateway;
            _tokenStore = tokenStore;
            _logger = logger;
        }

        /// <summary>Логин, введённый пользователем.</summary>
        [BindProperty]
        public string Login { get; set; } = string.Empty;

        /// <summary>Пароль в открытом виде (передаётся один раз в Auth, нигде не сохраняется).</summary>
        [BindProperty]
        public string Password { get; set; } = string.Empty;

        /// <summary>Сообщение об ошибке для отображения на форме.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// GET /Account/Login. Если пользователь уже аутентифицирован — сразу на главную.
        /// </summary>
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToPage("/Index");

            return Page();
        }

        /// <summary>
        /// POST /Account/Login. Основной обработчик входа.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            // 1. Простейшая валидация ввода на клиенте (не полагаемся на HTML required).
            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введите логин и пароль.";
                return Page();
            }

            // 2. Обращение к Auth через Gateway.
            var tokens = await _gateway.LoginAsync(Login, Password, ct);
            if (tokens is null)
            {
                // Не различаем «нет такого логина» и «неверный пароль» — как в Auth.
                ErrorMessage = "Неверный логин или пароль.";
                return Page();
            }

            // 3. Разбор access-токена.
            //    Подпись НЕ проверяем: токен только что получен от Auth напрямую.
            //    Нам нужны только claims sub (userId) и role (roleId).
            JwtSecurityToken jwt;
            try
            {
                jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось разобрать access-токен после логина");
                ErrorMessage = "Ошибка аутентификации. Попробуйте позже.";
                return Page();
            }

            // 4. Извлечение userId. Тип claim'а может быть как "sub", так и полный URI
            //    ClaimTypes.NameIdentifier — проверяем оба варианта для надёжности.
            var userIdStr = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;

            // 5. Извлечение roleId. В Auth он создан через ClaimTypes.Role,
            //    поэтому в JWT он лежит под полным URI. Проверяем и короткую форму "role".
            var roleStr = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type == "role")?.Value;

            if (!Guid.TryParse(userIdStr, out var userId) || !short.TryParse(roleStr, out var roleId))
            {
                _logger.LogError("В access-токене отсутствуют обязательные claims (sub/role). " +
                                 "sub={Sub}, role={Role}", userIdStr, roleStr);
                ErrorMessage = "Ошибка аутентификации. Обратитесь к администратору.";
                return Page();
            }

            // 6. Сохранение пары токенов в Redis. Получаем sessionId —
            //    он и будет единственной «связкой» браузера с токенами.
            var sessionId = await _tokenStore.CreateSessionAsync(userId, tokens.AccessToken, tokens.RefreshToken);
            // 7. Извлекаем ФИО из access-токена (claim "full_name", см. D-15).
            //    Если по какой-то причине его нет (старый токен), fallback — логин.
            var fullName = jwt.Claims.FirstOrDefault(c => c.Type == "full_name")?.Value;
            if (string.IsNullOrWhiteSpace(fullName)) fullName = Login;

            // 8. Установка HttpOnly cookie с claims.
            //    ClaimTypes.Name = ФИО: используется в Layout через @User.Identity.Name.
            //    Отдельный claim "login" — на случай, если понадобится именно логин
            //    (например, для аудита в UI).
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, fullName),
                new(ClaimTypes.Role, roleId.ToString()),
                new("session_id", sessionId),
                new("login", Login)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            _logger.LogInformation("Пользователь {Login} ({FullName}) вошёл. Сессия {SessionId}, роль {RoleId}",
                Login, fullName, sessionId, roleId);

            // 9. Редирект на главную.
            return RedirectToPage("/Index");
        }
    }
}