namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO элемента списка анкет. Совпадает по структуре с ответом
    /// GET /api/surveys/active (O-04) и GET /api/my-surveys (O-06).
    /// Поля из Surveys.Models.Dtos.SurveyListItemDto — копия один в один.
    /// </summary>
    public class SurveyListItemDto
    {
        /// <summary>
        /// Идентификатор анкеты. Используется для перехода на страницу
        /// заполнения /Student/Survey/{id}.
        /// </summary>
        public Guid SurveyId { get; init; }

        /// <summary>
        /// Название анкеты для отображения студенту.
        /// Формируется сервером как «{Дисциплина} — {Преподаватель}».
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Идентификатор периода анкетирования, к которому относится анкета.
        /// </summary>
        public Guid PeriodId { get; init; }

        /// <summary>
        /// Признак: отправлял ли текущий студент эту анкету.
        /// true — в UI показываем бейдж «Отправлено» вместо кнопки «Заполнить».
        /// </summary>
        public bool AlreadySubmitted { get; init; }

        /// <summary>
        /// Название периода анкетирования (например, "Осень 2026").
        /// Расширение контракта O-04/O-06 для удобства отображения (D-17).
        /// </summary>
        public string PeriodName { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO ответа O-13: полная структура анкеты для заполнения.
    /// Содержит вопросы и признак того, отправлял ли студент её ранее.
    /// </summary>
    public class SurveyDetailsDto
    {
        /// <summary>
        /// Уникальный идентификатор анкеты.
        /// </summary>
        public Guid SurveyId { get; init; }

        /// <summary>
        /// Название анкеты (Дисциплина — Преподаватель) для заголовка страницы.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Идентификатор периода анкетирования.
        /// </summary>
        public Guid PeriodId { get; init; }

        /// <summary>
        /// Текущий статус анкеты: "active" или "closed".
        /// В текущей реализации сервер возвращает только активные анкеты (O-13),
        /// но поле заложено на будущее.
        /// </summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Признак: отправлял ли текущий студент эту анкету ранее.
        /// true — форма заполнения не отображается, вместо неё показывается
        /// сообщение «Вы уже отправили эту анкету». Это UI-удобство;
        /// серверная защита от повторной отправки реализована в O-05.
        /// </summary>
        public bool AlreadySubmitted { get; init; }

        /// <summary>
        /// Список вопросов анкеты.
        /// Каждый вопрос содержит свой тип ("scale" или "text") и, если нужно,
        /// варианты ответа. Пустой список маловероятен на практике,
        /// но безопасен для рендера.
        /// </summary>
        public List<QuestionDto> Questions { get; init; } = new();
    }

    /// <summary>
    /// DTO одного вопроса внутри анкеты (часть ответа O-13).
    /// </summary>
    public class QuestionDto
    {
        /// <summary>
        /// Уникальный идентификатор вопроса (bigint в БД).
        /// Используется для привязки ответа к вопросу при отправке (O-05).
        /// </summary>
        public long QuestionId { get; init; }

        /// <summary>
        /// Текст вопроса, отображаемый пользователю.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Тип вопроса:
        ///   "scale" — оценка по шкале 1–5 (радиокнопки);
        ///   "text"  — свободный текст (textarea, до 2000 символов).
        /// Определяет, какой HTML-элемент рендерить на странице.
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Варианты ответа для типа "scale" (обычно [1, 2, 3, 4, 5]).
        /// Для типа "text" — null, так как вариантов нет.
        /// </summary>
        public List<int>? Options { get; init; }
    }

    /// <summary>
    /// DTO запроса O-05: заполненная анкета, готовая к отправке.
    /// </summary>
    public class SubmitSurveyRequest
    {
        /// <summary>
        /// Список ответов студента. По одному элементу на каждый вопрос,
        /// на который дан ответ. Если вопрос текстовый и оставлен пустым,
        /// сервер сам отфильтрует пустые значения.
        /// </summary>
        public List<AnswerItem> Answers { get; init; } = new();
    }

    /// <summary>
    /// Один ответ на вопрос внутри SubmitSurveyRequest.
    /// </summary>
    public class AnswerItem
    {
        /// <summary>
        /// Идентификатор вопроса, на который дан ответ.
        /// Должен принадлежать анкете, указанной в URL.
        /// </summary>
        public long QuestionId { get; init; }

        /// <summary>
        /// Значение ответа в виде строки:
        ///   для "scale" — число от "1" до "5";
        ///   для "text"  — сам текст комментария (до 2000 символов).
        /// Сервер сам валидирует: диапазон для scale, длину для text.
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO ответа O-05: результат успешной отправки анкеты.
    /// </summary>
    public class SubmitSurveyResponse
    {
        /// <summary>
        /// Идентификатор созданного обезличенного бюллетеня (ballots.id).
        /// НЕ равен student_id — по правилам анонимности связь не хранится.
        /// </summary>
        public Guid SubmissionId { get; init; }

        /// <summary>
        /// Момент, когда анкета была принята сервером (UTC).
        /// </summary>
        public DateTime SubmittedAt { get; init; }

        /// <summary>
        /// Статус обработки. В текущей реализации всегда "accepted".
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }
}