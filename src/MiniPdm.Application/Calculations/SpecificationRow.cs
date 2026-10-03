using MiniPdm.Domain;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Позиция сводной спецификации: все вхождения одного объекта сведены в одну строку.
/// </summary>
/// <param name="ObjectId">Объект.</param>
/// <param name="Type">Тип объекта.</param>
/// <param name="Designation">Обозначение.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Material">Материал.</param>
/// <param name="Quantity">Суммарное количество с учётом вложенности.</param>
/// <param name="UnitMassKg">Масса одного экземпляра.</param>
/// <param name="Occurrences">В скольких местах состава встречается позиция.</param>
public sealed record SpecificationRow(
    long ObjectId,
    PdmObjectType Type,
    string? Designation,
    string Name,
    string? Material,
    int Quantity,
    decimal? UnitMassKg,
    int Occurrences)
{
    /// <summary>Ключ для отображения.</summary>
    public string DisplayKey => string.IsNullOrWhiteSpace(Designation) ? Name : Designation;

    /// <summary>Суммарная масса позиции; <c>null</c>, если масса неизвестна.</summary>
    public decimal? TotalMassKg => UnitMassKg.HasValue ? UnitMassKg.Value * Quantity : null;
}