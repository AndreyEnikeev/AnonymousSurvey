using Analytics.Models;
using Microsoft.EntityFrameworkCore;

namespace Analytics.DataDb
{
    /// <summary>
    /// Контекст EF Core для сервиса аналитики (C-05).
    /// </summary>
    public class AnalyticsDbContext : DbContext
    {
        /// <summary>Конструктор контекста с опциями подключения к БД.</summary>
        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

        /// <summary>Набор ответов на вопросы (таблица answers).</summary>
        public DbSet<Answer> Answers => Set<Answer>();

        /// <summary>Набор вопросов анкет (таблица questions).</summary>
        public DbSet<Question> Questions => Set<Question>();

        /// <summary>Набор шаблонов анкет (таблица survey_templates).</summary>
        public DbSet<SurveyTemplate> SurveyTemplates => Set<SurveyTemplate>();

        /// <summary>Набор периодов анкетирования (таблица periods).</summary>
        public DbSet<Period> Periods => Set<Period>();

        /// <summary>Набор дисциплин (таблица disciplines).</summary>
        public DbSet<Discipline> Disciplines => Set<Discipline>();

        /// <summary>Набор преподавателей (таблица teachers).</summary>
        public DbSet<Teacher> Teachers => Set<Teacher>();

        /// <summary>Набор пользователей (таблица users).</summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>Набор записей о выгрузках (таблица exports).</summary>
        public DbSet<Export> Exports => Set<Export>();

        /// <summary>Набор обезличенных бюллетеней (таблица ballots).</summary>
        public DbSet<Ballot> Ballots => Set<Ballot>(); // <-- ДОБАВЛЕНО

        /// <summary>
        /// Настройка связей между сущностями и явное указание имен таблиц.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Answer>().ToTable("answers");
            modelBuilder.Entity<Question>().ToTable("questions");
            modelBuilder.Entity<SurveyTemplate>().ToTable("survey_templates");
            modelBuilder.Entity<Period>().ToTable("periods");
            modelBuilder.Entity<Discipline>().ToTable("disciplines");
            modelBuilder.Entity<Teacher>().ToTable("teachers");
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Export>().ToTable("exports");
            modelBuilder.Entity<Ballot>().ToTable("ballots");

            // Связи для SurveyTemplate
            modelBuilder.Entity<SurveyTemplate>(e =>
            {
                e.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.PeriodId);
                e.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId);
                e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId);
            });

            // Связь Teacher -> User
            modelBuilder.Entity<Teacher>(e =>
            {
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            });
        }
    }
}