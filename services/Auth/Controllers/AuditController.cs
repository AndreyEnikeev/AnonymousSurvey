using Auth.Data;
using Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.Controllers
{
    /// <summary>
    /// O-12: Просмотр журнала событий безопасности.
    /// Доступен только ролям admin (3) и tech (4).
    /// Проверка ролей — здесь, в Auth. Gateway ролей не проверяет.
    /// </summary>
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "3,4")]
    public class AuditController : ControllerBase
    {
        /// <summary>
        /// Контекст базы данных для чтения таблицы audit_events.
        /// </summary>
        private readonly AppDbContext _db;

        /// <summary>
        /// Конструктор контроллера. ASP.NET Core внедряет зависимость автоматически.
        /// </summary>
        public AuditController(AppDbContext db) => _db = db;

        /// <summary>
        /// GET /api/audit/events — список событий с фильтрами и пагинацией.
        /// </summary>
        /// <param name="eventType">Фильтр по типу события (login_success, login_failed и т.д.).</param>
        /// <param name="userId">Фильтр по идентификатору пользователя.</param>
        /// <param name="from">Нижняя граница по времени события (включительно).</param>
        /// <param name="to">Верхняя граница по времени события (включительно).</param>
        /// <param name="limit">Размер страницы (1–100, по умолчанию 20).</param>
        /// <param name="offset">Смещение от начала выборки (по умолчанию 0).</param>
        [HttpGet("events")]
        public async Task<IActionResult> GetEvents(
            [FromQuery] string? eventType,
            [FromQuery] Guid? userId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int limit = 20,
            [FromQuery] int offset = 0)
        {
            // Клипуем limit и offset, чтобы клиент не мог запросить
            // всю таблицу одним запросом.
            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            var query = _db.AuditEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(e => e.EventType == eventType);

            if (userId.HasValue)
                query = query.Where(e => e.UserId == userId.Value);

            if (from.HasValue)
                query = query.Where(e => e.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.CreatedAt <= to.Value);

            // Total считаем по фильтру, до пагинации.
            var total = await query.CountAsync();

            // В DTO выбираем только нужные поля (не всю строку таблицы).
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .Select(e => new AuditEventDto
                {
                    Id = e.Id,
                    UserId = e.UserId,
                    EventType = e.EventType,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    Details = e.Details,
                    IpAddress = e.IpAddress,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(new AuditEventsPage
            {
                Items = items,
                Total = total,
                Limit = limit,
                Offset = offset
            });
        }
    }
}