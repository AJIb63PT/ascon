using MiniPdm.Application.Cad;

namespace MiniPdm.Infrastructure.Cad;

/// <summary>
/// Каталог документов выгрузки CAD-системы, основанный на файловой системе.
/// </summary>
/// <remarks>
/// Отбирает только файлы расширений сборок и деталей (<c>.a3d</c>, <c>.m3d</c>),
/// поэтому сопроводительные файлы вроде <c>README.txt</c> в импорт не попадают.
/// </remarks>
public sealed class FileSystemCadDocumentCatalog : ICadDocumentCatalog
{
    private static readonly HashSet<string> DocumentExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".a3d", ".m3d" };

    /// <inheritdoc />
    public Task<IReadOnlyList<CadDocumentRef>> ListAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");
        }

        var documents = Directory
            .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => DocumentExtensions.Contains(Path.GetExtension(path)))
            .Select(path => new CadDocumentRef(Path.GetFileName(path), path))
            .OrderBy(document => document.FileName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<CadDocumentRef>>(documents);
    }
}