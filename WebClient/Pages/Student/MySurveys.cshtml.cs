using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Student
{
    /// <summary>
    /// O-06: история отправленных анкет.
    /// 
    /// Отображает только факты отправки (название анкеты и период),
    /// без содержания ответов. Это архитектурное следствие анонимности:
    /// в БД нет связи между студентом и его бюллетенем.
    /// </summary>
    [Authorize(Roles = "1")]
    public class MySurveysModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<MySurveysModel> _logger;

        /// <summary>Конструктор с DI.</summary>
        public MySurveysModel(GatewayApiClient gateway, ILogger<MySurveysModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Загруженный список отправленных анкет. null — если запрос не удался.</summary>
        public List<SurveyListItemDto>? Surveys { get; private set; }

        /// <summary>Текст ошибки для отображения над таблицей.</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// GET: загружает историю отправленных анкет через Gateway.
        /// </summary>
        public async Task OnGetAsync(CancellationToken ct)
        {
            Surveys = await _gateway.GetMySurveysAsync(ct);

            if (Surveys is null)
            {
                ErrorMessage = "Не удалось загрузить историю. Попробуйте обновить страницу.";
                _logger.LogWarning("GetMySurveys вернул null в MySurveysModel.OnGetAsync");
            }
        }
    }
}