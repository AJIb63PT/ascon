using MiniPdm.Application.Cad;

namespace MiniPdm.Tests.Fakes;

/// <summary>
/// Каталог документов в памяти: возвращает заранее заданный набор.
/// </summary>
public sealed class FakeCadDocumentCatalog : ICadDocumentCatalog
{
    private readonly List<CadDocumentRef> _documents = new();

    /// <summary>Папка, которая будет запрошена последней.</summary>
    public string? LastFolderPath { get; private set; }

    /// <summary>Добавляет документ в каталог.</summary>
    public FakeCadDocumentCatalog Add(string fileName)
    {
        _documents.Add(new CadDocumentRef(fileName, $"/fake/{fileName}"));
        return this;
    }

    /// <summary>Задаёт папку, в которой перечисление должно завершиться ошибкой.</summary>
    public string? ThrowOnFolderPath { get; set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<CadDocumentRef>> ListAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        LastFolderPath = folderPath;

        if (ThrowOnFolderPath is not null && folderPath == ThrowOnFolderPath)
        {
            throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");
        }

        return Task.FromResult<IReadOnlyList<CadDocumentRef>>(_documents.ToArray());
    }
}