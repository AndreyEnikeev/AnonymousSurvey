using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Auth.Services
{
    /// <summary>
    /// Сервис для генерации JWT (JSON Web Tokens) для аутентификации пользователей.
    /// </summary>
    public class TokenService
    {
        /// <summary>
        /// Секретный ключ для подписи токена (алгоритм HMAC-SHA256). 
        /// Читается из User Secrets.
        /// </summary>
        private readonly string _signingKey;

        /// <summary>
        /// Издатель токена (Issuer). Идентификатор нашей системы. 
        /// Используется на стороне Gateway для проверки, что токен выпущен именно нашим сервисом Auth.
        /// </summary>
        private readonly string _issuer;

        /// <summary>
        /// Аудитория токена (Audience). Для кого выпущен токен.
        /// Защищает от использования токена на сторонних ресурсах, если он был перехвачен.
        /// </summary>
        private readonly string _audience;

        /// <summary>
        /// Время жизни Access-токена (в минутах).
        /// </summary>
        private readonly int _accessTtlMinutes;

        /// <summary>
        /// База данных Redis для хранения Refresh-токенов.
        /// Redis используется для того, чтобы можно было отозвать токен при выходе из системы (Logout).
        /// </summary>
        private readonly IDatabase _redisDb;

        /// <summary>
        /// Инициализирует сервис токенов, загружая секретный ключ подписи из конфигурации 
        /// и подключаясь к Redis.
        /// </summary>
        /// <param name="configuration">Конфигурация приложения для получения ключей.</param>
        /// <param name="redis">Экземпляр подключения к Redis (внедряется через DI).</param>
        public TokenService(IConfiguration configuration, IConnectionMultiplexer redis)
        {
            // Читаем секрет из User Secrets
            _signingKey = configuration["Jwt:SigningKey"] ?? throw new ArgumentNullException("SigningKey");

            // Читаем конфигурацию из appsettings.json
            _issuer = configuration["Jwt:Issuer"] ?? "anon-survey";
            _audience = configuration["Jwt:Audience"] ?? "anon-survey-client";

            // Время жизни токена (в минутах)
            _accessTtlMinutes = configuration.GetValue<int>("Jwt:AccessTtlMinutes", 30);

            // Получаем базу данных Redis для дальнейшей работы
            _redisDb = redis.GetDatabase();
        }

        /// <summary>
        /// Генерирует новый JWT access-токен для аутентифицированного пользователя.
        /// </summary>
        /// <param name="userId">Уникальный идентификатор пользователя (GUID).</param>
        /// <param name="roleId">Идентификатор роли пользователя (например, 1 для студента).</param>
        /// <param name="login">Логин пользователя.</param>
        /// <returns>Строка с подписанным JWT-токеном.</returns>
        public string GenerateAccessToken(Guid userId, short roleId, string login)
        {
            // Утверждения (Claims) — это данные, которые мы кладем внутрь токена.
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, login),
                new Claim(ClaimTypes.Role, roleId.ToString()) // Роль нужна для авторизации в Gateway
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTtlMinutes), // Используем значение из конфигурации
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Генерирует Refresh-токен и сохраняет его в Redis.
        /// Используется для того, чтобы пользователь мог обновить сессию без повторного ввода пароля,
        /// а также чтобы можно было сделать реальный Logout (удалив этот токен).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя, которому принадлежит токен.</param>
        /// <returns>Строка с новым Refresh-токеном.</returns>
        public async Task<string> GenerateRefreshTokenAsync(Guid userId)
        {
            // Генерируем случайный токен (GUID)
            var refreshToken = Guid.NewGuid().ToString();

            // Сохраняем в Redis: ключ = "refresh:<токен>", значение = userId. 
            // Время жизни (TTL): 7 дней. Redis сам удалит его по истечении этого срока.
            await _redisDb.StringSetAsync($"refresh:{refreshToken}", userId.ToString(), TimeSpan.FromDays(7));

            return refreshToken;
        }

        /// <summary>
        /// Удаляет Refresh-токен из Redis. Вызывается при выходе из системы (Logout).
        /// После этого токен становится недействительным и не может быть использован.
        /// </summary>
        /// <param name="refreshToken">Строка Refresh-токена, который нужно отозвать.</param>
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            await _redisDb.KeyDeleteAsync($"refresh:{refreshToken}");
        }
    }
}