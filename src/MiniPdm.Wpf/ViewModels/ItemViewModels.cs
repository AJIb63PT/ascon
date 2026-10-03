using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MiniPdm.Application.Import;
using MiniPdm.Domain;

namespace MiniPdm.Wpf.ViewModels;

/// <summary>
/// Базовая модель представления с уведомлением об изменениях.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Присваивает значение и уведомляет, если оно изменилось.</summary>
    /// <returns><c>true</c>, если значение изменилось.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Уведомляет об изменении свойства.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Строка дерева состава.
/// </summary>
/// <remarks>
/// Модель представления не зависит от типов слоя хранения: дерево показывается
/// как иерархия, а поля вычисляются один раз при построении строки.
/// </remarks>
public sealed class BomNodeViewModel : ObservableObject
{
    private bool _isExpanded = true;

    /// <summary>Создаёт строку дерева.</summary>
    public BomNodeViewModel(
        string displayKey,
        string name,
        string typeName,
        int depth,
        int quantity,
        string? material,
        decimal? massKg,
        string stateName)
    {
        DisplayKey = displayKey;
        Name = name;
        TypeName = typeName;
        Depth = depth;
        Quantity = quantity;
        Material = material;
        MassKg = massKg;
        StateName = stateName;
        Children = new ObservableCollection<BomNodeViewModel>();
    }

    /// <summary>Обозначение или наименование для стандартного изделия.</summary>
    public string DisplayKey { get; }

    /// <summary>Наименование.</summary>
    public string Name { get; }

    /// <summary>Тип объекта словами: сборка, деталь или стандартное изделие.</summary>
    public string TypeName { get; }

    /// <summary>Уровень вложенности.</summary>
    public int Depth { get; }

    /// <summary>Количество с учётом вложенности.</summary>
    public int Quantity { get; }

    /// <summary>Материал.</summary>
    public string? Material { get; }

    /// <summary>Масса одного экземпляра.</summary>
    public decimal? MassKg { get; }

    /// <summary>Состояние текущей версии словами.</summary>
    public string StateName { get; }

    /// <summary>Дочерние узлы.</summary>
    public ObservableCollection<BomNodeViewModel> Children { get; }

    /// <summary>Развёрнут ли узел.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Отступ строки в дереве.</summary>
    public int Indent => Depth * 16;
}

/// <summary>
/// Строка позиции спецификации.
/// </summary>
public sealed class SpecificationRowViewModel
{
    /// <summary>Создаёт строку спецификации.</summary>
    public SpecificationRowViewModel(string displayKey, string name, string typeName, string? material, int quantity, decimal? unitMassKg)
    {
        DisplayKey = displayKey;
        Name = name;
        TypeName = typeName;
        Material = material;
        Quantity = quantity;
        UnitMassKg = unitMassKg;
    }

    /// <summary>Обозначение или наименование.</summary>
    public string DisplayKey { get; }

    /// <summary>Наименование.</summary>
    public string Name { get; }

    /// <summary>Тип объекта словами.</summary>
    public string TypeName { get; }

    /// <summary>Материал.</summary>
    public string? Material { get; }

    /// <summary>Суммарное количество.</summary>
    public int Quantity { get; }

    /// <summary>Масса одного экземпляра.</summary>
    public decimal? UnitMassKg { get; }

    /// <summary>Масса позиции или прочерк, если масса неизвестна.</summary>
    public string TotalMassText =>
        UnitMassKg.HasValue ? $"{UnitMassKg.Value * Quantity:F4} кг" : "—";
}

/// <summary>
/// Строка версии в карточке объекта.
/// </summary>
public sealed class VersionRowViewModel
{
    /// <summary>Создаёт строку версии.</summary>
    public VersionRowViewModel(int versionNo, string stateName, string? material, decimal? massKg, DateTimeOffset createdAt, bool isCurrent)
    {
        VersionNo = versionNo;
        StateName = stateName;
        Material = material;
        MassKg = massKg;
        CreatedAt = createdAt;
        IsCurrent = isCurrent;
    }

