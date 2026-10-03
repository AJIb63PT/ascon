using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using MiniPdm.Application.Calculations;
using MiniPdm.Application.Import;
using MiniPdm.Application.Lifecycle;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Queries;
using MiniPdm.Domain;

namespace MiniPdm.Wpf.ViewModels;

/// <summary>
/// Модель представления главного окна.
/// </summary>
/// <remarks>
/// Окно собрано на CommunityToolkit.Mvvm. Длительные операции выполняются через
/// <see cref="RunAsync"/>, который отменяет предыдущее задание: иначе два
/// импорта писали бы в базу одновременно.
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly PdmQueries _queries;
    private readonly ICadImportService _importService;
    private readonly IObjectLifecycleService _lifecycleService;
    private CancellationTokenSource? _operationCts;

    private string _searchText = string.Empty;
    private PdmObjectRow? _selectedObject;
    private TransitionOption? _targetTransition;
    private string _statusText = "Готово";
    private bool _isBusy;
    private double _importProgress;
    private string? _massText;
    private string? _compositionWarning;
    private ObjectCard? _card;
    private ImportFilterOption _selectedImportFilter = ImportFilterOptionsDefault[0];
    private string _importReportCountText = "Отчёт пуст";
    private bool _importTabSelected;

    /// <summary>
    /// Варианты фильтра по умолчанию. Объявлены полем, чтобы и начальное
    /// значение выбранного фильтра, и список в интерфейсе были одним и тем же
    /// перечислением.
    /// </summary>
    private static readonly ImportFilterOption[] ImportFilterOptionsDefault =
    {
        new(null, "Все результаты"),
        new(ImportSeverity.Error, "Только отклонённые"),
        new(ImportSeverity.Warning, "Только с замечанием"),
        new(ImportSeverity.Ok, "Только принятые"),
    };

    /// <summary>Создаёт модель представления.</summary>
    public MainWindowViewModel(
        PdmQueries queries,
        ICadImportService importService,
        IObjectLifecycleService lifecycleService)
    {
        _queries = queries;
        _importService = importService;
        _lifecycleService = lifecycleService;

        ImportCommand = new AsyncRelayCommand<string?>(ImportAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshSelectionAsync);
        ChangeStateCommand = new AsyncRelayCommand(ChangeStateAsync);
        ExportSpecificationCommand = new RelayCommand<string?>(ExportSpecification);

        // Отчёт показывается через представление коллекции: смена фильтра лишь
        // обновляет условие отбора, сами строки при этом не копируются.
        FilteredImportOutcomes = CollectionViewSource.GetDefaultView(ImportOutcomes);
        SelectedImportFilter = ImportFilterOptions[0];
    }

    /// <summary>Корневые узлы дерева состава.</summary>
    public ObservableCollection<BomNodeViewModel> TreeRoots { get; } = new();

    /// <summary>Позиции спецификации.</summary>
    public ObservableCollection<SpecificationRowViewModel> Specification { get; } = new();

    /// <summary>Версии выбранного объекта.</summary>
    public ObservableCollection<VersionRowViewModel> Versions { get; } = new();

    /// <summary>Результаты последнего импорта.</summary>
    public ObservableCollection<ImportOutcomeViewModel> ImportOutcomes { get; } = new();

    /// <summary>
    /// Отфильтрованная часть отчёта импорта.
    /// </summary>
    /// <remarks>
    /// Фильтрация выполняется представлением коллекции, а не отдельным списком:
    /// строки отчёта остаются в <see cref="ImportOutcomes"/>, а счётчики в шапке
    /// показывают результат по всей выгрузке, а не по тому, что сейчас видно.
    /// </remarks>
    public ICollectionView FilteredImportOutcomes { get; }

    /// <summary>Варианты фильтра отчёта импорта.</summary>
    public IReadOnlyList<ImportFilterOption> ImportFilterOptions => ImportFilterOptionsDefault;

    /// <summary>Флаг выбора вкладки «Отчёт импорта».</summary>
    /// <remarks>
    /// Позволяет автоматически переключить вкладку при наличии отклонённых
    /// документов после импорта, чтобы конструктор сразу увидел причины отказа.
    /// </remarks>
    public bool ImportTabSelected
    {
        get => _importTabSelected;
        set => SetProperty(ref _importTabSelected, value);
    }

    /// <summary>Результаты поиска.</summary>
    public ObservableCollection<PdmObjectRow> SearchResults { get; } = new();

    /// <summary>Выбранный вариант фильтра отчёта импорта.</summary>
    public ImportFilterOption SelectedImportFilter
    {
        get => _selectedImportFilter;
        set
        {
            if (SetProperty(ref _selectedImportFilter, value))
            {
                ApplyImportFilter();
            }
        }
    }

    /// <summary>Счётчик строк отчёта с учётом фильтра.</summary>
    public string ImportReportCountText
    {
        get => _importReportCountText;
        private set => SetProperty(ref _importReportCountText, value);
    }

    /// <summary>Команда импорта папки; параметр — путь к папке выгрузки.</summary>
    public IAsyncRelayCommand<string?> ImportCommand { get; }

    /// <summary>Команда перезагрузки карточки и состава.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Команда смены состояния версии.</summary>
    public IAsyncRelayCommand ChangeStateCommand { get; }

    /// <summary>Команда выгрузки спецификации в CSV; параметр — путь к файлу.</summary>
    public IRelayCommand<string?> ExportSpecificationCommand { get; }

    /// <summary>Текст для строки поиска.</summary>
    /// <remarks>
    /// Присваивание не запускает поиск: он выполняется по Enter или кнопкой,
    /// чтобы не обращаться к базе на каждый символ.
    /// </remarks>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

        /// <summary>Выбранный объект.</summary>
    public PdmObjectRow? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value) && value is not null)
            {
                SelectionRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Событие выбора объекта.</summary>
    public event EventHandler? SelectionRequested;

    /// <summary>Выбранный переход состояния.</summary>
    public TransitionOption? TargetTransition
    {
        get => _targetTransition;
        set => SetProperty(ref _targetTransition, value);
    }

    /// <summary>Доступные переходы для выбранного объекта.</summary>
    public IReadOnlyList<TransitionOption> TransitionTargets =>
        AllowedTransitions.Select(state => new TransitionOption(state)).ToArray();

    /// <summary>Разрешённые переходы для выбранного объекта.</summary>
    public IReadOnlyList<VersionState> AllowedTransitions =>
        Card?.AllowedTransitions ?? Array.Empty<VersionState>();

    /// <summary>Карточка выбранного объекта.</summary>
    public ObjectCard? Card
    {
        get => _card;
        private set
        {
            if (SetProperty(ref _card, value))
            {
                OnPropertyChanged(nameof(AllowedTransitions));
                OnPropertyChanged(nameof(TransitionTargets));
                OnPropertyChanged(nameof(CardTitle));
            }
        }
    }

    /// <summary>Заголовок карточки.</summary>
    public string CardTitle => Card is null
        ? "Объект не выбран"
        : $"{Card.Object.DisplayKey} — {Card.Object.Name}";

    /// <summary>Текст в строке состояния.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Выполняется ли длительная операция.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Прогресс импорта от 0 до 1.</summary>
    public double ImportProgress
    {
        get => _importProgress;
        private set => SetProperty(ref _importProgress, value);
    }

    /// <summary>Сводка по массе изделия.</summary>
    public string? MassText
    {
        get => _massText;
        private set => SetProperty(ref _massText, value);
    }

    /// <summary>Предупреждение о составе: циклы или неизвестные массы.</summary>
    public string? CompositionWarning
    {
        get => _compositionWarning;
        private set => SetProperty(ref _compositionWarning, value);
    }

    /// <summary>
    /// Первичная загрузка при открытии окна.
    /// </summary>
    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await LoadObjectsAsync(string.Empty).ConfigureAwait(true);

            if (SearchResults.Count > 0)
            {
                SelectedObject = SearchResults[0];
            }
        }).ConfigureAwait(true);
    }

    /// <summary>Выполняет поиск по строке <see cref="SearchText"/>.</summary>
    public async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            var count = await LoadObjectsAsync(SearchText).ConfigureAwait(true);
            StatusText = count == 0
                ? $"По запросу «{SearchText}» ничего не найдено"
                : $"Найдено объектов: {count}";
        }).ConfigureAwait(true);
    }

    /// <summary>Перезагружает карточку и состав выбранного объекта.</summary>
    public Task RefreshSelectionAsync() => RunAsync(LoadSelectionAsync);

    /// <summary>
    /// Наполняет карточку и состав данными.
    /// </summary>
    /// <remarks>
    /// Отдельный метод нужен потому, что импорт и смена состояния вызывают
    /// обновление изнутри уже запущенной операции: вложенный вызов <see cref="RunAsync"/>
    /// отменил бы сам себя.
    /// </remarks>
    private async Task LoadSelectionAsync()
    {
        {
            TreeRoots.Clear();
            Specification.Clear();
            Versions.Clear();

            if (SelectedObject is null)
            {
                Card = null;
                MassText = null;
                CompositionWarning = null;
                return;
            }

            var card = await _queries.GetCardAsync(SelectedObject.Id, CurrentToken).ConfigureAwait(true);
            Card = card;

            foreach (var version in card?.Versions ?? Array.Empty<ObjectVersionRow>())
            {
                Versions.Add(new VersionRowViewModel(
                    version.VersionNo,
                    VersionStateNames.Describe(version.State),
                    version.Material,
                    version.MassKg,
                    version.CreatedAt,
                    card?.CurrentVersion?.Id == version.Id));
            }

            // Значение по умолчанию — первый разрешённый переход.
            TargetTransition = TransitionTargets.FirstOrDefault();

            var composition = await _queries
                .GetCompositionAsync(SelectedObject.Id, CurrentToken)
                .ConfigureAwait(true);

            if (composition is null)
            {
                MassText = null;
                CompositionWarning = "Состав не раскрывается: у объекта нет текущей версии.";
                return;
            }

            foreach (var root in composition.Roots)
            {
                TreeRoots.Add(ToNode(root));
            }

            foreach (var row in composition.Specification)
            {
                Specification.Add(new SpecificationRowViewModel(
                    row.DisplayKey,
                    row.Name,
                    PdmTypeNames.Describe(row.Type),
                    row.Material,
                    row.Quantity,
                    row.UnitMassKg));
            }

            MassText = composition.Mass.TotalMassKg is { } total
                ? $"Масса изделия: {total.ToString("F4", CultureInfo.CurrentCulture)} кг"
                : "Масса изделия не рассчитана полностью";

            CompositionWarning = BuildWarning(composition);
        }
    }

    /// <summary>Импортирует папку выгрузки.</summary>
    private async Task ImportAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusText = "Папка выгрузки не выбрана";
            return;
        }

        ImportOutcomes.Clear();
        ImportProgress = 0;

        // Новый импорт начинается с обзорного отчёта: иначе пользователь увидел бы
        // пустую таблицу под фильтром предыдущей выгрузки.
        SelectedImportFilter = ImportFilterOptions[0];

        await RunAsync(async () =>
        {
            var progress = new Progress<ImportProgress>(update =>
            {
                ImportProgress = update.Ratio;
                StatusText = $"Импорт: {update.Processed} из {update.Total} — {update.CurrentFileName}";
            });

            var report = await _importService
                .ImportAsync(folderPath, progress, CurrentToken)
                .ConfigureAwait(true);

            foreach (var outcome in report.Outcomes)
            {
                ImportOutcomes.Add(new ImportOutcomeViewModel(
                    outcome.FileName,
                    outcome.Severity,
                    Describe(outcome.Severity),
                    outcome.Reason ?? string.Empty,
                    outcome.Designation,
                    outcome.Name));
            }

            // Приём отклонённых документов конструктору важнее всего: отчёт
            // открывается на них, а не на полном списке принятых.
            if (report.RejectedCount > 0)
            {
                SelectedImportFilter = ImportFilterOptions[1];
                ImportTabSelected = true;
            }

            // Счётчик обновляется явно: если фильтр не изменился, свойство не
            // выдаст уведомление, а строк в отчёте стало больше.
            UpdateImportReportCount();

            StatusText = report.ToSummary();
            await LoadObjectsAsync(SearchText).ConfigureAwait(true);
            await LoadSelectionAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    /// <summary>Применяет условие отбора к отчёту импорта.</summary>
    private void ApplyImportFilter()
    {
        // Условие читает текущее значение SelectedImportFilter при каждой
        // переоценке, поэтому замыкание на снимок варианта не требуется.
        var severity = SelectedImportFilter?.Severity;

        FilteredImportOutcomes.Filter = item =>
            severity is null || item is ImportOutcomeViewModel outcome && outcome.Severity == severity;

        FilteredImportOutcomes.Refresh();
        UpdateImportReportCount();
    }

    /// <summary>Обновляет счётчик строк отчёта с учётом фильтра.</summary>
    private void UpdateImportReportCount()
    {
        if (ImportOutcomes.Count == 0)
        {
            ImportReportCountText = "Отчёт пуст";
            return;
        }

        var shown = FilteredImportOutcomes.Cast<object>().Count();

        // Счётчики по всей выгрузке приведены рядом с показанными: иначе не
        // видно, что фильтр что-то скрыл.
        var rejected = ImportOutcomes.Count(o => o.Severity == ImportSeverity.Error);
        var warning = ImportOutcomes.Count(o => o.Severity == ImportSeverity.Warning);

        var summary = $"Показано {shown} из {ImportOutcomes.Count}";
        var counts = $"отклонено {rejected}, с замечанием {warning}";

        ImportReportCountText = shown == ImportOutcomes.Count
            ? $"{summary} ({counts})"
            : $"{summary}, фильтр «{SelectedImportFilter?.Title}» ({counts})";
    }

    /// <summary>Меняет состояние текущей версии выбранного объекта.</summary>
    private async Task ChangeStateAsync()
    {
        if (SelectedObject is null || TargetTransition is not { } transition)
        {
            StatusText = "Сначала выберите объект и состояние перехода.";
            return;
        }

        await RunAsync(async () =>
        {
            await _lifecycleService
                .ChangeStateAsync(SelectedObject.Id, transition.State, CurrentToken)
                .ConfigureAwait(true);

            StatusText = $"Состояние изменено на «{transition.Title}»";
            await LoadSelectionAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    /// <summary>Выгружает спецификацию в CSV.</summary>
    private void ExportSpecification(string? filePath)
    {
        if (Specification.Count == 0 || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        // Разделитель «;» и русская кодировка выбраны под Excel: с запятой
        // русский Excel открывает файл как несколько столбцов.
        var builder = new StringBuilder();
        builder.AppendLine("Обозначение;Наименование;Тип;Материал;Количество;Масса позиции, кг");

        foreach (var row in Specification)
        {
            var mass = row.UnitMassKg.HasValue
                ? (row.UnitMassKg.Value * row.Quantity).ToString("F4", CultureInfo.InvariantCulture)
                : string.Empty;

            builder.AppendLine(string.Join(';',
                row.DisplayKey,
                row.Name,
                row.TypeName,
                row.Material ?? string.Empty,
                row.Quantity.ToString(CultureInfo.InvariantCulture),
                mass));
        }

        File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        StatusText = $"Спецификация выгружена: {filePath}";
    }

    private async Task<int> LoadObjectsAsync(string? query)
    {
        var results = await _queries.SearchAsync(query, CurrentToken).ConfigureAwait(true);
        SearchResults.Clear();

        foreach (var result in results)
        {
            SearchResults.Add(result);
        }

        return results.Count;
    }

    private static string? BuildWarning(CompositionView composition)
    {
        if (composition.Cycles.Count > 0)
        {
            return $"Обнаружен цикл в составе: {composition.Cycles.Count}. Раскрытие может быть неверным.";
        }

        if (composition.Mass.NodesWithUnknownMass.Count > 0)
        {
            var names = string.Join(", ", composition.Mass.NodesWithUnknownMass.Select(node => node.Name));
            return $"Не заполнена масса: {names}";
        }

        return null;
    }

    private static BomNodeViewModel ToNode(BomNode node)
    {
        var viewModel = new BomNodeViewModel(
            node.DisplayKey,
            node.Name,
            PdmTypeNames.Describe(node.Type),
            node.Depth,
            node.Quantity,
            node.Material,
            node.MassKg,
            VersionStateNames.Describe(node.State));

        foreach (var child in node.Children)
        {
            viewModel.Children.Add(ToNode(child));
        }

        return viewModel;
    }

    private CancellationToken CurrentToken => _operationCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Выполняет операцию с отменой предыдущей и разблокировкой интерфейса.
    /// </summary>
    private async Task RunAsync(Func<Task> operation)
    {
        // Новый запуск отменяет предыдущий: два параллельных импорта в одну базу
        // привели бы к взаимоблокировке или задвоению объектов.
        _operationCts?.Cancel();
        _operationCts?.Dispose();

        var cts = new CancellationTokenSource();
        _operationCts = cts;

        IsBusy = true;

        try
        {
            // Продолжение возвращается на поток интерфейса: коллекции и свойства
            // модели представления обновляются из этого же потока.
            await operation().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Операция отменена";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            // Источник отмены освобождается только если его не подхватил более
            // новый запуск: иначе отменялся бы уже завершившийся.
            if (ReferenceEquals(_operationCts, cts))
            {
                _operationCts = null;
                cts.Dispose();
                IsBusy = false;
            }
        }
    }

    private static string Describe(ImportSeverity severity) => severity switch
    {
        ImportSeverity.Ok => "Принято",
        ImportSeverity.Warning => "С замечанием",
        _ => "Отклонено",
    };
}
