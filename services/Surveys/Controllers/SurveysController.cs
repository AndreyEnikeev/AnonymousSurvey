using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Surveys.Contracts;
using Surveys.DataDb;
using Surveys.Models;
using Surveys.Models.Dtos;
using System.Security.Claims;

namespace Surveys.Controllers
{
    /// <summary>
    /// Эндпоинты сервиса анкетирования (C-04).
    /// Проверка ролей — здесь, в Surveys. Gateway ролей не проверяет.
    /// </summary>
    [ApiController]
    [Route("api/surveys")]
    public class SurveysController : ControllerBase
    {
        /// <summary>Контекст БД для чтения анкет и связанных сущностей.</summary>
        private readonly AppDbContext _db;
        private readonly IConnectionMultiplexer _redis;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>Конструктор: ASP.NET Core внедряет зависимость.</summary>
        public SurveysController(AppDbContext db, IConnectionMultiplexer redis, IPublishEndpoint publishEndpoint)
        {
            _db = db;
            _redis = redis;
            _publishEndpoint = publishEndpoint;
        }

        /// <summary>
        /// O-13: GET /api/surveys/{surveyId} — структура активной анкеты для заполнения.
        /// Доступно только роли student (1).
        /// </summary>
        /// <param name="surveyId">Идентификатор анкеты.</param>
        /// <param name="ct">Токен отмены запроса.</param>
        [HttpGet("{surveyId:guid}")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> GetSurvey(Guid surveyId, CancellationToken ct)
        {
            // 1) Идентификатор студента — из токена, а НЕ из query/body.
            //    Клиент не должен иметь возможности подставить чужой userId.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var studentId))
                return Unauthorized();

            // 2) Загружаем анкету со всеми связанными сущностями.
            //    AsNoTracking — только чтение, EF Core не будет держать объекты в контексте.
            var survey = await _db.SurveyTemplates
                .AsNoTracking()
                .Include(s => s.Period)             // загрузи Period (JOIN periods)
                    .ThenInclude(p => p.Status)     // и его Status (JOIN period_statuses)
                .Include(s => s.Status)             // загрузи Status анкеты (JOIN survey_statuses)
                .Include(s => s.Discipline)         // загрузи Discipline (JOIN disciplines)
                .Include(s => s.Teacher)            // загрузи Teacher (JOIN teachers)
                    .ThenInclude(t => t.User)       // и его User (JOIN users)
                .Include(s => s.Questions)          // загрузи все Questions (JOIN questions)
                    .ThenInclude(q => q.Type)       // и их Type (JOIN question_types)
                .FirstOrDefaultAsync(s => s.Id == surveyId, ct);

            if (survey is null)
                return NotFound(new { message = "Анкета не найдена." });

            // 3) Проверка права на объект: анкета активна, период открыт.
            //    Если нет — 403 (а не 404), чтобы клиент понял: анкета есть, но недоступна.
            if (survey.Status.Code != "active" || survey.Period.Status.Code != "open")
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Анкета не активна или недоступна." });

            // 4) Проверяем, отправлял ли уже этот студент данную анкету.
            //    voter_checkins знает «кто голосовал», но не «что ответил».
            var alreadySubmitted = await _db.VoterCheckins
                .AnyAsync(vc => vc.StudentId == studentId
                             && vc.SurveyTemplateId == surveyId
                             && vc.HasVoted, ct);

            // 5) Формируем DTO. Только поля из контракта O-13, без лишнего.
            var dto = new SurveyDetailsDto
            {
                SurveyId = survey.Id,
                Title = $"{survey.Discipline.Name} — {survey.Teacher.User.FullName}",
                PeriodId = survey.PeriodId,
                Status = survey.Status.Code,
                AlreadySubmitted = alreadySubmitted,
                Questions = survey.Questions
                    .OrderBy(q => q.Id)
                    .Select(q => new QuestionDto
                    {
                        QuestionId = q.Id,
                        Text = q.Text,
                        Type = q.Type.Code,
                        // Для шкалы 1–5 варианты фиксированные; для text — null.
                        Options = q.Type.Code == "scale"
                            ? new List<int> { 1, 2, 3, 4, 5 }
                            : null
                    })
                    .ToList()
            };

            return Ok(dto);
        }

        /// <summary>
        /// O-04: GET /api/surveys/active — список активных анкет для текущего студента.
        /// Доступно только роли student (1).
        /// </summary>
        [HttpGet("active")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> GetActiveSurveys(CancellationToken ct)
        {
            // 1) Идентификатор студента — из токена.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var studentId))
                return Unauthorized();

            // 2) Загружаем только активные анкеты, у которых период открыт.
            //    Сразу включаем связи для формирования заголовка.
            var surveys = await _db.SurveyTemplates
                .AsNoTracking()
                .Include(s => s.Period).ThenInclude(p => p.Status)
                .Include(s => s.Status)
                .Include(s => s.Discipline)
                .Include(s => s.Teacher).ThenInclude(t => t.User)
                .Where(s => s.Status.Code == "active" && s.Period.Status.Code == "open")
                .OrderBy(s => s.Period.EndDate) // сначала те, что скоро закроются
                .ToListAsync(ct);

            if (!surveys.Any())
                return Ok(new List<SurveyListItemDto>());

            // 3) Одним запросом узнаем, на какие из этих анкет студент уже ответил.
            var surveyIds = surveys.Select(s => s.Id).ToList();
            var votedSurveyIds = await _db.VoterCheckins
                .AsNoTracking()
                .Where(vc => vc.StudentId == studentId
                          && surveyIds.Contains(vc.SurveyTemplateId)
                          && vc.HasVoted)
                .Select(vc => vc.SurveyTemplateId)
                .ToListAsync(ct);

            // 4) Формируем DTO.
            var result = surveys.Select(s => new SurveyListItemDto
            {
                SurveyId = s.Id,
                Title = $"{s.Discipline.Name} — {s.Teacher.User.Login}",
                PeriodId = s.PeriodId,
                AlreadySubmitted = votedSurveyIds.Contains(s.Id)
            }).ToList();

            return Ok(result);
        }


        /// <summary>
        /// O-05: POST /api/surveys/{surveyId}/submit — отправка ответов студентом.
        /// </summary>
        [HttpPost("{surveyId:guid}/submit")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> SubmitSurvey(Guid surveyId, [FromBody] SubmitSurveyRequest request, CancellationToken ct)
        {
            // 1) Идентификатор студента берем из токена.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var studentId)) return Unauthorized();

            // 2) Проверка Idempotency-Key (защита от повторной отправки при сбоях сети).
            if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) || string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { message = "Заголовок Idempotency-Key обязателен." });

            var cacheKey = $"idemp:submit:{studentId}:{idempotencyKey}";
            var redisDb = _redis.GetDatabase();

            // Если этот ключ уже обрабатывался (повторный запрос) — возвращаем сохраненный ответ.
            var cachedResult = await redisDb.StringGetAsync(cacheKey);
            if (cachedResult.HasValue)
            {
                var cachedObj = System.Text.Json.JsonSerializer.Deserialize<SubmitSurveyResponse>(cachedResult!);
                return Accepted(cachedObj);
            }

            // 3) Загружаем анкету и проверяем её статус.
            var survey = await _db.SurveyTemplates
                .Include(s => s.Period).ThenInclude(p => p.Status)
                .Include(s => s.Status)
                .Include(s => s.Questions).ThenInclude(q => q.Type)
                .FirstOrDefaultAsync(s => s.Id == surveyId, ct);

            if (survey is null) return NotFound(new { message = "Анкета не найдена." });
            if (survey.Status.Code != "active" || survey.Period.Status.Code != "open")
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Анкета не активна." });

            // 4) Защита от накрутки (проверяем voter_checkins).
            var alreadyVoted = await _db.VoterCheckins
                .AnyAsync(vc => vc.StudentId == studentId && vc.SurveyTemplateId == surveyId && vc.HasVoted, ct);

            if (alreadyVoted) return StatusCode(StatusCodes.Status409Conflict, new { message = "Вы уже отправляли эту анкету." });

            // 5) Валидация ответов.
            var answerEntities = new List<Answer>();
            foreach (var ans in request.Answers)
            {
                var question = survey.Questions.FirstOrDefault(q => q.Id == ans.QuestionId);
                if (question is null) return BadRequest(new { message = $"Вопрос {ans.QuestionId} не принадлежит этой анкете." });

                short? valueInt = null;
                string? valueText = null;

                if (question.TypeId == 1) // Тип "scale" (1-5)
                {
                    if (!short.TryParse(ans.Value, out var val) || val < 1 || val > 5)
                        return BadRequest(new { message = $"Некорректная оценка для вопроса {question.Id} (ожидается 1-5)." });
                    valueInt = val;
                }
                else // Тип "text"
                {
                    if (ans.Value.Length > 2000) return BadRequest(new { message = "Текст ответа слишком длинный (макс. 2000)." });
                    valueText = ans.Value;
                }

                answerEntities.Add(new Answer
                {
                    QuestionId = question.Id,
                    ValueInt = valueInt,
                    ValueText = valueText
                });
            }

            // 6) Сохранение в транзакции (чтобы не было рассинхрона).
            using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // 6.1 Факт голосования (знает КТО, но не знает ЧТО).
                _db.VoterCheckins.Add(new VoterCheckin { StudentId = studentId, SurveyTemplateId = surveyId, HasVoted = true });

                // 6.2 Обезличенный бюллетень (знает ЧТО, но не знает КТО).
                var ballot = new Ballot
                {
                    Id = Guid.NewGuid(),
                    SurveyTemplateId = surveyId,
                    SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow)
                };
                _db.Ballots.Add(ballot);

                // 6.3 Сами ответы.
                foreach (var a in answerEntities)
                {
                    a.BallotId = ballot.Id;
                    _db.Answers.Add(a);
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                // 7) Постановка задачи на классификацию ИИ (L-15).
                // Берем только текстовые ответы, которые реально были заполнены.
                var textAnswers = answerEntities.Where(a => !string.IsNullOrWhiteSpace(a.ValueText)).ToList();

                foreach (var textAnswer in textAnswers)
                {
                    // Публикуем событие в RabbitMQ.
                    await _publishEndpoint.Publish(new LlmClassificationRequested(
                        textAnswer.Id,
                        textAnswer.ValueText!
                    ), ct);
                }

                // 8) Кэшируем результат идемпотентности.
                var response = new SubmitSurveyResponse
                {
                    SubmissionId = ballot.Id,
                    SubmittedAt = DateTime.UtcNow,
                    Status = "accepted"
                };
                await redisDb.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(response), TimeSpan.FromHours(24));

                return Accepted(response);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        /// <summary>
        /// O-06: GET /api/my-surveys — история отправленных анкет текущего студента.
        /// Доступно только роли student (1).
        /// </summary>
        [HttpGet("/api/my-surveys")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> GetMySurveys(CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var studentId)) return Unauthorized();

            // Получаем ID анкет, на которые студент уже ответил
            var votedSurveyIds = await _db.VoterCheckins
                .AsNoTracking()
                .Where(vc => vc.StudentId == studentId && vc.HasVoted)
                .Select(vc => vc.SurveyTemplateId)
                .ToListAsync(ct);

            if (!votedSurveyIds.Any())
                return Ok(new List<SurveyListItemDto>());

            // Загружаем сами анкеты по этим ID
            var surveys = await _db.SurveyTemplates
                .AsNoTracking()
                .Include(s => s.Discipline)
                .Include(s => s.Teacher).ThenInclude(t => t.User)
                .Where(s => votedSurveyIds.Contains(s.Id))
                .OrderByDescending(s => s.Period.EndDate)
                .ToListAsync(ct);

            var result = surveys.Select(s => new SurveyListItemDto
            {
                SurveyId = s.Id,
                Title = $"{s.Discipline.Name} — {s.Teacher.User.Login}",
                PeriodId = s.PeriodId,
                AlreadySubmitted = true
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// O-08: GET /api/admin/periods — список периодов анкетирования для администратора.
        /// Доступно только роли admin (3).
        /// </summary>
        [HttpGet("/api/admin/periods")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> GetPeriods([FromQuery] string? status, CancellationToken ct)
        {
            var query = _db.Periods
                .AsNoTracking()
                .Include(p => p.Status)
                .AsQueryable();

            // Фильтрация по статусу (если передан, например, ?status=open)
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status.Code == status);
            }

            var periods = await query
                .OrderByDescending(p => p.StartDate)
                .Select(p => new PeriodDto
                {
                    PeriodId = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status.Code
                })
                .ToListAsync(ct);

            return Ok(periods);
        }

        /// <summary>
        /// O-09: PATCH /api/admin/periods/{periodId} — изменение статуса периода.
        /// Доступно только роли admin (3).
        /// </summary>
        [HttpPatch("/api/admin/periods/{periodId:guid}")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> UpdatePeriodStatus(Guid periodId, [FromBody] UpdatePeriodStatusRequest request, CancellationToken ct)
        {
            // 1) Получаем ID администратора из токена (нужен для аудита).
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(adminIdClaim, out var adminId)) return Unauthorized();

            // 2) Загружаем период и его текущий статус из БД.
            var period = await _db.Periods
                .Include(p => p.Status)
                .FirstOrDefaultAsync(p => p.Id == periodId, ct);

            if (period is null) return NotFound(new { message = "Период не найден." });

            var oldStatus = period.Status.Code;
            var newStatus = request.Status.ToLowerInvariant(); // Приводим к нижнему регистру (Open -> open)

            // 3) Проверка на конфликт: если статус уже такой, возвращаем 409 (как в контракте).
            if (oldStatus == newStatus)
                return Conflict(new { message = $"Период уже находится в статусе '{newStatus}'." });

            // 4) Проверка допустимости перехода.
            // Бизнес-правило: из архива нельзя вернуть период в работу.
            if (oldStatus == "archived")
                return BadRequest(new { message = "Нельзя изменить статус архивированного периода." });

            // 5) Ищем новый статус в справочнике (чтобы убедиться, что он существует).
            var statusEntity = await _db.PeriodStatuses.FirstOrDefaultAsync(s => s.Code == newStatus, ct);
            if (statusEntity is null)
                return BadRequest(new { message = $"Недопустимый статус: {newStatus}." });

            // 6) Обновляем статус периода.
            period.StatusId = statusEntity.Id;

            // 7) Записываем событие в журнал аудита (защита от непривилегированных действий).
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _db.AuditEvents.Add(new AuditEvent
            {
                UserId = adminId,
                EventType = "period_status_changed",
                EntityType = "period",
                EntityId = period.Id.ToString(),
                Details = $"{{\"old\":\"{oldStatus}\",\"new\":\"{newStatus}\"}}", // Сохраняем, что было и что стало
                IpAddress = ip,
                CreatedAt = DateTime.UtcNow
            });

            // 8) Сохраняем изменения в БД (одним SaveChanges, чтобы период и аудит обновились вместе).
            await _db.SaveChangesAsync(ct);

            // 9) Публикуем событие в RabbitMQ.
            // Сервис уведомлений (на Этапе 4) получит это сообщение и разошлёт письма.
            await _publishEndpoint.Publish(new PeriodStatusChanged(
                    period.Id,
                    period.Name,
                    oldStatus,
                    newStatus,
                    DateTime.UtcNow
                ), ct);

            // 10) Возвращаем ответ клиенту.
            return Ok(new UpdatePeriodStatusResponse
            {
                PeriodId = period.Id,
                Status = newStatus,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}