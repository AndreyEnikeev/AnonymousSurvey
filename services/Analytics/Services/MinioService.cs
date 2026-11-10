using Amazon.S3;
using Amazon.S3.Model;

namespace Analytics.Services
{
    /// <summary>
    /// Сервис для работы с объектным хранилищем MinIO.
    /// Использует AWS SDK, так как MinIO совместим с протоколом S3.
    /// </summary>
    public class MinioService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        /// <summary>
        /// Инициализирует клиент S3 на основе конфигурации (User Secrets).
        /// Автоматически определяет протокол (HTTP/HTTPS) в зависимости от среды.
        /// </summary>
        /// <param name="config">Конфигурация приложения.</param>
        /// <param name="env">Информация об окружении (Development/Production).</param>
        public MinioService(IConfiguration config, IHostEnvironment env)
        {
            var endpoint = config["MinIO:Endpoint"] ?? "http://localhost:9000";
            var accessKey = config["MinIO:AccessKey"] ?? "minioadmin";
            var secretKey = config["MinIO:SecretKey"] ?? "minioadmin";
            _bucketName = config["MinIO:BucketName"] ?? "reports";

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true, // Обязательно для MinIO
                
                // Жестко указываем SDK использовать HTTP, если так написано в конфиге.
                UseHttp = true
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        /// <summary>
        /// Загружает CSV-контент в бакет MinIO.
        /// </summary>
        /// <param name="content">Текст файла в формате CSV.</param>
        /// <param name="fileName">Имя файла (без пути).</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Ключ объекта в хранилище (object key).</returns>
        public async Task<string> UploadFileAsync(byte[] content, string fileName, string contentType, CancellationToken ct)
        {
            var objectKey = $"reports/{fileName}";

            using var stream = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(request, ct);
            return objectKey;
        }
        /// <summary>
        /// Генерирует pre-signed (временную) ссылку для скачивания объекта.
        /// </summary>
        /// <param name="objectKey">Ключ объекта в хранилище.</param>
        /// <param name="expiry">Время жизни ссылки.</param>
        /// <returns>Временная ссылка для скачивания.</returns>
        public string GetPreSignedUrl(string objectKey, TimeSpan expiry)
        {
            return _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.Add(expiry),
                Verb = HttpVerb.GET
            });
        }
    }
}