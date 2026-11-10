using MassTransit;

namespace LlmOrchestrator.Contracts
{
    /// <summary>
    /// Событие, получаемое из RabbitMQ от сервиса Surveys (C-04).
    /// Содержит задачу на классификацию текстового ответа.
    /// </summary>
    /// <param name="AnswerId">Идентификатор ответа, к которому относится комментарий.</param>
    /// <param name="CommentText">Обезличенный текст комментария.</param>
    [EntityName("LlmClassificationRequested")]
    [MessageUrn("Surveys.Contracts:LlmClassificationRequested")]
    public record LlmClassificationRequested(
        long AnswerId,
        string CommentText
    );
}