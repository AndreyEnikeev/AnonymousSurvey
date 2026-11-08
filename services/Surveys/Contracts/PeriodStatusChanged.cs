namespace Surveys.Contracts
{
    /// <summary>
    /// Событие, публикуемое в RabbitMQ при успешной смене статуса периода.
    /// Сервис уведомлений (C-07) будет слушать это событие и рассылать письма.
    /// </summary>
    /// <param name="PeriodId">Идентификатор периода, статус которого изменился.</param>
    /// <param name="OldStatus">Предыдущий статус (например, "open").</param>
    /// <param name="NewStatus">Новый статус (например, "closed").</param>
    /// <param name="UpdatedAt">Время изменения в UTC.</param>
    public record PeriodStatusChanged(
        Guid PeriodId,
        string OldStatus,
        string NewStatus,
        DateTime UpdatedAt
    );
}