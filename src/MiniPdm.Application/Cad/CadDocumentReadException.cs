namespace MiniPdm.Application.Cad;

/// <summary>
/// Документ не удалось прочитать: файл отсутствует, недоступен или содержит
/// некорректные данные. Импорт не прерывается — ошибка попадает в отчёт.
/// </summary>
public sealed class CadDocumentReadException : Exception
{
    /// <summary>Создаёт исключение.</summary>
    public CadDocumentReadException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с указанием исходной причины.</summary>
    public CadDocumentReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}