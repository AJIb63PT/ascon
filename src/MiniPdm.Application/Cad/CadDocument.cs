namespace MiniPdm.Application.Cad;

/// <summary>
/// Документ CAD-системы в виде, пригодном для импорта.
/// </summary>
/// <remarks>
/// Модель не зависит от формата источника: это позволяет подменить JSON-читатель
/// на адаптер настоящего API САПР, не меняя логику импорта.
/// </remarks>
public sealed record CadDocument
{
    /// <summary>Версия формата исходных данных.</summary>
    public required int FormatVersion { get; init; }

    /// <summary>
    /// Идентификатор документа — имя файла на диске. Именно на него ссылаются
    /// <see cref="Components"/> и по нему выполняется повторный импорт.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>Тип документа.</summary>
    public required CadDocumentType Type { get; init; }

    /// <summary>Обозначение. У стандартных изделий — <c>null</c>.</summary>
    public string? Designation { get; init; }

    /// <summary>Наименование.</summary>
    public required string Name { get; init; }

    /// <summary>Материал. У сборок, как правило, не заполняется.</summary>
    public string? Material { get; init; }

    /// <summary>Масса в килограммах за один экземпляр. Может отсутствовать.</summary>
    public decimal? MassKg { get; init; }

    /// <summary>Компоненты состава. Непустой список только у сборок.</summary>
    public IReadOnlyList<CadComponent> Components { get; init; } = Array.Empty<CadComponent>();

    /// <summary>Возвращает <c>true</c>, если документ является сборкой.</summary>
    public bool IsAssembly => Type == CadDocumentType.Assembly;
}