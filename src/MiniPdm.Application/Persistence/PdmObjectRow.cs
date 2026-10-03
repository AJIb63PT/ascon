using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>
/// Объект PDM в терминах хранилища.
/// </summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Type">Тип объекта.</param>
/// <param name="Designation">Обозначение; <c>null</c> у стандартных изделий.</param>
/// <param name="Name">Наименование.</param>
/// <param name="SourceFileName">Имя файла в выгрузке CAD-системы — внешний идентификатор документа.</param>
/// <param name="CurrentVersionId">Текущая версия; <c>null</c>, если версий ещё нет.</param>
public sealed record PdmObjectRow(
    long Id,
    PdmObjectType Type,
    string? Designation,
    string Name,
    string? SourceFileName,
    long? CurrentVersionId)
{
    /// <summary>Ключ для отображения: обозначение, а у стандартных изделий — наименование.</summary>
    public string DisplayKey => string.IsNullOrWhiteSpace(Designation) ? Name : Designation;
}