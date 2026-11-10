using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO элемента списка анкет в админке (ответ от бэкенда).
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

        /// <summary>Текущий статус анкеты (draft, active, closed).</summary>
        public string StatusCode { get; init; } = string.Empty;

        /// <summary>Количество вопросов в анкете.</summary>
        public int QuestionsCount { get; init; }

        /// <summary>
        /// Количество полученных ответов. 
        /// Если больше 0, редактирование анкеты блокируется на сервере.
        /// </summary>
        public int AnswersCount { get; init; }
    }

    /// <summary>
    /// DTO запроса на создание или редактирование шаблона анкеты.
    /// </summary>
    public class SaveSurveyTemplateRequest
    {
        /// <summary>Идентификатор периода, к которому привязывается анкета.</summary>
        public Guid PeriodId { get; set; }

        /// <summary>Идентификатор дисциплины.</summary>
        public Guid DisciplineId { get; set; }

        /// <summary>Идентификатор преподавателя.</summary>
        public Guid TeacherId { get; set; }

        /// <summary>Список вопросов анкеты.</summary>
        public List<QuestionInputDto> Questions { get; set; } = new();
    }

    /// <summary>
    /// DTO вопроса при сохранении анкеты.
    /// </summary>
    public class QuestionInputDto
    {
        /// <summary>
        /// Идентификатор вопроса. Если null (или 0), значит это новый вопрос.
        /// </summary>
        public long? Id { get; set; }

        /// <summary>Текст вопроса.</summary>
        [Required(ErrorMessage = "Текст вопроса обязателен")]
        public string Text { get; set; } = string.Empty;

        /// <summary>Тип вопроса: 1 - scale (оценка), 2 - text (комментарий).</summary>
        public short TypeId { get; set; }
    }

    /// <summary>
    /// DTO для формы редактирования анкеты (ответ от бэкенда).
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

        /// <summary>Список вопросов анкеты.</summary>
        public List<QuestionDto> Questions { get; init; } = new();
    }

    /// <summary>
    /// Универсальный элемент справочника для выпадающих списков.
    /// </summary>
    public class DictionaryItemDto
    {
        /// <summary>Идентификатор записи.</summary>
        public Guid Id { get; init; }

        /// <summary>Название для отображения (ФИО или имя дисциплины).</summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO связи преподавателя и дисциплины.
    /// </summary>
    public class TeacherDisciplineMapDto
    {
        /// <summary>Идентификатор преподавателя.</summary>
        public Guid TeacherId { get; init; }

        /// <summary>Список идентификаторов дисциплин, которые ведет этот преподаватель.</summary>
        public List<Guid> DisciplineIds { get; init; } = new();
    }

    /// <summary>
    /// Набор справочников для формы создания анкеты.
    /// </summary>
    public class AdminDictionariesDto
    {
        /// <summary>Список доступных дисциплин.</summary>
        public List<DictionaryItemDto> Disciplines { get; init; } = new();

        /// <summary>Список преподавателей.</summary>
        public List<DictionaryItemDto> Teachers { get; init; } = new();

        /// <summary>Список периодов в статусе draft, в которые можно добавить анкету.</summary>
        public List<DictionaryItemDto> DraftPeriods { get; init; } = new();

        /// <summary>
        /// Связи преподавателей и дисциплин (кто что ведет).
        /// Используется в JS для фильтрации выпадающего списка дисциплин.
        /// </summary>
        public List<TeacherDisciplineMapDto> TeacherDisciplines { get; init; } = new();
    }
}