namespace Surveys.Models
{
    /// <summary>
    /// Ответ на конкретный вопрос (таблица answers).
    /// Все ответы (и оценки, и тексты) хранятся здесь и привязаны к обезличенному BallotId.
    /// </summary>
    public class Answer
    {
        /// <summary>Уникальный идентификатор ответа (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Ссылка на обезличенный бюллетень (ballots.id).</summary>
        public Guid BallotId { get; set; }

        /// <summary>Ссылка на вопрос, на который дан ответ (questions.id).</summary>
        public long QuestionId { get; set; }

        /// <summary>
        /// Числовой ответ. Используется для вопросов типа "scale" (оценка от 1 до 5).
        /// Для текстовых вопросов здесь null.
        /// </summary>
        public short? ValueInt { get; set; }

        /// <summary>
        /// Текстовый ответ. Используется для вопросов типа "text" (комментарии).
        /// Для вопросов с оценкой здесь null.
        /// </summary>
        public string? ValueText { get; set; }
    }
}