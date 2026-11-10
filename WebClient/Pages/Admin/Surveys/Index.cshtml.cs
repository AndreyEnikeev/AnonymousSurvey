using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Admin.Surveys
{
    /// <summary>
    /// Страница управления шаблонами анкет (CRUD).
    /// Доступна только администратору (роль 3).
    /// </summary>
    [Authorize(Roles = "3")]
    public class IndexModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(GatewayApiClient gateway, ILogger<IndexModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Список анкет для отображения.</summary>
        public List<AdminSurveyListDto>? Surveys { get; private set; }

        /// <summary>Сообщение об ошибке загрузки.</summary>
        public string? ErrorMessage { get; private set; }

        public async Task OnGetAsync(CancellationToken ct)
        {
            Surveys = await _gateway.GetAdminSurveysAsync(ct);
            if (Surveys is null)
            {
                ErrorMessage = "Не удалось загрузить список анкет.";
                _logger.LogWarning("GetAdminSurveys вернул null в IndexModel");
            }
        }
        /// <summary>
        /// Обработчик публикации анкеты (перевод из draft в active).
        /// </summary>
        public async Task<IActionResult> OnPostPublishAsync(Guid id, CancellationToken ct)
        {
            // 2 - это ID статуса "active" в таблице survey_statuses
            var (success, msg) = await _gateway.ChangeSurveyStatusAsync(id, 2, ct);

            if (success)
                TempData["SuccessMessage"] = "Анкета опубликована! Теперь студенты могут её видеть (если период открыт).";
            else
                TempData["ErrorMessage"] = msg;

            return RedirectToPage();
        }
    }
}