using LlmOrchestrator.Models.Dtos;
using System.Net.Http.Json;

namespace LlmOrchestrator.Services
{
    /// <summary>
    /// Клиент для взаимодействия с внешним провайдером языковой модели (C-12).
    /// В рамках ЛР №3 реальная модель не подключена, поэтому запросы 
    /// будут уходить на несуществующий адрес, вызывая сетевую ошибку.
    /// </summary>
    public class LlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LlmClient> _logger;
        private readonly string _apiUrl;

        /// <summary>
        /// Конструктор клиента. Внедряет HttpClient и конфигурацию.
        /// </summary>
        /// <param name="httpClient">HTTP-клиент, управляемый фабрикой .NET.</param>
        /// <param name="config">Конфигурация приложения.</param>
        /// <param name="logger">Логгер для записи ошибок.</param>
        public LlmClient(HttpClient httpClient, IConfiguration config, ILogger<LlmClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            // По умолчанию указываем порт 9999, где гарантированно ничего нет.
            _apiUrl = config["LlmProvider:ApiUrl"] ?? "http://localhost:9999/api/classify";
        }

        /// <summary>
        /// Отправляет обезличенный текст на классификацию внешнему провайдеру.
        /// </summary>
        /// <param name="text">Текст комментария.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат классификации (если провайдер доступен).</returns>
        /// <exception cref="HttpRequestException">Выбрасывается, если провайдер недоступен.</exception>
        public async Task<LlmResponseDto> ClassifyAsync(string text, CancellationToken ct)
        {
            _logger.LogInformation("Отправка запроса к внешнему LLM (URL: {Url})...", _apiUrl);

            try
            {
                var requestDto = new LlmRequestDto { Text = text };

                // Пытаемся сделать POST-запрос по несуществующему адресу (порт 9999).
                var response = await _httpClient.PostAsJsonAsync(_apiUrl, requestDto, ct);

                // Если сервер вдруг ответит ошибкой (500, 503), EnsureSuccessStatusCode выбросит исключение.
                response.EnsureSuccessStatusCode();

                // Парсим успешный ответ (сработает только если LLM реально запущен).
                var result = await response.Content.ReadFromJsonAsync<LlmResponseDto>(cancellationToken: ct);
                return result ?? throw new InvalidOperationException("Пустой ответ от LLM");
            }
            catch (HttpRequestException ex)
            {
                // Логируем сетевую ошибку (сервис недоступен).
                _logger.LogError(ex, "Внешний провайдер LLM недоступен! Ошибка: {Message}", ex.Message);
                throw; // Пробрасываем ошибку наверх, чтобы обработать её в контроллере или консьюмере.
            }
        }
    }
}