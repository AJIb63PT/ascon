namespace MiniPdm.Application.Cad;

/// <summary>
/// Читает документ CAD-системы по пути.
/// </summary>
/// <remarks>
/// Единственная точка, где прикладной код касается внешнего формата.
/// Импорт тестируется с подменой этой зависимости, без обращения к диску.
/// </remarks>
public interface ICadDocumentReader
{
    /// <summary>Читает документ.</summary>
    /// <param name="path">Путь к файлу документа.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="CadDocumentReadException">Файл не удалось прочитать или разобрать.</exception>
    Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken = default);
}