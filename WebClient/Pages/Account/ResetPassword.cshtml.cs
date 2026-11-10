using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Services;

namespace WebClient.Pages.Account
{
    /// <summary>
    /// Установка нового пароля по одноразовому токену (O-02, шаг 2).
    /// 
    /// Токен приходит в query string (?token=...). Он попадает в модель
    /// при GET, кладётся в hidden-поле формы и отправляется обратно при POST.
    /// 
    /// Сервер (Auth) сам проверяет срок действия и одноразовость токена —
    /// клиент эти проверки не дублирует (они бессмысленны, любой может
    /// подделать форму). Клиентская валидация — только «пароли совпадают».
    /// </summary>
    public class ResetPasswordModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<ResetPasswordModel> _logger;

        /// <summary>Конструктор с DI.</summary>
        public ResetPasswordModel(GatewayApiClient gateway, ILogger<ResetPasswordModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Одноразовый токен из ссылки в письме.</summary>
        [BindProperty]
        public string Token { get; set; } = string.Empty;

        /// <summary>Новый пароль.</summary>
        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>Повтор пароля (только клиентская проверка «пароли совпадают»).</summary>
        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>Признак успеха (после успешного POST).</summary>
        public bool Success { get; set; }

        /// <summary>Сообщение об ошибке (для отображения над формой).</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// GET: читаем токен из query string, кладём в модель.
        /// Если токена нет — сразу показываем ошибку (не рендерим форму).
        /// </summary>
        public IActionResult OnGet([FromQuery] string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ErrorMessage = "Ссылка недействительна: в ней отсутствует токен. " +
                               "Запросите новую ссылку на восстановление пароля.";
                return Page();
            }

            Token = token;
            return Page();
        }

        /// <summary>
        /// POST: проверяем, что пароли совпадают, и отправляем в Auth через Gateway.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            // 1. Простейшая клиентская валидация.
            //    Остальное (срок действия токена, одноразовость) проверяет Auth —
            //    повторять эти проверки на клиенте бессмысленно, клиент не имеет
            //    доступа к БД токенов.
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
            {
                ErrorMessage = "Пароль должен содержать не менее 8 символов.";
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Пароли не совпадают.";
                return Page();
            }

            // 2. Отправка запроса в Auth (через Gateway).
            var (success, message) = await _gateway.ConfirmPasswordResetAsync(Token, NewPassword, ct);

            if (!success)
            {
                // Auth вернул 400 — токен недействителен, истёк или уже использован.
                // Показываем сообщение от Auth (оно нейтральное и не раскрывает лишнего).
                ErrorMessage = message ?? "Токен недействителен или истёк. Запросите новую ссылку.";
                _logger.LogInformation("Сброс пароля не удался: {Message}", message);
                return Page();
            }

            // 3. Успех. Показываем сообщение «пароль изменён, войдите».
            _logger.LogInformation("Пароль успешно изменён по токену");
            Success = true;
            Token = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            return Page();
        }
    }
}