using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Teacher
{
    /// <summary>
    /// Страница агрегированного отчёта (O-07) для преподавателя.
    ///
    /// Механика получения данных:
    ///   - клиент делает запрос к Gateway без параметров (без teacherId);
    ///   - сервис Analytics на бэкенде сам извлекает userId из JWT,
    ///     находит преподавателя и принудительно фильтрует отчёт по нему.
    ///
    /// Это защита на уровне объекта: даже если преподаватель подделает
    /// HTTP-запрос (например, через curl), добавив ?teacherId=<чужой_id>,
    /// сервер проигнорирует параметр и вернёт только его собственные данные.
    /// </summary>
    [Authorize(Roles = "2")]
    public class ReportsModel : PageModel
    {
        /// <summary>
        /// HTTP-клиент к Gateway. Используется для вызова GET /api/reports/aggregated.
        /// </summary>
        private readonly GatewayApiClient _gateway;

        /// <summary>Логгер для диагностики ошибок загрузки.</summary>
        private readonly ILogger<ReportsModel> _logger;

        /// <summary>
        /// Конструктор. ASP.NET Core внедряет зависимости через DI.
        /// </summary>
        /// <param name="gateway">Клиент Gateway.</param>
        /// <param name="logger">Логгер.</param>
        public ReportsModel(GatewayApiClient gateway, ILogger<ReportsModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>
        /// Загруженный отчёт. null — если запрос не удался (сеть, 4xx, 5xx).
        /// Инициализируется в OnGetAsync.
        /// </summary>
        public AggregatedReportResponseDto? Report { get; private set; }

        /// <summary>
        /// Сообщение об ошибке для отображения над таблицей.
        /// Пустое, если загрузка прошла успешно (даже если Items пустой).
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Обработчик GET. Загружает агрегированный отчёт.
        ///
        /// Почему не передаём параметры:
        ///   - преподаватель должен видеть только свои дисциплины;
        ///   - сервер сам определяет, кто спрашивает, по JWT (см. Analytics.ReportsController);
        ///   - передача teacherId с клиента открыла бы возможность подмены.
        ///     Сервер её всё равно отбросит, но лучше не давать повода.
        /// </summary>
        public async Task OnGetAsync(CancellationToken ct)
        {
            Report = await _gateway.GetAggregatedReportAsync(ct);

            if (Report is null)
            {
                // null означает, что Gateway ответил не-200 (после попытки refresh)
                // или произошла сетевая ошибка. Пользователю — нейтральное сообщение.
                ErrorMessage = "Не удалось загрузить отчёт. Попробуйте позже.";
                _logger.LogWarning("GetAggregatedReport вернул null в ReportsModel.OnGetAsync");
            }
        }
    }
}