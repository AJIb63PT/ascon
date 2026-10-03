using MiniPdm.Application.Cad;
using MiniPdm.Domain;

namespace MiniPdm.Application.Import;

/// <summary>
/// Результат обработки одного документа выгрузки.
/// </summary>
/// <param name="FileName">Имя файла — идентификатор документа.</param>
/// <param name="Severity">Итог обработки.</param>
/// <param name="Reason">Пояснение: причина отклонения или текст предупреждения.</param>
/// <param name="Type">Тип документа, если он был распознан.</param>
/// <param name="Designation">Обозначение, если оно было распознано.</param>
/// <param name="Name">Наименование, если оно было распознано.</param>
/// <param name="ObjectId">Идентификатор созданного или обновлённого объекта.</param>
/// <param name="VersionNo">Номер записанной версии, если версия была создана или изменена.</param>
public sealed record ImportOutcome(
    string FileName,
    ImportSeverity Severity,
    string? Reason,
    CadDocumentType? Type = null,
    string? Designation = null,
    string? Name = null,
    long? ObjectId = null,
    int? VersionNo = null)
{
    /// <summary>Возвращает <c>true</c>, если документ попал в базу.</summary>
    public bool IsImported => Severity != ImportSeverity.Error;
}