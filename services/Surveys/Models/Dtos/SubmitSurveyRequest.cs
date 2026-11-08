using System.Collections.Generic;

namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO запроса O-05: отправка заполненной анкеты.
    /// </summary>
    public class SubmitSurveyRequest
    {
        /// <summary>Список ответов студента на вопросы анкеты.</summary>
        public List<AnswerItem> Answers { get; init; } = new();
    }

    /// <summary>
    /// Один ответ на вопрос в запросе O-05.
    /// </summary>
    public class AnswerItem
    {
        /// <summary>Идентификатор вопроса, на который дан ответ.</summary>
        public long QuestionId { get; init; }

        /// <summary>
        /// Значение ответа. Если вопрос типа "scale" — тут будет "5".
        /// Если вопрос типа "text" — тут будет сам текст комментария.
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }
}