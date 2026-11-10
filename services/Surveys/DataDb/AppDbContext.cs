using Microsoft.EntityFrameworkCore;
using Surveys.Models;

namespace Surveys.DataDb
{
    /// <summary>
    /// Контекст EF Core для сервиса анкетирования (C-04).
    /// Мапится на существующую схему из ЛР № 2.
    /// Имена таблиц и колонок — snake_case (за счёт UseSnakeCaseNamingConvention в Program.cs).
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>Конструктор контекста с опциями подключения к БД.</summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        /// <summary>Набор шаблонов анкет (таблица survey_templates).</summary>
        public DbSet<SurveyTemplate> SurveyTemplates => Set<SurveyTemplate>();

        /// <summary>Набор вопросов (таблица questions).</summary>
        public DbSet<Question> Questions => Set<Question>();

        /// <summary>Набор периодов анкетирования (таблица periods).</summary>
        public DbSet<Period> Periods => Set<Period>();

        /// <summary>Справочник статусов анкет (таблица survey_statuses).</summary>
        public DbSet<SurveyStatus> SurveyStatuses => Set<SurveyStatus>();

        /// <summary>Справочник статусов периодов (таблица period_statuses).</summary>
        public DbSet<PeriodStatus> PeriodStatuses => Set<PeriodStatus>();

        /// <summary>Справочник типов вопросов (таблица question_types).</summary>
        public DbSet<QuestionType> QuestionTypes => Set<QuestionType>();

        /// <summary>Набор дисциплин (таблица disciplines).</summary>
        public DbSet<Discipline> Disciplines => Set<Discipline>();

        /// <summary>Набор профилей преподавателей (таблица teachers).</summary>
        public DbSet<Teacher> Teachers => Set<Teacher>();

        /// <summary>Набор учётных записей (проекция таблицы users).</summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>Реестр фактов голосования (таблица voter_checkins).</summary>
        public DbSet<VoterCheckin> VoterCheckins => Set<VoterCheckin>();

        /// <summary>Обезличенные бюллетени с ответами (таблица ballots).</summary>
        public DbSet<Ballot> Ballots => Set<Ballot>();

        /// <summary>Ответы на вопросы анкеты (таблица answers).</summary>
        public DbSet<Answer> Answers => Set<Answer>();

        /// <summary>Журнал событий безопасности (таблица audit_events).</summary>
        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        public DbSet<TeacherDiscipline> TeacherDisciplines { get; set; }

        /// <summary>
        /// Настройка связей между сущностями и явное указание имён таблиц.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            // ЯВНОЕ УКАЗАНИЕ ИМЁН ТАБЛИЦ.
            // Без ToTable() EF Core может пытаться плюрализовать имена,
            // создавая "survey_template" вместо "survey_templates".
            // ============================================================
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Teacher>().ToTable("teachers");
            modelBuilder.Entity<Discipline>().ToTable("disciplines");
            modelBuilder.Entity<PeriodStatus>().ToTable("period_statuses");
            modelBuilder.Entity<SurveyStatus>().ToTable("survey_statuses");
            modelBuilder.Entity<QuestionType>().ToTable("question_types");
            modelBuilder.Entity<Period>().ToTable("periods");
            modelBuilder.Entity<Question>().ToTable("questions");
            modelBuilder.Entity<SurveyTemplate>().ToTable("survey_templates");
            modelBuilder.Entity<VoterCheckin>().ToTable("voter_checkins");
            modelBuilder.Entity<Ballot>().ToTable("ballots");
            modelBuilder.Entity<Answer>().ToTable("answers");
            modelBuilder.Entity<TeacherDiscipline>().HasKey(td => new { td.TeacherId, td.DisciplineId });
            // Настройка таблицы аудита
            modelBuilder.Entity<AuditEvent>(e =>
            {
                e.ToTable("audit_events");
                // Указываем, что колонка Details имеет тип jsonb (как в схеме ЛР №2).
                // Без этого Npgsql будет отправлять строку как text, и БД выдаст ошибку 42804.
                e.Property(x => x.Details).HasColumnType("jsonb");
            });

            // ============================================================
            // СВЯЗИ МЕЖДУ СУЩНОСТЯМИ.
            // ============================================================
            // SurveyTemplate → survey_templates
            modelBuilder.Entity<SurveyTemplate>(e =>
            {
                e.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.PeriodId);
                e.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId);
                e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId);
                e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
                e.HasMany(x => x.Questions).WithOne().HasForeignKey(q => q.SurveyTemplateId);
            });

            // Question → questions
            modelBuilder.Entity<Question>(e =>
            {
                e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
            });

            // Period → periods
            modelBuilder.Entity<Period>(e =>
            {
                e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
            });

            // Teacher → teachers
            modelBuilder.Entity<Teacher>(e =>
            {
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            });

            // Answer → answers
            // Связываем ответ с обезличенным бюллетенем через BallotId.
            // Внешнего ключа на студента (users.id) здесь намеренно НЕТ — это требование анонимности.
            modelBuilder.Entity<Answer>(e =>
            {
                e.HasOne<Ballot>().WithMany().HasForeignKey(x => x.BallotId);
            });
        }
    }
}