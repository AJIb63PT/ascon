namespace MiniPdm.Application.Import;

/// <summary>
/// Отчёт об импорте папки выгрузки.
/// </summary>
public sealed class ImportReport
{
    /// <summary>Создаёт отчёт.</summary>
    public ImportReport(
        string folderPath,
        DateTimeOffset startedAt,
        TimeSpan duration,
        IReadOnlyList<ImportOutcome> outcomes)
    {
        FolderPath = folderPath;
        StartedAt = startedAt;
        Duration = duration;
        Outcomes = outcomes;
    }

    /// <summary>Папка, из которой выполнялся импорт.</summary>
    public string FolderPath { get; }

    /// <summary>Время начала импорта.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Длительность импорта.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Результат по каждому файлу.</summary>
    public IReadOnlyList<ImportOutcome> Outcomes { get; }

    /// <summary>Количество документов, попавших в базу, включая принятые с предупреждениями.</summary>
    public int AcceptedCount => Outcomes.Count(outcome => outcome.IsImported);

    /// <summary>Количество отклонённых документов.</summary>
    public int RejectedCount => Outcomes.Count(outcome => outcome.Severity == ImportSeverity.Error);

    /// <summary>Количество принятых документов с замечаниями.</summary>
    public int WarningCount => Outcomes.Count(outcome => outcome.Severity == ImportSeverity.Warning);

    /// <summary>Всего обработано файлов.</summary>
    public int TotalCount => Outcomes.Count;

    /// <summary>Возвращает <c>true</c>, если хотя бы один документ отклонён.</summary>
    public bool HasErrors => RejectedCount > 0;

    /// <summary>Текстовое представление отчёта для журнала и окна приложения.</summary>
    public string ToSummary()
    {
        return $"Принято: {AcceptedCount}, отклонено: {RejectedCount}, предупреждений: {WarningCount} "
            + $"из {TotalCount} файлов за {Duration.TotalSeconds:F1} с";
    }
}