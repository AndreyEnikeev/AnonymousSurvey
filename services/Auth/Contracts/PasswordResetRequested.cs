using MassTransit;

namespace Auth.Contracts
{
    /// <summary>
    /// Событие: пользователь запросил восстановление пароля (O-02).
    /// Публикуется сервисом Auth (C-03) в RabbitMQ при успешной генерации
    /// одноразового токена. Получатель — Notifications (C-07), который
    /// отправит письмо в MailHog через IEmailSender.
    ///
    /// ВАЖНО: здесь передаётся ResetToken в открытом виде.
    /// Это допустимо потому, что:
    ///   - RabbitMQ доступен только внутри контура (пароль брокера в User Secrets);
    ///   - токен живёт 15 минут, одноразовый;
    ///   - в БД уже лежит только его SHA-256 хеш (см. AuthController).
    ///
    /// Атрибуты [EntityName] и [MessageUrn] ЖЁСТКО фиксируют имя exchange
    /// и URN сообщения — иначе MassTransit использует namespace + имя класса,
    /// и сообщение не долетит между Auth.Contracts и Notifications.Contracts
    /// (разные namespace = разные имена exchange = сообщение в никуда).
    /// </summary>
    /// <param name="UserId">Идентификатор пользователя (для аудита и корреляции).</param>
    /// <param name="Email">Адрес, на который уйдёт письмо.</param>
    /// <param name="FullName">ФИО для персонализированного обращения.</param>
    /// <param name="ResetToken">Одноразовый токен (plaintext).</param>
    /// <param name="ExpiresAt">Момент истечения токена (UTC).</param>
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