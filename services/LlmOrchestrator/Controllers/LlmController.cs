using LlmOrchestrator.Models.Dtos;
using LlmOrchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LlmOrchestrator.Controllers
{
    /// <summary>
    /// Контроллер для ручного вызова эндпоинта O-11 (классификация).
    /// Используется для демонстрации корректной обработки недоступности внешнего сервиса.
    /// </summary>
    [ApiController]
    public class LlmController : ControllerBase
    {
        private readonly LlmClient _llmClient;

        /// <summary>
        /// Конструктор контроллера.
        /// </summary>
        public LlmController(LlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        /// <summary>
        /// O-11: POST /api/internal/llm/classify
        /// Внутренний эндпоинт для отправки текста на классификацию.
        /// </summary>
        /// <param name="request">Текст комментария.</param>
        /// <returns>Результат классификации или ошибка 503.</returns>
        [HttpPost("/api/internal/llm/classify")]
        public async Task<IActionResult> Classify([FromBody] LlmRequestDto request)
        {
            try
            {
                var result = await _llmClient.ClassifyAsync(request.Text, CancellationToken.None);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                // Возвращаем 503 Service Unavailable, так как внешний LLM недоступен.
                return StatusCode(503, new
                {
                    message = "Внешний провайдер LLM недоступен.",
                    details = ex.Message
                });
            }
        }
    }
}