namespace Notifications.Services
{
    /// <summary>
    /// Абстракция отправки email-сообщений.
    /// 
    /// Позволяет подменять реализацию в тестах (мок вместо реального SMTP)
    /// и при смене транспорта (SMTP → HTTP API почтового провайдера).
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Отправляет одно письмо.
        /// </summary>
        /// <param name="to">Адрес получателя.</param>
        /// <param name="subject">Тема письма.</param>
        /// <param name="htmlBody">Тело письма в HTML.</param>
        /// <param name="ct">Токен отмены.</param>
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    }
}