namespace Surveys.Models
{
    /// <summary>
    /// Реестр фактов голосования (таблица voter_checkins).
    /// Хранит «кто голосовал», но НЕ «что ответил» — это ключ к анонимности.
    /// </summary>
    public class VoterCheckin
    {
        /// <summary>Уникальный идентификатор записи (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Ссылка на студента, который голосовал (users.id).</summary>
        public Guid StudentId { get; set; }

        /// <summary>Ссылка на шаблон анкеты, по которому голосовали.</summary>
        public Guid SurveyTemplateId { get; set; }

        /// <summary>Признак: студент уже отправил эту анкету.</summary>
        public bool HasVoted { get; set; }
    }
}