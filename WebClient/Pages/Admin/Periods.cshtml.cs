using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Admin
{
    /// <summary>
    /// O-08 (просмотр) и O-09 (смена статуса) для администратора.
    /// 
    /// На одной странице реализованы оба сценария: список периодов 
    /// с возможностью прямо из таблицы изменить статус через 
    /// выпадающий список и кнопку.
    /// </summary>
    [Authorize(Roles = "3")]
    public class PeriodsModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<PeriodsModel> _logger;
        // Свойства для создания периода
        [BindProperty] public string NewPeriodName { get; set; } = string.Empty;
        [BindProperty] public DateOnly? NewStartDate { get; set; }
        [BindProperty] public DateOnly? NewEndDate { get; set; }

        public PeriodsModel(GatewayApiClient gateway, ILogger<PeriodsModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Список периодов, загруженный из БД.</summary>
        public List<PeriodDto>? Periods { get; private set; }

        /// <summary>Текст ошибки для отображения на странице.</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>Фильтр по статусу (из query string).</summary>
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        /// <summary>
        /// GET: загрузка списка периодов.
        /// </summary>
        public async Task OnGetAsync(CancellationToken ct)
        {
            Periods = await _gateway.GetPeriodsAsync(StatusFilter, ct);
            if (Periods is null)
            {
                ErrorMessage = "Не удалось загрузить список периодов.";
            }
        }

        /// <summary>
        /// POST: обработчик смены статуса.
        /// Вызывается при нажатии кнопки "Сохранить" в строке таблицы.
        /// </summary>
        public async Task<IActionResult> OnPostUpdateStatusAsync(
            Guid periodId, string newStatus, string? comment, CancellationToken ct)
        {
            // Проверка на пустой статус (защита от подмены формы)
            if (string.IsNullOrWhiteSpace(newStatus))
            {
                TempData["ErrorMessage"] = "Не указан новый статус.";
                return RedirectToPage();
            }

            var (success, message) = await _gateway.UpdatePeriodStatusAsync(periodId, newStatus, comment, ct);

            if (success)
            {
                // TempData переживает редирект. Это стандартный паттерн PRG 
                // (Post-Redirect-Get), защищающий от повторной отправки формы при F5.
                TempData["SuccessMessage"] = $"Статус периода успешно изменён на '{newStatus}'.";
            }
            else
            {
                TempData["ErrorMessage"] = message ?? "Не удалось изменить статус.";
            }

            return RedirectToPage(new { status = StatusFilter });
        }


        /// <summary>
        /// Обработчик создания нового периода.
        /// </summary>
        public async Task<IActionResult> OnPostCreatePeriodAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(NewPeriodName) || !NewStartDate.HasValue || !NewEndDate.HasValue)
            {
                TempData["ErrorMessage"] = "Заполните все поля для создания периода.";
                return RedirectToPage();
            }

            // Вызываем новый метод в GatewayApiClient (нужно его добавить, см. ниже)
            var (success, message) = await _gateway.CreatePeriodAsync(NewPeriodName, NewStartDate.Value, NewEndDate.Value, ct);

            if (success)
                TempData["SuccessMessage"] = "Период создан. Теперь в него можно добавлять анкеты.";
            else
                TempData["ErrorMessage"] = message;

            return RedirectToPage();
        }
    }
}