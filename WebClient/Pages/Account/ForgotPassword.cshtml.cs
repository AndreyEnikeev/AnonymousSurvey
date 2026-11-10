using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Services;

namespace WebClient.Pages.Account
{
    /// <summary>
    /// Запрос на восстановление пароля (O-02, шаг 1).
    /// После успешной отправки показывает нейтральное сообщение —
    /// без раскрытия, существует ли пользователь в системе.
    /// </summary>
    public class ForgotPasswordModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<ForgotPasswordModel> _logger;

        /// <summary>Конструктор с DI.</summary>
        public ForgotPasswordModel(GatewayApiClient gateway, ILogger<ForgotPasswordModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Введённый логин или email.</summary>
        [BindProperty]
        public string LoginOrEmail { get; set; } = string.Empty;

        /// <summary>Признак: форма отправлена (показываем нейтральное сообщение).</summary>
        public bool Submitted { get; set; }

        /// <summary>Если пользователь уже аутентифицирован — ему не нужна эта страница.</summary>
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToPage("/Index");

            return Page();
        }

        /// <summary>POST: отправка запроса в Auth через Gateway.</summary>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(LoginOrEmail))
            {
                Submitted = false;
                return Page();
            }

            // Всегда переходим к Submitted = true — даже если Gateway вернул
            // ошибку сети. Причина: показывать пользователю «сервер упал»
            // на этом шаге бессмысленно и раскрывает устройство. При ошибке
            // администратор увидит её в логах Auth.
            await _gateway.RequestPasswordResetAsync(LoginOrEmail, ct);
            _logger.LogInformation("Запрос восстановления пароля для {LoginOrEmail}", LoginOrEmail);

            Submitted = true;
            // Очищаем модель — чтобы поле не подставилось при повторной отправке.
            LoginOrEmail = string.Empty;
            return Page();
        }
    }
}