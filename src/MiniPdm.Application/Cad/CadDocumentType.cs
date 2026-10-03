namespace MiniPdm.Application.Cad;

/// <summary>
/// Тип документа CAD-системы. Соответствует значению <c>type</c> в исходных данных.
/// </summary>
/// <remarks>
/// Тип намеренно отличается от <see cref="MiniPdm.Domain.PdmObjectType"/>: контракт
/// с внешней системой не должен зависеть от внутренней модели предметной области.
/// </remarks>
public enum CadDocumentType
{
    /// <summary>Сборка.</summary>
    Assembly = 0,

    /// <summary>Деталь.</summary>
    Part = 1,

    /// <summary>Стандартное изделие.</summary>
    StandardPart = 2,
}