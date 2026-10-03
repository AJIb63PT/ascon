namespace MiniPdm.Application.Persistence;

/// <summary>
/// Уровень записи в журнал импорта.
/// </summary>
public enum ImportLogSeverity
{
    /// <summary>Документ принят без замечаний.</summary>
    Info = 0,

    /// <summary>Документ принят, но содержит неполные данные.</summary>
    Warning = 1,

    /// <summary>Документ отклонён.</summary>
    Error = 2,
}