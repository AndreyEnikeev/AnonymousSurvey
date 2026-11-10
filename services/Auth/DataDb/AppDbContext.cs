using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.DataDb
{
    /// <summary>
    /// Контекст базы данных для Entity Framework Core.
    /// Отвечает за подключение к PostgreSQL и предоставляет доступ к таблицам через DbSet.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Таблица пользователей (учётные записи).
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Таблица журнала событий безопасности (аудит).
        /// </summary>
        public DbSet<AuditEvent> AuditEvents { get; set; }

        /// <summary>
        /// Таблица одноразовых токенов (для сброса пароля и подтверждения email).
        /// </summary>
        public DbSet<OneTimeToken> OneTimeTokens { get; set; }

        /// <summary>
        /// Настройка связей между C#-классами и реальными таблицами в PostgreSQL.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Привязываем классы к существующим таблицам из schema.sql
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<AuditEvent>().ToTable("audit_events");
            modelBuilder.Entity<OneTimeToken>().ToTable("one_time_tokens");
        }
    }

    /// <summary>
    /// Модель пользователя (строго соответствует таблице users из schema.sql).
    ///
    /// Поле full_name добавлено по D-11: колонка есть в схеме,
    /// используется для персонализации писем (PasswordResetRequested).
    /// </summary>
    [Table("users")]
    public class User
    {
        /// <summary>Уникальный идентификатор пользователя (uuid).</summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>Логин (уникальный, используется для входа).</summary>
        [Column("login")]
        public string Login { get; set; } = string.Empty;

        /// <summary>Хеш пароля (Argon2id, PHC-строка). Никогда не возвращается клиенту.</summary>
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Соль пароля (отдельный столбец, см. п. 3.3 ЛР №2).</summary>
        [Column("password_salt")]
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>Идентификатор роли: 1=student, 2=teacher, 3=admin, 4=tech.</summary>
        [Column("role_id")]
        public short RoleId { get; set; }

        /// <summary>Адрес электронной почты (уникальный, используется в O-02).</summary>
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// ФИО пользователя. NOT NULL в БД (см. D-11).
        /// Используется для персонализации писем (PasswordResetRequested.FullName).
        /// </summary>
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Модель события безопасности (строго соответствует таблице audit_events).
    /// Используется для журналирования успешных и неуспешных действий.
    /// </summary>
    [Table("audit_events")]
    public class AuditEvent
    {
        [Column("id")]
        public long Id { get; set; }

        /// <summary>
        /// ID пользователя. Может быть NULL, если событие не связано с конкретным пользователем 
        /// (например, попытка входа с несуществующим логином).
        /// </summary>
        [Column("user_id")]
        public Guid? UserId { get; set; }

        /// <summary>
        /// Тип события (например, "login_success", "login_failed", "password_reset").
        /// </summary>
        [Column("event_type")]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// С какой сущностью работали (например, "user", "period").
        /// </summary>
        [Column("entity_type")]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// ID сущности в виде строки (для гибкости).
        /// </summary>
        [Column("entity_id")]
        public string? EntityId { get; set; }

        /// <summary>
        /// Детали события в формате JSON (например, IP-адрес, сообщение об ошибке).
        /// TypeName = "jsonb" указывает Entity Framework использовать нативный тип JSON в PostgreSQL.
        /// </summary>
        [Column("details", TypeName = "jsonb")]
        public string? Details { get; set; }

        /// <summary>
        /// IP-адрес, с которого пришёл запрос (IPv4 или IPv6).
        /// </summary>
        [Column("ip_address")]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Время создания события (в UTC).
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Модель одноразового токена (строго соответствует таблице one_time_tokens).
    /// Используется для безопасного сброса пароля и подтверждения email.
    /// </summary>
    [Table("one_time_tokens")]
    public class OneTimeToken
    {
        [Column("id")]
        public long Id { get; set; }

        /// <summary>
        /// ID пользователя, для которого выпущен токен.
        /// </summary>
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Назначение токена: "password_reset" или "email_verify".
        /// </summary>
        [Column("purpose")]
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// Хеш токена (сам токен в базе не хранится из соображений безопасности).
        /// </summary>
        [Column("token_hash")]
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Дата и время истечения срока действия токена.
        /// </summary>
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Дата и время использования токена. Если NULL — токен ещё не использован.
        /// </summary>
        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// Время создания токена.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}