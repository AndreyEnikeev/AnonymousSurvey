using LlmOrchestrator.Models;
using Microsoft.EntityFrameworkCore;

namespace LlmOrchestrator.DataDb
{
    /// <summary>
    /// Контекст EF Core для сервиса LLM-оркестратора (C-06).
    /// Обеспечивает доступ к таблицам результатов классификации и аудита.
    /// </summary>
    public class LlmDbContext : DbContext
    {
        /// <summary>
        /// Конструктор контекста с опциями подключения к БД.
        /// </summary>
        /// <param name="options">Опции, передаваемые через DI.</param>
        public LlmDbContext(DbContextOptions<LlmDbContext> options) : base(options) { }

        /// <summary>
        /// Набор результатов классификации (таблица llm_classifications).
        /// </summary>
        public DbSet<LlmClassification> LlmClassifications => Set<LlmClassification>();

        /// <summary>
        /// Набор событий аудита (таблица audit_events).
        /// </summary>
        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        /// <summary>
        /// Настройка маппинга сущностей на таблицы и типы колонок.
        /// </summary>
        /// <param name="modelBuilder">Построитель модели.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LlmClassification>(e =>
            {
                e.ToTable("llm_classifications");
                // Указываем тип jsonb для колонки topics, как в схеме ЛР №2.
                e.Property(x => x.Topics).HasColumnType("jsonb");
            });

            modelBuilder.Entity<AuditEvent>(e =>
            {
                e.ToTable("audit_events");
                // Указываем тип jsonb для колонки details.
                e.Property(x => x.Details).HasColumnType("jsonb");
            });
        }
    }
}