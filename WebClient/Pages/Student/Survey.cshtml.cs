using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Student
{
    /// <summary>
    /// O-13 (чтение) и O-05 (отправка) для конкретной анкеты.
    /// </summary>
    [Authorize(Roles = "1")]
    public class SurveyModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<SurveyModel> _logger;

        public SurveyModel(GatewayApiClient gateway, ILogger<SurveyModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Данные анкеты (загружаются в OnGetAsync).</summary>
        public SurveyDetailsDto? Survey { get; private set; }

        /// <summary>Текст ошибки, если анкету не удалось загрузить или отправить.</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>Ключ идемпотентности (генерируется при открытии, передаётся при отправке).</summary>
        [BindProperty]
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>
        /// Сюда Razor Pages автоматически сложит ответы из формы
        /// (благодаря именам полей вида Answers[0].QuestionId).
        /// </summary>
        [BindProperty]
        public List<AnswerItem> Answers { get; set; } = new();

        /// <summary>
        /// GET: загрузка анкеты.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
        {
            Survey = await _gateway.GetSurveyAsync(id, ct);

            if (Survey is null)
            {
                ErrorMessage = "Анкета не найдена, не активна или недоступна.";
                return Page();
            }

            // Генерируем ключ идемпотентности только если анкета ещё не отправлена.
            // Если уже отправлена, форма не покажется, и ключ не понадобится.
            if (!Survey.AlreadySubmitted)
            {
                IdempotencyKey = Guid.NewGuid().ToString();
            }

            return Page();
        }

        /// <summary>
        /// POST: отправка заполненной анкеты.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
        {
            // 1. Базовая проверка: если пришёл пустой ключ, что-то пошло не так на клиенте.
            if (string.IsNullOrWhiteSpace(IdempotencyKey))
            {
                // Перезапрашиваем анкету, чтобы отрисовать форму заново
                Survey = await _gateway.GetSurveyAsync(id, ct);
                ErrorMessage = "Ошибка сессии. Пожалуйста, обновите страницу и попробуйте снова.";
                return Page();
            }

            // 2. Собираем запрос. Отфильтровываем пустые ответы, если они не требуются.
            // (В нашем случае сервер сам валидирует, что оценка в диапазоне 1-5, а текст не пустой).
            var request = new SubmitSurveyRequest
            {
                Answers = Answers ?? new List<AnswerItem>()
            };

            // 3. Отправляем через Gateway.
            var (success, message) = await _gateway.SubmitSurveyAsync(id, request, IdempotencyKey, ct);

            if (success)
            {
                // Успех! Можно передать сообщение через TempData,
                // но проще сразу редиректнуть на список с флеш-сообщением.
                TempData["SuccessMessage"] = "Анкета успешно отправлена. Спасибо за участие!";
                return RedirectToPage("/Student/Surveys");
            }

            // 4. Ошибка отправки.
            // Загружаем анкету заново (она могла измениться, если истекло время),
            // чтобы пользователь мог попробовать снова.
            Survey = await _gateway.GetSurveyAsync(id, ct);

            if (Survey?.AlreadySubmitted == true)
            {
                // Если ошибка 409 (конфликт) или сервер вернул, что уже отправлено.
                ErrorMessage = message ?? "Вы уже отправляли эту анкету.";
            }
            else
            {
                // Ошибка валидации (например, не на все вопросы ответили).
                ErrorMessage = message ?? "Не удалось отправить анкету.";
            }

            return Page();
        }
    }
}