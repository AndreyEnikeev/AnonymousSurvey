using Auth.Data;
using Auth.Models;
using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Auth.Controllers
{
    /// <summary>
    /// Контроллер для управления аутентификацией пользователей (вход, выход, сброс пароля).
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        /// <summary>
        /// Контекст базы данных для доступа к таблицам.
        /// </summary>
        private readonly AppDbContext _db;

        /// <summary>
        /// Сервис для проверки и генерации паролей (Argon2id).
        /// </summary>
        private readonly PasswordHasher _passwordHasher;

        /// <summary>
        /// Сервис для генерации JWT-токенов.
        /// </summary>
        private readonly TokenService _tokenService;

        /// <summary>
        /// Конструктор контроллера. ASP.NET Core внедряет зависимости автоматически.
        /// </summary>
        public AuthController(AppDbContext db, PasswordHasher passwordHasher, TokenService tokenService)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        /// <summary>
        /// O-01: Вход в систему. Проверяет логин и пароль, возвращает Access и Refresh токены.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == request.Login);

            if (user == null)
            {
                await LogAuditEvent(null, "login_failed", $"Попытка входа с несуществующим логином: {request.Login}");
                return Unauthorized(new { message = "Неверный логин или пароль" });
            }

            var isValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash);
            if (!isValid)
            {
                await LogAuditEvent(user.Id, "login_failed", "Неверный пароль");
                return Unauthorized(new { message = "Неверный логин или пароль" });
            }

            var accessToken = _tokenService.GenerateAccessToken(user.Id, user.RoleId, user.Login);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

            await LogAuditEvent(user.Id, "login_success");

            return Ok(new { accessToken, refreshToken });
        }

        /// <summary>
        /// O-14: Выход из системы. Удаляет Refresh-токен из Redis.
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (!string.IsNullOrEmpty(request.RefreshToken))
            {
                await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
                await LogAuditEvent(null, "logout_success", "Пользователь вышел из системы");
            }

            return NoContent();
        }

        /// <summary>
        /// O-02: Запрос на сброс пароля. Генерирует одноразовый токен и "отправляет" письмо.
        /// </summary>
        [HttpPost("password-reset")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request)
        {
            // Ищем по email или логину
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.LoginOrEmail || u.Login == request.LoginOrEmail);

            // Всегда возвращаем 200 OK, даже если юзера нет (чтобы хакеры не могли перебирать базу)
            if (user == null)
            {
                return Ok(new { message = "Если пользователь существует, письмо отправлено." });
            }

            // Генерируем случайный токен
            var resetToken = Guid.NewGuid().ToString("N");

            // Хешируем токен (в базе храним только хеш, как и с паролями)
            using var sha256 = SHA256.Create();
            var tokenHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(resetToken)));

            // Сохраняем в БД на 15 минут
            _db.OneTimeTokens.Add(new OneTimeToken
            {
                UserId = user.Id,
                Purpose = "password_reset",
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });

            await _db.SaveChangesAsync();
            await LogAuditEvent(user.Id, "password_reset_requested");

            // В реальности здесь мы бы отправили email через SMTP (на Этапе 4 мы это сделаем).
            // Пока выводим в консоль, чтобы вы могли протестировать.
            Console.WriteLine("==================================================");
            Console.WriteLine($"СБРОС ПАРОЛЯ ДЛЯ {user.Login}");
            Console.WriteLine($"Токен: {resetToken}");
            Console.WriteLine("==================================================");

            return Ok(new { message = "Если пользователь существует, письмо отправлено." });
        }

        /// <summary>
        /// O-02 (подтверждение): Смена пароля по одноразовому токену.
        /// </summary>
        [HttpPost("password-reset/confirm")]
        public async Task<IActionResult> ConfirmPasswordReset([FromBody] ResetPasswordRequest request)
        {
            // Хешируем присланный токен, чтобы найти его в базе
            using var sha256 = SHA256.Create();
            var tokenHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Token)));

            // Ищем действующий и неиспользованный токен
            var dbToken = await _db.OneTimeTokens.FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.Purpose == "password_reset" &&
                t.UsedAt == null &&
                t.ExpiresAt > DateTime.UtcNow);

            if (dbToken == null)
            {
                return BadRequest(new { message = "Токен недействителен или истек срок его действия." });
            }

            var user = await _db.Users.FindAsync(dbToken.UserId);
            if (user == null) return BadRequest();

            // Генерируем новую соль и хеш для нового пароля
            user.PasswordSalt = _passwordHasher.GenerateSalt();
            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword, user.PasswordSalt);

            // Помечаем токен как использованный (сжечь мосты!)
            dbToken.UsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await LogAuditEvent(user.Id, "password_reset_success");

            return Ok(new { message = "Пароль успешно изменен." });
        }

        /// <summary>
        /// Вспомогательный метод для записи события в журнал аудита (таблица audit_events).
        /// </summary>
        private async Task LogAuditEvent(Guid? userId, string eventType, string details = "")
        {
            // Получаем IP-адрес клиента (если не удалось — пишем unknown)
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Экранируем кавычки в строке, чтобы не сломать JSON
            var safeDetails = details.Replace("\"", "\\\"");

            _db.AuditEvents.Add(new AuditEvent
            {
                UserId = userId,
                EventType = eventType,
                EntityType = "user",
                EntityId = userId?.ToString(),
                Details = string.IsNullOrEmpty(details) ? null : $"{{\"message\": \"{safeDetails}\"}}",
                IpAddress = ip,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}