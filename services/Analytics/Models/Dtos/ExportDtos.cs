namespace Analytics.Models.Dtos
{
    /// <summary>
    /// Запрос на формирование выгрузки отчета (DTO).
    /// Используется в теле POST-запроса O-10.
    /// </summary>
    /// <param name="PeriodId">Идентификатор периода, за который формируется отчет.</param>
    /// <param name="Format">Формат файла: "csv" (по умолчанию).</param>
    public record ExportRequestDto(
        /// <summary>
        /// Идентификатор периода анкетирования.
        /// </summary>
        Guid PeriodId,

        /// <summary>
        /// Формат выгружаемого файла.
        /// </summary>
        string Format = "csv"
    );

    /// <summary>
    /// Ответ с информацией о сформированной выгрузке (DTO).
    /// Возвращается клиенту после успешного создания файла.
    /// </summary>
    public class ExportResponseDto
    {
        /// <summary>
        /// Уникальный идентификатор записи о выгрузке.
        /// </summary>
        public Guid ExportId { get; init; }

        /// <summary>
        /// Временная (pre-signed) ссылка для скачивания файла из MinIO.
        /// </summary>
        public string DownloadUrl { get; init; } = string.Empty;

        /// <summary>
        /// Момент времени, когда ссылка на скачивание перестанет быть действительной (UTC).
        /// </summary>
        public DateTime ExpiresAt { get; init; }

        /// <summary>
        /// Текущий статус выгрузки: "ready" (готово) или "processing" (в обработке).
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }
}