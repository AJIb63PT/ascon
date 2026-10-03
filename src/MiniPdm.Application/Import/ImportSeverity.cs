namespace MiniPdm.Application.Import;

/// <summary>
/// Итог обработки одного документа при импорте.
/// </summary>
public enum ImportSeverity
{
    /// <summary>Документ принят без замечаний.</summary>
    Ok = 0,

    /// <summary>Документ принят, но содержит неполные данные.</summary>
    Warning = 1,

    /// <summary>Документ отклонён.</summary>
    Error = 2,
}