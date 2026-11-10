using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Admin
{
    /// <summary>
    /// O-10: Выгрузка сводного отчета в Excel.
    /// 
    /// Сначала администратор выбирает период из списка.
    /// Затем нажимает кнопку, происходит POST-запрос к Analytics,
    /// который генерирует файл, кладет его в MinIO и возвращает
    /// pre-signed ссылку (DownloadUrl).
    /// WebClient показывает ссылку пользователю.
    /// </summary>
    [Authorize(Roles = "3")]
    public class ExportModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<ExportModel> _logger;

        public ExportModel(GatewayApiClient gateway, ILogger<ExportModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Список периодов для выпадающего списка.</summary>
        public List<PeriodDto>? Periods { get; private set; }

        /// <summary>Результат выгрузки (ссылка на файл), если POST прошел успешно.</summary>
        public ExportResponseDto? ExportResult { get; private set; }

        /// <summary>Сообщение об ошибке.</summary>
        public string? ErrorMessage { get; private set; }

        [BindProperty]
        public Guid SelectedPeriodId { get; set; }

        /// <summary>Ключ идемпотентности (генерируется при открытии страницы).</summary>
        [BindProperty]
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>
        /// GET: загружаем список периодов, чтобы админ мог выбрать.
        /// </summary>
        public async Task OnGetAsync(CancellationToken ct)
        {
            // Генерируем ключ при каждом открытии страницы
            IdempotencyKey = Guid.NewGuid().ToString();

            // Загружаем все периоды (можно отфильтровать только closed, если нужно)
            Periods = await _gateway.GetPeriodsAsync(ct: ct);
            if (Periods is null)
            {
                ErrorMessage = "Не удалось загрузить список периодов.";
            }
        }

        /// <summary>
        /// POST: запрос на выгрузку.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (SelectedPeriodId == Guid.Empty)
            {
                ErrorMessage = "Выберите период.";
                Periods = await _gateway.GetPeriodsAsync(ct: ct);
                return Page();
            }

            var (success, result, message) = await _gateway.ExportReportAsync(SelectedPeriodId, IdempotencyKey, ct);

            if (success && result != null)
            {
                ExportResult = result;
                // Не делаем редирект, чтобы пользователь сразу увидел кнопку скачивания.
                // Ключ идемпотентности оставляем тем же (на случай, если он нажмет еще раз).
            }
            else
            {
                ErrorMessage = message ?? "Ошибка при формировании отчета.";
                // Перезагружаем периоды, если была ошибка
                Periods = await _gateway.GetPeriodsAsync(ct: ct);
            }

            return Page();
        }
    }
}