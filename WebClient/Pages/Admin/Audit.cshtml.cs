using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Admin
{
    /// <summary>
    /// O-12: Журнал событий безопасности.
    /// Доступен ролям admin (3) и tech (4).
    /// 
    /// Реализована фильтрация по типу события и датам, а также пагинация.
    /// </summary>
    [Authorize(Roles = "3,4")]
    public class AuditModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<AuditModel> _logger;

        public AuditModel(GatewayApiClient gateway, ILogger<AuditModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public AuditEventsPage? EventsPage { get; private set; }
        public string? ErrorMessage { get; private set; }

        // Параметры фильтрации (привязываются из query string при GET)
        [BindProperty(SupportsGet = true)]
        public string? EventType { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? From { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? To { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Limit { get; set; } = 20;

        [BindProperty(SupportsGet = true)]
        public int Offset { get; set; } = 0;

        public async Task OnGetAsync(CancellationToken ct)
        {
            // Защита от некорректного ввода
            if (Limit < 1) Limit = 20;
            if (Limit > 100) Limit = 100;
            if (Offset < 0) Offset = 0;

            EventsPage = await _gateway.GetAuditEventsAsync(EventType, From, To, Limit, Offset, ct);

            if (EventsPage is null)
            {
                ErrorMessage = "Не удалось загрузить журнал аудита.";
                _logger.LogWarning("GetAuditEvents вернул null в AuditModel.OnGetAsync");
            }
        }
    }
}