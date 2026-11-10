using LlmOrchestrator.Contracts;
using LlmOrchestrator.DataDb;
using LlmOrchestrator.Models;
using LlmOrchestrator.Services;
using MassTransit;

namespace LlmOrchestrator.Consumers
{
    /// <summary>
    /// Обработчик событий из RabbitMQ.
    /// Получает задачу, пытается отправить её в LLM, а при сбое логирует ошибку.
    /// </summary>
    public class LlmClassificationConsumer : IConsumer<LlmClassificationRequested>
    {
        private readonly LlmDbContext _db;
        private readonly LlmClient _llmClient;
        private readonly ILogger<LlmClassificationConsumer> _logger;

        /// <summary>
        /// Конструктор консьюмера.
        /// </summary>
        public LlmClassificationConsumer(LlmDbContext db, LlmClient llmClient, ILogger<LlmClassificationConsumer> logger)
        {
            _db = db;
            _llmClient = llmClient;
            _logger = logger;
        }

        /// <summary>
        /// Обрабатывает входящее сообщение.
        /// </summary>
        public async Task Consume(ConsumeContext<LlmClassificationRequested> context)
        {
            var msg = context.Message;
            _logger.LogInformation("Получена задача на классификацию для AnswerId={AnswerId}", msg.AnswerId);

            try
            {
                // Пытаемся обратиться к внешнему LLM (который сейчас недоступен).
                var result = await _llmClient.ClassifyAsync(msg.CommentText, context.CancellationToken);

                // Если бы LLM работал, мы бы сохранили результат в _db.LlmClassifications.
                // (Этот код опущен, так как LLM не подключен).
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("Не удалось классифицировать ответ {AnswerId}. Сервис LLM недоступен.", msg.AnswerId);

                // Записываем факт сбоя в журнал аудита.
                _db.AuditEvents.Add(new AuditEvent
                {
                    UserId = null,
                    EventType = "llm_classification_failed",
                    EntityType = "answer",
                    EntityId = msg.AnswerId.ToString(),
                    Details = $"{{\"error\":\"{ex.Message}\"}}",
                    IpAddress = "127.0.0.1", // Заглушка для фонового процесса
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(context.CancellationToken);

                // Пробрасываем ошибку, чтобы MassTransit применил политику ретраев (повторных попыток).
                throw;
            }
        }
    }
}