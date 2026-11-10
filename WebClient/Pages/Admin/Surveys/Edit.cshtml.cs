using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebClient.Models.Dtos;
using WebClient.Services;

namespace WebClient.Pages.Admin.Surveys
{
    /// <summary>
    /// Страница создания и редактирования анкеты.
    /// </summary>
    [Authorize(Roles = "3")]
    public class EditModel : PageModel
    {
        private readonly GatewayApiClient _gateway;
        private readonly ILogger<EditModel> _logger;

        public EditModel(GatewayApiClient gateway, ILogger<EditModel> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        /// <summary>Идентификатор редактируемой анкеты. Если null — создаем новую.</summary>
        [BindProperty(SupportsGet = true)]
        public Guid? Id { get; set; }

        /// <summary>Справочники для выпадающих списков (дисциплины, преподаватели, периоды).</summary>
        public AdminDictionariesDto? Dictionaries { get; private set; }

        /// <summary>Существующие данные анкеты (при редактировании).</summary>
        public AdminSurveyDetailsDto? ExistingSurvey { get; private set; }

        public string? ErrorMessage { get; private set; }

        // Свойства для POST-запроса
        [BindProperty] public Guid PeriodId { get; set; }
        [BindProperty] public Guid DisciplineId { get; set; }
        [BindProperty] public Guid TeacherId { get; set; }

        [BindProperty]
        public List<QuestionInputDto> Questions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            Dictionaries = await _gateway.GetAdminDictionariesAsync(ct);
            if (Dictionaries == null)
            {
                ErrorMessage = "Ошибка загрузки справочников.";
                return Page();
            }

            if (Id.HasValue)
            {
                // Режим редактирования: подгружаем текущую анкету
                ExistingSurvey = await _gateway.GetAdminSurveyAsync(Id.Value, ct);
                if (ExistingSurvey == null)
                {
                    ErrorMessage = "Анкета не найдена.";
                    return Page();
                }

                if (ExistingSurvey.StatusCode != "draft")
                {
                    ErrorMessage = "Редактирование заблокировано. Анкета уже не является черновиком.";
                    return Page();
                }

                // Заполняем поля из существующей анкеты
                PeriodId = ExistingSurvey.PeriodId;
                DisciplineId = ExistingSurvey.DisciplineId;
                TeacherId = ExistingSurvey.TeacherId;
                Questions = ExistingSurvey.Questions.Select(q => new QuestionInputDto
                {
                    Id = q.QuestionId,
                    Text = q.Text,
                    TypeId = q.Type == "scale" ? (short)1 : (short)2
                }).ToList();
            }
            else
            {
                // Режим создания: если есть только один draft-период, выбираем его по умолчанию
                if (Dictionaries.DraftPeriods.Count == 1)
                    PeriodId = Dictionaries.DraftPeriods[0].Id;

                // Добавляем один пустой вопрос по умолчанию
                Questions.Add(new QuestionInputDto { TypeId = 1 });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (PeriodId == Guid.Empty || DisciplineId == Guid.Empty || TeacherId == Guid.Empty)
            {
                ErrorMessage = "Выберите период, дисциплину и преподавателя.";
                return await ReloadPageAsync(ct);
            }

            // Удаляем пустые вопросы, если пользователь добавил строку, но не заполнил
            Questions = Questions.Where(q => !string.IsNullOrWhiteSpace(q.Text)).ToList();

            if (!Questions.Any())
            {
                ErrorMessage = "Анкета должна содержать хотя бы один вопрос.";
                return await ReloadPageAsync(ct);
            }

            var request = new SaveSurveyTemplateRequest
            {
                PeriodId = PeriodId,
                DisciplineId = DisciplineId,
                TeacherId = TeacherId,
                Questions = Questions
            };

            bool success;
            string? msg;

            if (Id.HasValue)
            {
                (success, msg) = await _gateway.UpdateAdminSurveyAsync(Id.Value, request, ct);
            }
            else
            {
                (success, msg) = await _gateway.CreateAdminSurveyAsync(request, ct);
            }

            if (success)
            {
                TempData["SuccessMessage"] = Id.HasValue ? "Анкета обновлена." : "Анкета создана.";
                return RedirectToPage("/Admin/Surveys/Index");
            }

            ErrorMessage = msg ?? "Ошибка сохранения.";
            return await ReloadPageAsync(ct);
        }

        // Вспомогательный метод для перезагрузки справочников при ошибке
        private async Task<IActionResult> ReloadPageAsync(CancellationToken ct)
        {
            Dictionaries = await _gateway.GetAdminDictionariesAsync(ct);
            return Page();
        }
    }
}