namespace MiniPdm.Application.Cad;

/// <summary>
/// Перечисляет документы CAD-системы в папке выгрузки.
/// </summary>
/// <remarks>
/// Вынесено отдельно от <see cref="ICadDocumentReader"/>, чтобы логика импорта
/// не знала о файловой системе и не зависела от расширений файлов.
/// </remarks>
public interface ICadDocumentCatalog
{
    /// <summary>Возвращает список документов, доступных для импорта.</summary>
    /// <param name="folderPath">Путь к папке выгрузки CAD-системы.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<CadDocumentRef>> ListAsync(string folderPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ссылка на документ в папке выгрузки.
/// </summary>
/// <param name="FileName">Имя файла — идентификатор документа.</param>
/// <param name="Path">Полный путь к файлу.</param>
public sealed record CadDocumentRef(string FileName, string Path);