using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Student
{
    /// <summary>
    /// O-04: список активных анкет для студента.
    ///
    /// [Authorize(Roles = "1")] — доступ только роли student.
    /// Если зайдёт teacher/admin — cookie-auth перенаправит на /Account/Denied.
    /// Проверка на сервере дублирует UI: в меню ссылки скрыты для не-студентов,
    /// но прямой ввод URL всё равно защищён.
    /// </summary>
    [Authorize(Roles = "1")]
    public class SurveysModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<SurveysModel> _logger;

        /// <summary>Конструктор с DI.</summary>
        public SurveysModel(GatewayApiClient gateway, ILogger<SurveysModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Загруженный список анкет. null — если запрос не удался.</summary>
        public List<SurveyListItemDto>? Surveys { get; private set; }

        /// <summary>Текст ошибки для отображения над таблицей.</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// GET: загружает список активных анкет через Gateway.
        /// </summary>
        public async Task OnGetAsync(CancellationToken ct)
        {
            Surveys = await _gateway.GetActiveSurveysAsync(ct);

            if (Surveys is null)
            {
                // Причина может быть разной (сеть, 401 после неудачного refresh,
                // 500 на сервере). Пользователю показываем нейтрально.
                ErrorMessage = "Не удалось загрузить список анкет. Попробуйте обновить страницу.";
                _logger.LogWarning("GetActiveSurveys вернул null в SurveysModel.OnGetAsync");
            }
        }
    }
}