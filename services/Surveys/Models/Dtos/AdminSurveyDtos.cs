using System.ComponentModel.DataAnnotations;

namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO для отображения списка шаблонов анкет в административной панели.
    /// Используется в методе GetAll контроллера AdminSurveysController.
    /// </summary>
    public class AdminSurveyListDto
    {
        /// <summary>Уникальный идентификатор шаблона анкеты.</summary>
        public Guid SurveyId { get; init; }

        /// <summary>Название периода анкетирования (например, "Осень 2026").</summary>
        public string PeriodName { get; init; } = string.Empty;

        /// <summary>Название дисциплины.</summary>
        public string DisciplineName { get; init; } = string.Empty;

        /// <summary>ФИО преподавателя.</summary>
        public string TeacherName { get; init; } = string.Empty;

        /// <summary>Строковый код статуса анкеты: "draft", "active" или "closed".</summary>
        public string StatusCode { get; init; } = string.Empty;

        /// <summary>Количество вопросов в анкете.</summary>
        public int QuestionsCount { get; init; }

        /// <summary>Количество отправленных ответов (голосов). Нужно, чтобы блокировать редактирование.</summary>
        public int AnswersCount { get; init; }
    }

    /// <summary>
    /// DTO запроса на создание или редактирование шаблона анкеты.
    /// </summary>
    public class SaveSurveyTemplateRequest
    {
        /// <summary>Идентификатор периода, к которому привязывается анкета.</summary>
        [Required]
        public Guid PeriodId { get; set; }

        /// <summary>Идентификатор дисциплины.</summary>
        [Required]
        public Guid DisciplineId { get; set; }

        /// <summary>Идентификатор преподавателя.</summary>
        [Required]
        public Guid TeacherId { get; set; }

        /// <summary>Список вопросов, которые нужно сохранить (создать или обновить).</summary>
        [Required]
        public List<QuestionInputDto> Questions { get; set; } = new();
    }

    /// <summary>
    /// DTO вопроса при сохранении анкеты.
    /// </summary>
    public class QuestionInputDto
    {
        /// <summary>
        /// Идентификатор вопроса.
        /// Если null или 0 — будет создан новый вопрос.
        /// Если передан — существующий вопрос будет обновлен.
        /// </summary>
        public long? Id { get; set; }

        /// <summary>Текст вопроса.</summary>
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        /// <summary>Тип вопроса: 1 - scale (оценка 1-5), 2 - text (свободный текст).</summary>
        [Required]
        public short TypeId { get; set; }
    }

    /// <summary>
    /// DTO для возврата полной структуры анкеты при открытии формы редактирования.
    /// </summary>
    public class AdminSurveyDetailsDto
    {
        /// <summary>Идентификатор анкеты.</summary>
        public Guid SurveyId { get; init; }

        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Идентификатор дисциплины.</summary>
        public Guid DisciplineId { get; init; }

        /// <summary>Идентификатор преподавателя.</summary>
        public Guid TeacherId { get; init; }

        /// <summary>Текущий статус анкеты.</summary>
        public string StatusCode { get; init; } = string.Empty;

        /// <summary>Список вопросов для отображения в форме редактирования.</summary>
        public List<QuestionDto> Questions { get; init; } = new();
    }

    /// <summary>
    /// Элемент справочника (дисциплина, преподаватель или период).
    /// </summary>
    public class DictionaryItemDto
    {
        /// <summary>Идентификатор записи.</summary>
        public Guid Id { get; init; }

        /// <summary>Название для отображения в выпадающем списке.</summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO для получения справочников при создании анкеты.
    /// </summary>
    public class AdminDictionariesDto
    {
        /// <summary>Список доступных дисциплин.</summary>
        public List<DictionaryItemDto> Disciplines { get; init; } = new();

        /// <summary>Список преподавателей (ФИО).</summary>
        public List<DictionaryItemDto> Teachers { get; init; } = new();

        /// <summary>Список периодов, в которые еще можно добавлять анкеты (только draft).</summary>
        public List<DictionaryItemDto> DraftPeriods { get; init; } = new();

        /// <summary>Связи преподавателей и дисциплин (кто что ведет).</summary>
        public List<TeacherDisciplineMapDto> TeacherDisciplines { get; init; } = new();
    }

    /// <summary>
    /// DTO связи преподавателя и дисциплины для админки.
    /// </summary>
    public class TeacherDisciplineMapDto
    {
        /// <summary>Идентификатор преподавателя.</summary>
        public Guid TeacherId { get; init; }

        /// <summary>Список идентификаторов дисциплин.</summary>
        public List<Guid> DisciplineIds { get; init; } = new();
    }
    /// <summary>
    /// DTO запроса на смену статуса анкеты (например, публикация из draft в active).
    /// </summary>
    public class ChangeSurveyStatusRequest
    {
        /// <summary>
        /// Новый идентификатор статуса.
        /// 1 - draft (черновик)
        /// 2 - active (в работе)
        /// 3 - closed (закрыта)
        /// </summary>
        public short StatusId { get; init; }
    }
}