namespace MiniPdm.Domain;

/// <summary>
/// Тип объекта системы. Соответствует значению <c>type</c> в документах CAD-системы.
/// </summary>
public enum PdmObjectType
{
    /// <summary>Сборка: имеет состав, масса вычисляется.</summary>
    Assembly = 0,

    /// <summary>Деталь: имеет собственную массу и материал.</summary>
    Part = 1,

    /// <summary>Стандартное изделие: идентифицируется наименованием, обозначение отсутствует.</summary>
    StandardPart = 2,
}