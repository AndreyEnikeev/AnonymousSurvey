using MassTransit;

namespace Notifications.Contracts
{
    /// <summary>
    /// Копия контракта из Auth (C-03). Поля должны совпадать бит-в-бит.
    /// Атрибуты [EntityName] и [MessageUrn] фиксируют имя exchange — 
    /// это единственный способ доставить сообщение между разными namespace.
    /// </summary>
    [EntityName("PasswordResetRequested")]
    [MessageUrn("AnonymousSurvey.Contracts:PasswordResetRequested")]
    public record PasswordResetRequested(
        Guid UserId,
        string Email,
        string FullName,
        string ResetToken,
        DateTime ExpiresAt
    );
}