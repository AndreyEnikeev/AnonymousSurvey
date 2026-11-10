using System;

namespace Surveys.Models.Dtos
{
    /// <summary>
    /// DTO ответа O-08: информация о периоде анкетирования.
    /// </summary>
    public class PeriodDto
    {
        /// <summary>Идентификатор периода.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Название периода (например, "Осенний семестр 2026").</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Дата начала периода.</summary>
        public DateOnly StartDate { get; init; }

        /// <summary>Дата окончания периода.</summary>
        public DateOnly EndDate { get; init; }

        /// <summary>Текущий статус (draft / open / closed / archived).</summary>
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>
    /// DTO запроса на создание нового периода анкетирования (D-20).
    /// Период всегда создаётся в статусе draft.
    /// </summary>
    public class CreatePeriodRequest
    {
        /// <summary>
        /// Название периода (например, "Весна 2027").
        /// Обязательное поле.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Дата начала периода.
        /// Используется тип DateOnly, так как в БД это колонка DATE (без времени).
        /// </summary>
        public DateOnly StartDate { get; init; }

        /// <summary>
        /// Дата окончания периода.
        /// Должна быть не раньше StartDate (валидация на сервере).
        /// </summary>
        public DateOnly EndDate { get; init; }
    }
}