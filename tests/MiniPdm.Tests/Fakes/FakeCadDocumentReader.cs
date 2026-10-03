using MiniPdm.Application.Cad;

namespace MiniPdm.Tests.Fakes;

/// <summary>
/// Читатель документов в памяти. Позволяет проверять импорт без файловой системы,
/// в том числе подменять содержимое документа между вызовами — так проверяется
/// повторный импорт.
/// </summary>
public sealed class FakeCadDocumentReader : ICadDocumentReader
{
    private readonly Dictionary<string, CadDocument> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);

    /// <summary>Документ, который нужно вернуть для указанного файла.</summary>
    public FakeCadDocumentReader WithDocument(CadDocument document)
    {
        _documents[document.FileName] = document;
        return this;
    }

    /// <summary>Документ, который нужно вернуть, с явным указанием имени файла.</summary>
    public FakeCadDocumentReader WithDocument(string fileName, CadDocument document)
    {
        _documents[fileName] = document with { FileName = fileName };
        return this;
    }

    /// <summary>Ошибка чтения для указанного файла.</summary>
    public FakeCadDocumentReader WithFailure(string fileName, string reason)
    {
        _failures[fileName] = reason;
        return this;
    }

    /// <summary>Количество обращений к ридеру.</summary>
    public int ReadCount { get; private set; }

    /// <inheritdoc />
    public Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;

        var fileName = Path.GetFileName(path);

        if (_failures.TryGetValue(fileName, out var failure))
        {
            throw new CadDocumentReadException(failure);
        }

        if (_documents.TryGetValue(fileName, out var document))
        {
            return Task.FromResult(document);
        }

        throw new CadDocumentReadException($"Файл не зарегистрирован в тесте: {fileName}");
    }
}