    /// <summary>Номер версии.</summary>
    public int VersionNo { get; }

    /// <summary>Состояние словами.</summary>
    public string StateName { get; }

    /// <summary>Материал.</summary>
    public string? Material { get; }

    /// <summary>Масса.</summary>
    public decimal? MassKg { get; }

    /// <summary>Время создания.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Является ли версия текущей.</summary>
    public bool IsCurrent { get; }

    /// <summary>Пометка текущей версии для интерфейса.</summary>
    public string StateWithMark => IsCurrent ? $"{StateName} (текущая)" : StateName;
}

/// <summary>
/// Строка отчёта импорта.
/// </summary>
public sealed class ImportOutcomeViewModel
{
    /// <summary>Создаёт строку отчёта.</summary>
    public ImportOutcomeViewModel(
        string fileName,
        ImportSeverity severity,
        string severityName,
        string reason,
        string? designation,
        string? name)
    {
        FileName = fileName;
        Severity = severity;
        SeverityName = severityName;
        Reason = reason;
        Designation = designation;
        Name = name;
    }

    /// <summary>Имя файла.</summary>
    public string FileName { get; }

    /// <summary>
    /// Итог обработки документа: нужен для фильтра, название результата для
    /// показа не годится — по нему строки не сравнить.
    /// </summary>
    public ImportSeverity Severity { get; }

    /// <summary>Результат словами: принято, с замечанием или отклонено.</summary>
    public string SeverityName { get; }

    /// <summary>Причина или пояснение.</summary>
    public string Reason { get; }

    /// <summary>Обозначение из документа.</summary>
    public string? Designation { get; }

    /// <summary>Наименование из документа.</summary>
    public string? Name { get; }
}

/// <summary>
/// Вариант фильтра отчёта импорта.
/// </summary>
/// <remarks>
/// Вариант «все» не ограничивает результат, поэтому его итог — <c>null</c>.
/// </remarks>
public sealed class ImportFilterOption
{
    /// <summary>Создаёт вариант фильтра.</summary>
    public ImportFilterOption(ImportSeverity? severity, string title)
    {
        Severity = severity;
        Title = title;
    }

    /// <summary>Отбираемый итог; <c>null</c> — без ограничения.</summary>
    public ImportSeverity? Severity { get; }

    /// <summary>Название варианта вместе с количеством строк.</summary>
    public string Title { get; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Доступный переход состояния в виде, пригодном для выбора в списке.
/// </summary>
/// <remarks>
/// Список показывает название словами, а команде передаётся само состояние.
/// Список из строк не годился бы: выбранный элемент тогда не совпал бы с
/// состоянием, и переход не выполнялся.
/// </remarks>
public sealed class TransitionOption
{
    /// <summary>Создаёт вариант перехода.</summary>
    public TransitionOption(VersionState state) =>
        Title = VersionStateNames.Describe(state);

    /// <summary>Состояние, в которое переводится версия.</summary>
    public VersionState State { get; }

    /// <summary>Название состояния словами.</summary>
    public string Title { get; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Тип объекта словами.
/// </summary>
public static class PdmTypeNames
{
    /// <summary>Возвращает русское название типа.</summary>
    public static string Describe(PdmObjectType type) => type switch
    {
        PdmObjectType.Assembly => "Сборка",
        PdmObjectType.Part => "Деталь",
        PdmObjectType.StandardPart => "Стандартное изделие",
        _ => "Неизвестный тип",
    };
}

/// <summary>
/// Состояние версии словами.
/// </summary>
public static class VersionStateNames
{
    /// <summary>Возвращает русское название состояния.</summary>
    public static string Describe(VersionState state) => state switch
    {
        VersionState.InWork => "В работе",
        VersionState.Approved => "Утверждена",
        VersionState.Annulled => "Аннулирована",
        _ => "Неизвестно",
    };
}
