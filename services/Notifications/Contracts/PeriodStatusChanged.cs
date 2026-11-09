using MassTransit;

namespace Notifications.Contracts
{
    /// <summary>
    /// Событие смены статуса периода анкетирования.
    /// 
    /// Копия контракта из сервиса Surveys (C-04). Структура должна совпадать бит-в-бит.
    /// Атрибут [EntityName] жёстко фиксирует имя exchange в RabbitMQ
    /// ("PeriodStatusChanged"), потому что MassTransit по умолчанию использует
    /// полное имя типа (namespace + имя), и при разных namespace в Surveys и Notifications
    /// сообщения бы не доходили.
    /// </summary>
    /// <param name="PeriodId">Идентификатор периода.</param>
    /// <param name="PeriodName">Человекочитаемое название периода (например, "Осень 2026").</param>
    /// <param name="OldStatus">Предыдущий статус: draft / open / closed / archived.</param>
    /// <param name="NewStatus">Новый статус.</param>
    /// <param name="UpdatedAt">Время изменения в UTC.</param>
    [EntityName("PeriodStatusChanged")]
    [MessageUrn("AnonymousSurvey.Contracts:PeriodStatusChanged")]
    public record PeriodStatusChanged(
        Guid PeriodId,
        string PeriodName,
        string OldStatus,
        string NewStatus,
        DateTime UpdatedAt
    );
}