using Analytics.DataDb;
using Analytics.Models;
using Analytics.Models.Dtos;
using Analytics.Services;
using ClosedXML.Excel; // <-- Обязательно для работы с Excel
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace Analytics.Controllers
{
    /// <summary>
    /// Контроллер для работы с аналитическими отчетами (C-05).
    /// Предоставляет доступ к агрегированным данным (O-07) и выгрузке файлов (O-10).
    /// </summary>
    [ApiController]
    public class ReportsController : ControllerBase
    {
        /// <summary>Контекст базы данных для выполнения запросов к PostgreSQL.</summary>
        private readonly AnalyticsDbContext _db;

        /// <summary>Подключение к Redis для кэширования тяжелых агрегаций.</summary>
        private readonly IConnectionMultiplexer _redis;

        /// <summary>Сервис для работы с файловым хранилищем MinIO.</summary>
        private readonly MinioService _minio;

        /// <summary>
        /// Конструктор контроллера. Зависимости внедряются через DI-контейнер.
        /// </summary>
        public ReportsController(AnalyticsDbContext db, IConnectionMultiplexer redis, MinioService minio)
        {
            _db = db;
            _redis = redis;
            _minio = minio;
        }

        /// <summary>
        /// O-07: GET /api/reports/aggregated — получение агрегированного обезличенного отчета.
        /// Вычисляет средние оценки по дисциплинам и преподавателям с поддержкой фильтрации и пагинации.
        /// Доступно только ролям teacher (2) и admin (3).
        /// </summary>
        /// <param name="disciplineId">Фильтр по идентификатору дисциплины (опционально).</param>
        /// <param name="periodId">Фильтр по идентификатору периода (опционально).</param>
        /// <param name="teacherId">Фильтр по идентификатору преподавателя (опционально).</param>
        /// <param name="limit">Количество записей для пагинации (по умолчанию 20).</param>
        /// <param name="offset">Смещение для пагинации (по умолчанию 0).</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Объект с массивом агрегированных записей и метаданными пагинации.</returns>
        [HttpGet("/api/reports/aggregated")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> GetAggregatedReport(
            [FromQuery] Guid? disciplineId,
            [FromQuery] Guid? periodId,
            [FromQuery] Guid? teacherId,
            [FromQuery] int limit = 20,
            [FromQuery] int offset = 0,
            CancellationToken ct = default)
        {
            // 1. Извлекаем идентификатор текущего пользователя из JWT-токена.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            // 2. Проверяем роль. Если это преподаватель, он должен видеть ТОЛЬКО свои отчеты.
            var isTeacher = User.IsInRole("2");
            Guid? forcedTeacherId = null;

            if (isTeacher)
            {
                var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId, ct);
                if (teacher == null)
                    return Forbid();

                forcedTeacherId = teacher.Id;
            }

            // 3. Проверяем кэш Redis.
            var cacheKey = $"reports:agg:{userId}:{disciplineId}:{periodId}:{teacherId}:{limit}:{offset}";
            var redisDb = _redis.GetDatabase();
            var cached = await redisDb.StringGetAsync(cacheKey);

            if (cached.HasValue)
            {
                return Ok(JsonSerializer.Deserialize<AggregatedReportResponseDto>(cached!));
            }

            // 4. Формируем базовый запрос к БД.
            var query = _db.SurveyTemplates.AsNoTracking()
                .Include(s => s.Period)
                .Include(s => s.Discipline)
                .Include(s => s.Teacher).ThenInclude(t => t.User)
                .AsQueryable();

            // 5. Применяем фильтры.
            if (forcedTeacherId.HasValue)
                query = query.Where(s => s.TeacherId == forcedTeacherId.Value);
            else if (teacherId.HasValue)
                query = query.Where(s => s.TeacherId == teacherId.Value);

            if (disciplineId.HasValue)
                query = query.Where(s => s.DisciplineId == disciplineId.Value);

            if (periodId.HasValue)
                query = query.Where(s => s.PeriodId == periodId.Value);

            // 6. Пагинация.
            var total = await query.CountAsync(ct);
            var templates = await query.OrderBy(s => s.Discipline.Name).Skip(offset).Take(limit).ToListAsync(ct);

            // 7. Загружаем ВСЕ ответы для найденных шаблонов (и числовые, и текстовые).
            var templateIds = templates.Select(t => t.Id).ToList();

            var allAnswers = await _db.Answers.AsNoTracking()
                .Where(a => _db.Questions.Where(q => templateIds.Contains(q.SurveyTemplateId)).Select(q => q.Id).Contains(a.QuestionId))
                .ToListAsync(ct);

            var questionToTemplate = await _db.Questions.AsNoTracking()
                .Where(q => templateIds.Contains(q.SurveyTemplateId))
                .ToDictionaryAsync(q => q.Id, q => q.SurveyTemplateId, ct);

            // 8. Вычисляем средние значения и формируем DTO.
            var items = templates.Select(t =>
            {
                var tAnswers = allAnswers.Where(a => questionToTemplate[a.QuestionId] == t.Id).ToList();

                var numericAnswers = tAnswers.Where(a => a.ValueInt != null).ToList();
                var textAnswers = tAnswers.Where(a => !string.IsNullOrWhiteSpace(a.ValueText)).ToList();

                var avg = numericAnswers.Any() ? numericAnswers.Average(a => a.ValueInt!.Value) : 0;

                return new AggregatedReportItemDto
                {
                    DisciplineId = t.DisciplineId,
                    TeacherId = t.TeacherId,
                    PeriodId = t.PeriodId,
                    AverageScore = Math.Round(avg, 2),
                    ResponseCount = numericAnswers.Count,
                    TextResponsesCount = textAnswers.Count, // Считаем текстовые ответы
                    Sentiment = null // D-01: LLM не реализован
                };
            }).ToList();

            var result = new AggregatedReportResponseDto
            {
                Items = items,
                Total = total,
                Limit = limit,
                Offset = offset
            };

            // 9. Кэшируем результат на 5 минут.
            await redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(5));

            return Ok(result);
        }

        /// <summary>
        /// O-10: POST /api/admin/export — формирование и выгрузка сводного отчета в формате Excel (XLSX).
        /// Каждый шаблон анкеты выгружается на отдельный лист.
        /// Доступно только роли admin (3).
        /// </summary>
        [HttpPost("/api/admin/export")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> ExportReport([FromBody] ExportRequestDto request, CancellationToken ct)
        {
            if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyStr)
                || !Guid.TryParse(idempotencyKeyStr, out var idempotencyKey))
                return BadRequest(new { message = "Заголовок Idempotency-Key обязателен (UUID)." });

            var existingExport = await _db.Exports.AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey, ct);

            if (existingExport != null)
            {
                var url = _minio.GetPreSignedUrl(existingExport.ObjectKey, existingExport.ExpiresAt - DateTime.UtcNow);
                return Ok(new ExportResponseDto { ExportId = existingExport.Id, DownloadUrl = url, ExpiresAt = existingExport.ExpiresAt, Status = "ready" });
            }

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var periodName = await _db.Periods.AsNoTracking()
                .Where(p => p.Id == request.PeriodId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "Отчет";

            var templates = await _db.SurveyTemplates.AsNoTracking()
                .Include(s => s.Discipline).Include(s => s.Teacher).ThenInclude(t => t.User)
                .Where(s => s.PeriodId == request.PeriodId)
                .ToListAsync(ct);

            if (!templates.Any())
                return NotFound(new { message = "За выбранный период нет анкет." });

            var templateIds = templates.Select(t => t.Id).ToList();

            var allQuestions = await _db.Questions.AsNoTracking()
                .Where(q => templateIds.Contains(q.SurveyTemplateId))
                .OrderBy(q => q.Id)
                .ToListAsync(ct);

            var allBallots = await _db.Ballots.AsNoTracking()
                .Where(b => templateIds.Contains(b.SurveyTemplateId))
                .ToListAsync(ct);

            var questionIds = allQuestions.Select(q => q.Id).ToList();
            var allAnswers = await _db.Answers.AsNoTracking()
                .Where(a => questionIds.Contains(a.QuestionId))
                .ToListAsync(ct);

            using var workbook = new XLWorkbook();

            // =========================================================
            // 1. ЛИСТ "СВОДКА"
            // =========================================================
            var summarySheet = workbook.Worksheets.Add("Сводка");
            summarySheet.Cell(1, 1).Value = "Код дисциплины";
            summarySheet.Cell(1, 2).Value = "Название дисциплины";
            summarySheet.Cell(1, 3).Value = "Преподаватель";
            summarySheet.Cell(1, 4).Value = "Средний балл";
            summarySheet.Cell(1, 5).Value = "Кол-во оценок";
            summarySheet.Cell(1, 6).Value = "Кол-во текстовых ответов";
            summarySheet.Row(1).Style.Font.Bold = true;

            int summaryRow = 2;
            foreach (var t in templates)
            {
                var tAnswers = allAnswers.Where(a => allQuestions.Any(q => q.Id == a.QuestionId && q.SurveyTemplateId == t.Id)).ToList();
                var numericAnswers = tAnswers.Where(a => a.ValueInt != null).ToList();
                var textAnswers = tAnswers.Where(a => !string.IsNullOrWhiteSpace(a.ValueText)).ToList();
                var avg = numericAnswers.Any() ? Math.Round(numericAnswers.Average(a => a.ValueInt!.Value), 2) : 0;

                summarySheet.Cell(summaryRow, 1).Value = t.Discipline.Code;
                summarySheet.Cell(summaryRow, 2).Value = t.Discipline.Name;
                summarySheet.Cell(summaryRow, 3).Value = t.Teacher.User.FullName;
                summarySheet.Cell(summaryRow, 4).Value = avg;
                summarySheet.Cell(summaryRow, 5).Value = numericAnswers.Count;
                summarySheet.Cell(summaryRow, 6).Value = textAnswers.Count;
                summaryRow++;
            }
            summarySheet.Columns().AdjustToContents();


            // =========================================================
            // 2. ЛИСТЫ ДЛЯ КАЖДОЙ АНКЕТЫ
            // =========================================================
            foreach (var template in templates)
            {
                // 1. Формируем имя листа из кода дисциплины и ФИО преподавателя
                var rawSheetName = $"{template.Discipline.Code}_{template.Teacher.User.FullName}";

                // 2. Очищаем от запрещенных в Excel символов ( : \ / ? * [ ] )
                var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
                var cleanSheetName = string.Join("_", rawSheetName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

                // 3. Обрезаем до 31 символа (ограничение Excel), если ФИО окажется слишком длинным
                if (cleanSheetName.Length > 31)
                    cleanSheetName = cleanSheetName.Substring(0, 31).TrimEnd('_', ' ');

                var worksheet = workbook.Worksheets.Add(cleanSheetName);

                var templateQuestions = allQuestions.Where(q => q.SurveyTemplateId == template.Id).ToList();

                // Формируем карту колонок: true = это колонка ИИ, false = это вопрос
                var columnMap = new List<(long? QuestionId, bool IsAi)>();
                int col = 1;

                foreach (var q in templateQuestions)
                {
                    worksheet.Cell(1, col).Value = q.Text;
                    worksheet.Cell(1, col).Style.Font.Bold = true;
                    worksheet.Cell(1, col).Style.Alignment.WrapText = true;
                    columnMap.Add((q.Id, false));
                    col++;

                    // Если вопрос текстовый (TypeId == 2), сразу добавляем колонку для ИИ
                    if (q.TypeId == 2)
                    {
                        worksheet.Cell(1, col).Value = $"Оценка ИИ (для: {q.Text})";
                        worksheet.Cell(1, col).Style.Font.Bold = true;
                        worksheet.Cell(1, col).Style.Font.FontColor = XLColor.Gray;
                        worksheet.Cell(1, col).Style.Alignment.WrapText = true;
                        columnMap.Add((null, true));
                        col++;
                    }
                }

                // Заполняем строки
                var templateBallots = allBallots.Where(b => b.SurveyTemplateId == template.Id).ToList();
                int row = 2;

                foreach (var ballot in templateBallots)
                {
                    var ballotAnswers = allAnswers.Where(a => a.BallotId == ballot.Id).ToList();

                    for (int i = 0; i < columnMap.Count; i++)
                    {
                        var map = columnMap[i];
                        int currentCol = i + 1;

                        if (!map.IsAi)
                        {
                            var ans = ballotAnswers.FirstOrDefault(a => a.QuestionId == map.QuestionId);
                            if (ans != null)
                            {
                                if (ans.ValueInt.HasValue)
                                    worksheet.Cell(row, currentCol).Value = ans.ValueInt.Value;
                                else if (!string.IsNullOrWhiteSpace(ans.ValueText))
                                    worksheet.Cell(row, currentCol).Value = ans.ValueText;
                            }
                        }
                        else
                        {
                            // Заглушка для ИИ
                            worksheet.Cell(row, currentCol).Value = "—";
                        }
                    }
                    row++;
                }

                worksheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var safePeriodName = string.Join("_", periodName.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"Отчет {safePeriodName} {DateTime.UtcNow:ddMMyyyy_HHmmss}.xlsx";

            var objectKey = await _minio.UploadFileAsync(
                stream.ToArray(),
                fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ct);

            var expiresAt = DateTime.UtcNow.AddHours(1);
            var export = new Export
            {
                Id = Guid.NewGuid(),
                CreatedBy = userId,
                PeriodId = request.PeriodId,
                FormatId = 2,
                StatusId = 2,
                IdempotencyKey = idempotencyKey,
                ObjectKey = objectKey,
                ExpiresAt = expiresAt
            };

            _db.Exports.Add(export);
            await _db.SaveChangesAsync(ct);

            var downloadUrl = _minio.GetPreSignedUrl(objectKey, TimeSpan.FromHours(1));

            return Ok(new ExportResponseDto
            {
                ExportId = export.Id,
                DownloadUrl = downloadUrl,
                ExpiresAt = expiresAt,
                Status = "ready"
            });
        }
    }
}