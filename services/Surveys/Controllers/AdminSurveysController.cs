using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Surveys.DataDb;
using Surveys.Models;
using Surveys.Models.Dtos;

namespace Surveys.Controllers
{
    /// <summary>
    /// Контроллер для управления шаблонами анкет (CRUD).
    /// 
    /// Является расширением контракта ЛР №2 (см. discrepancies.md, D-19),
    /// так как изначально операции создания анкет не были предусмотрены.
    /// 
    /// Доступ к контроллеру строго ограничен ролью admin (3).
    /// Gateway валидирует JWT, но роль проверяется здесь, в Surveys.
    /// </summary>
    [ApiController]
    [Route("api/admin/surveys")]
    [Authorize(Roles = "3")]
    public class AdminSurveysController : ControllerBase
    {
        /// <summary>Контекст базы данных для работы с анкетами, вопросами и периодами.</summary>
        private readonly AppDbContext _db;

        /// <summary>
        /// Конструктор. Зависимости внедряются через DI.
        /// </summary>
        /// <param name="db">Контекст БД Surveys.</param>
        public AdminSurveysController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получение списка всех шаблонов анкет.
        /// 
        /// Возвращает сводную информацию: период, дисциплину, преподавателя,
        /// статус, количество вопросов и количество уже отправленных ответов.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            // AsNoTracking — только чтение, не держим объекты в контексте.
            // Include — жадная загрузка связанных сущностей (JOIN).
            var surveys = await _db.SurveyTemplates
                .AsNoTracking()
                .Include(s => s.Period)
                .Include(s => s.Discipline)
                .Include(s => s.Teacher).ThenInclude(t => t.User)
                .Include(s => s.Status)
                .OrderByDescending(s => s.Period.StartDate)
                .ThenBy(s => s.Discipline.Name)
                .Select(s => new AdminSurveyListDto
                {
                    SurveyId = s.Id,
                    PeriodName = s.Period.Name,
                    DisciplineName = s.Discipline.Name,
                    TeacherName = s.Teacher.User.FullName,
                    StatusCode = s.Status.Code,
                    // Count() внутри проекции EF Core транслирует в SQL подзапрос COUNT.
                    QuestionsCount = s.Questions.Count(),
                    
                    // Считаем количество проголосовавших. 
                    // Если > 0, то редактировать вопросы уже нельзя.
                    AnswersCount = _db.VoterCheckins.Count(vc => vc.SurveyTemplateId == s.Id && vc.HasVoted)
                })
                .ToListAsync(ct);

            return Ok(surveys);
        }

        /// <summary>
        /// Получение структуры конкретной анкеты для редактирования.
        /// </summary>
        /// <param name="id">Идентификатор шаблона анкеты.</param>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var survey = await _db.SurveyTemplates
                .AsNoTracking()
                .Include(s => s.Status)
                .Include(s => s.Questions).ThenInclude(q => q.Type)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (survey == null)
                return NotFound(new { message = "Анкета не найдена." });

