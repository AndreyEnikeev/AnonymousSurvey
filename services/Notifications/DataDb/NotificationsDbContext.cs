using Microsoft.EntityFrameworkCore;
using Notifications.Models;

namespace Notifications.DataDb
{
    /// <summary>
    /// Контекст EF Core для сервиса уведомлений (C-07).
    /// 
    /// Использует две таблицы:
    /// - users (SELECT на id, email, role_id);
    /// - audit_events (INSERT).
    /// 
    /// Оба имени таблиц мапятся в snake_case (настраивается в Program.cs
    /// через UseSnakeCaseNamingConvention).
    /// </summary>
    public class NotificationsDbContext : DbContext
    {
        /// <summary>Конструктор с опциями подключения к БД.</summary>
        public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
            : base(options) { }

        /// <summary>Проекция пользователей (только нужные для рассылки поля).</summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>Журнал событий безопасности.</summary>
        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        /// <summary>
        /// Настройка маппинга сущностей на существующую схему из ЛР №2.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToTable("users");

            modelBuilder.Entity<AuditEvent>(e =>
            {
                e.ToTable("audit_events");
                // Колонка details имеет тип jsonb. Без явного HasColumnType
                // Npgsql отправит строку как text и БД выдаст ошибку 42804.
                e.Property(x => x.Details).HasColumnType("jsonb");
            });
        }
    }
}