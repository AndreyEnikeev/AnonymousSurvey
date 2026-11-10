namespace WebClient.Models.Dtos
{
    /// <summary>
    /// DTO запроса O-10: формирование отчета.
    /// </summary>
    public class ExportRequestDto
    {
        /// <summary>Идентификатор периода, за который формируется отчет.</summary>
        public Guid PeriodId { get; init; }

        /// <summary>Формат файла (xlsx).</summary>
        public string Format { get; init; } = "xlsx";
    }

    /// <summary>
    /// DTO ответа O-10: результат создания выгрузки.
    /// </summary>
    public class ExportResponseDto
    {
        /// <summary>Идентификатор записи о выгрузке в БД.</summary>
        public Guid ExportId { get; init; }

        /// <summary>
        /// Временная (pre-signed) ссылка на файл в MinIO.
        /// По ней можно скачать файл без авторизации.
        /// </summary>
        public string DownloadUrl { get; init; } = string.Empty;

        /// <summary>Время, когда ссылка перестанет работать.</summary>
        public DateTime ExpiresAt { get; init; }

        /// <summary>Статус выгрузки (ready).</summary>
        public string Status { get; init; } = string.Empty;
    }
}