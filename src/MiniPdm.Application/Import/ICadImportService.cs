namespace MiniPdm.Application.Import;

/// <summary>
/// Импортирует папку выгрузки CAD-системы.
/// </summary>
public interface ICadImportService
{
    /// <summary>
    /// Импортирует все документы из папки.
    /// </summary>
    /// <remarks>
    /// Ошибка отдельного документа не прерывает импорт: он попадает в отчёт.
    /// Все изменения применяются одной транзакцией.
    /// </remarks>
    /// <param name="folderPath">Папка выгрузки.</param>
    /// <param name="progress">Ход выполнения для интерфейса.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<ImportReport> ImportAsync(
        string folderPath,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Состояние процесса импорта.
/// </summary>
/// <param name="Processed">Обработано файлов.</param>
/// <param name="Total">Всего файлов.</param>
/// <param name="CurrentFileName">Текущий файл.</param>
public sealed record ImportProgress(int Processed, int Total, string CurrentFileName)
{
    /// <summary>Доля выполнения от 0 до 1.</summary>
    public double Ratio => Total == 0 ? 0d : (double)Processed / Total;
}