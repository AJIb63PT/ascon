using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>
/// Узел развёрнутого состава изделия.
/// </summary>
/// <param name="ObjectId">Объект узла.</param>
/// <param name="VersionId">Текущая версия объекта, использованная при раскрытии.</param>
/// <param name="ParentObjectId">Объект родительского узла; <c>null</c> у корня.</param>
/// <param name="Type">Тип объекта.</param>
/// <param name="Designation">Обозначение.</param>
/// <param name="Name">Наименование.</param>
/// <param name="State">Состояние текущей версии.</param>
/// <param name="VersionNo">Номер текущей версии.</param>
/// <param name="Material">Материал.</param>
/// <param name="MassKg">Масса одного экземпляра.</param>
/// <param name="Quantity">Количество с учётом всех уровней вложенности.</param>
/// <param name="Depth">Глубина в дереве; корень — 0.</param>
public sealed record BomTreeNode(
    long ObjectId,
    long VersionId,
    long? ParentObjectId,
    PdmObjectType Type,
    string? Designation,
    string Name,
    VersionState State,
    int VersionNo,
    string? Material,
    decimal? MassKg,
    int Quantity,
    int Depth)
{
    /// <summary>Ключ для отображения.</summary>
    public string DisplayKey => string.IsNullOrWhiteSpace(Designation) ? Name : Designation;

    /// <summary>Возвращает <c>true</c>, если масса известна.</summary>
    public bool HasMass => MassKg.HasValue;

    /// <summary>Масса с учётом количества по всем уровням; <c>null</c>, если масса неизвестна.</summary>
    public decimal? TotalMassKg => MassKg.HasValue ? MassKg.Value * Quantity : null;
}