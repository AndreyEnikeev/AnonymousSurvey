namespace Analytics.Models
{
    /// <summary>Ответ на вопрос (таблица answers).</summary>
    public class Answer
    {
        /// <summary>Уникальный идентификатор ответа.</summary>
        public long Id { get; set; }

        /// <summary>Идентификатор бюллетеня.</summary>
        public Guid BallotId { get; set; }

        /// <summary>Идентификатор вопроса.</summary>
        public long QuestionId { get; set; }

        /// <summary>Числовое значение (для шкалы 1-5).</summary>
        public short? ValueInt { get; set; }

        /// <summary>Текстовое значение (для комментариев).</summary>
        public string? ValueText { get; set; }
    }
}