            var dto = new AdminSurveyDetailsDto
            {
                SurveyId = survey.Id,
                PeriodId = survey.PeriodId,
                DisciplineId = survey.DisciplineId,
                TeacherId = survey.TeacherId,
                StatusCode = survey.Status.Code,
                // Сортируем вопросы по ID, чтобы порядок был стабильным.
                Questions = survey.Questions.OrderBy(q => q.Id).Select(q => new QuestionDto
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    Type = q.Type.Code,
                    Options = q.Type.Code == "scale" ? new List<int> { 1, 2, 3, 4, 5 } : null
                }).ToList()
            };

            return Ok(dto);
        }

        /// <summary>
        /// Создание нового шаблона анкеты со списком вопросов.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveSurveyTemplateRequest request, CancellationToken ct)
        {
            // 1. Проверяем период.
            var period = await _db.Periods.Include(p => p.Status).FirstOrDefaultAsync(p => p.Id == request.PeriodId, ct);
            if (period == null)
                return BadRequest(new { message = "Период не найден." });

            // БИЗНЕС-ПРАВИЛО: новые анкеты можно создавать только в черновиках.
            // Если период уже открыт (open) — добавлять туда новые анкеты нельзя, 
            // иначе студенты увидят их в середине процесса анкетирования.
            if (period.Status.Code != "draft")
                return BadRequest(new { message = "Создавать анкеты можно только в периодах со статусом 'draft'." });

            // 2. Создаем шаблон. По умолчанию ему ставится статус draft (1).
            var surveyId = Guid.NewGuid();
            var survey = new SurveyTemplate
            {
                Id = surveyId,
                PeriodId = request.PeriodId,
                DisciplineId = request.DisciplineId,
                TeacherId = request.TeacherId,
                StatusId = 1 // draft
            };
            _db.SurveyTemplates.Add(survey);

            // 3. Добавляем вопросы.
            foreach (var q in request.Questions)
            {
                _db.Questions.Add(new Question
                {
                    SurveyTemplateId = surveyId,
                    Text = q.Text,
                    TypeId = (byte)q.TypeId
                });
            }

            await _db.SaveChangesAsync(ct);
            return Ok(new { surveyId });
        }

        /// <summary>
        /// Редактирование существующего шаблона анкеты.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveSurveyTemplateRequest request, CancellationToken ct)
        {
            // 1. Ищем анкету.
            var survey = await _db.SurveyTemplates
                .Include(s => s.Status)
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (survey == null)
                return NotFound(new { message = "Анкета не найдена." });

            // 2. ПРОВЕРКА СТАТУСА: редактировать можно только черновики.
            // Это реализует требование: "как только анкета уходит в работу, менять нельзя".
            if (survey.Status.Code != "draft")
                return BadRequest(new { message = "Нельзя редактировать анкету, которая уже не является черновиком." });

            // 3. ПРОВЕРКА ОТВЕТОВ: если есть хоть один голос, менять структуру нельзя.
            // Иначе сломается статистика (старые ответы будут ссылаться на несуществующие вопросы).
            var hasAnswers = await _db.VoterCheckins.AnyAsync(vc => vc.SurveyTemplateId == id && vc.HasVoted, ct);
            if (hasAnswers)
                return BadRequest(new { message = "Нельзя менять вопросы, так как на анкету уже есть ответы." });

            // 4. Обновляем связи шаблона.
            survey.DisciplineId = request.DisciplineId;
            survey.TeacherId = request.TeacherId;

            // 5. Синхронизируем вопросы (сложная логика).
            // Находим ID вопросов, которые пришли с фронтенда (они уже существуют).
            var incomingIds = request.Questions.Where(q => q.Id.HasValue).Select(q => q.Id!.Value).ToList();

            // Удаляем вопросы, которых нет в новом списке (пользователь их стер в UI).
            var toDelete = survey.Questions.Where(q => !incomingIds.Contains(q.Id)).ToList();
            _db.Questions.RemoveRange(toDelete);

            foreach (var q in request.Questions)
            {
                if (q.Id.HasValue && q.Id.Value > 0)
                {
                    // Если ID передан — обновляем существующий вопрос.
                    var existing = survey.Questions.FirstOrDefault(x => x.Id == q.Id.Value);
                    if (existing != null)
                    {
                        existing.Text = q.Text;
                        existing.TypeId = (byte)q.TypeId;
                    }
                }
                else
                {
                    // Если ID нет (null или 0) — создаем новый вопрос.
                    survey.Questions.Add(new Question
                    {
                        Text = q.Text,
                        TypeId = (byte)q.TypeId
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
            return Ok();
        }

        /// <summary>
        /// Получение справочников для формы создания/редактирования анкеты.
        /// Возвращает дисциплины, преподавателей и только те периоды, 
        /// которые находятся в статусе draft (черновик).
        /// </summary>
        [HttpGet("dictionaries")]
        public async Task<IActionResult> GetDictionaries(CancellationToken ct)
        {
            var disciplines = await _db.Disciplines
                .AsNoTracking()
                .Select(d => new DictionaryItemDto { Id = d.Id, Name = d.Name })
                .ToListAsync(ct);

            var teachers = await _db.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .Select(t => new DictionaryItemDto { Id = t.Id, Name = t.User.FullName })
                .ToListAsync(ct);

            var draftPeriods = await _db.Periods
                .AsNoTracking()
                .Include(p => p.Status)
                .Where(p => p.Status.Code == "draft")
                .Select(p => new DictionaryItemDto { Id = p.Id, Name = p.Name })
                .ToListAsync(ct);

            // ТЕПЕРЬ ИСПОЛЬЗУЕМ ЧИСТЫЙ EF CORE, БЕЗ SQL!
            var teacherDisciplines = await _db.TeacherDisciplines
                .AsNoTracking()
                .GroupBy(td => td.TeacherId)
                .Select(g => new TeacherDisciplineMapDto
                {
                    TeacherId = g.Key,
                    DisciplineIds = g.Select(x => x.DisciplineId).ToList()
                })
                .ToListAsync(ct);

            return Ok(new AdminDictionariesDto
            {
                Disciplines = disciplines,
                Teachers = teachers,
                DraftPeriods = draftPeriods,
                TeacherDisciplines = teacherDisciplines
            });
        }


        /// <summary>
        /// Смена статуса анкеты (например, публикация из draft в active).
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeSurveyStatusRequest request, CancellationToken ct)
        {
            var survey = await _db.SurveyTemplates
                .Include(s => s.Status)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (survey == null) return NotFound(new { message = "Анкета не найдена." });

            // Проверяем, что такой статус существует (1-draft, 2-active, 3-closed)
            var newStatus = await _db.SurveyStatuses.FindAsync((byte)request.StatusId);
            if (newStatus == null) return BadRequest(new { message = "Неверный статус." });

            // Если пытаемся вернуть в draft из active/closed - запрещаем
            if (request.StatusId == 1 && survey.Status.Code != "draft")
                return BadRequest(new { message = "Нельзя вернуть анкету в черновик." });

            // Если пытаемся опубликовать (active), проверяем, есть ли в ней вопросы
            if (request.StatusId == 2)
            {
                var hasQuestions = await _db.Questions.AnyAsync(q => q.SurveyTemplateId == id, ct);
                if (!hasQuestions)
                    return BadRequest(new { message = "Нельзя опубликовать анкету без вопросов." });
            }

            survey.StatusId = (byte)request.StatusId;
            await _db.SaveChangesAsync(ct);

            return Ok();
        }
    }
}