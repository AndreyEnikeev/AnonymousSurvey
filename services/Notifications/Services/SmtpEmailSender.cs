using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Notifications.Services
{
    /// <summary>
    /// SMTP-отправитель на базе MailKit.
    /// 
    /// В dev-окружении письма уходят в MailHog (localhost:1025),
    /// перехватываются и отображаются в его веб-интерфейсе (http://localhost:8025).
    /// В продакшене тот же код будет работать с реальным SMTP-сервером,
    /// если поменять хост/порт/учётные данные в User Secrets.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _logger;

        /// <summary>Конструктор: конфигурация и логгер внедряются через DI.</summary>
        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            var host = _config["Smtp:Host"] ?? "localhost";
            var port = int.Parse(_config["Smtp:Port"] ?? "1025");
            var from = _config["Smtp:FromAddress"] ?? "noreply@anon-survey.local";
            var fromName = _config["Smtp:FromName"] ?? "AnonymousSurvey";
            var useSsl = bool.Parse(_config["Smtp:UseSsl"] ?? "false");
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            // MailHog работает без TLS — используем SecureSocketOptions.None.
            await client.ConnectAsync(
                host, port,
                useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogDebug("Письмо отправлено на {To} с темой «{Subject}»", to, subject);
        }
    }
}