using System.ComponentModel.DataAnnotations.Schema;

namespace Surveys.Models
{
    /// <summary>
    /// Связующая таблица «Преподаватель — Дисциплина» (многие ко многим).
    /// Позволяет определить, какие дисциплины ведет каждый преподаватель.
    /// </summary>
    [Table("teacher_disciplines")]
    public class TeacherDiscipline
    {
        /// <summary>Идентификатор преподавателя.</summary>
        [Column("teacher_id")]
        public Guid TeacherId { get; set; }

        /// <summary>Идентификатор дисциплины.</summary>
        [Column("discipline_id")]
        public Guid DisciplineId { get; set; }
    }
}