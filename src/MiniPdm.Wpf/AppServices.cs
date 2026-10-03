using Microsoft.Extensions.Configuration;
using MiniPdm.Application.Calculations;
using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Application.Lifecycle;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Queries;
using MiniPdm.Infrastructure.Cad;
using MiniPdm.Infrastructure.Persistence;
using MiniPdm.Wpf.ViewModels;

namespace MiniPdm.Wpf;

/// <summary>
/// Composition root приложения: собирает зависимости и создаёт окно.
/// </summary>
/// <remarks>
/// Контейнер внедрения не используется намеренно: набор зависимостей невелик,
/// а требование «состав сборки виден в одном месте» выполняется точнее, когда
/// граф зависимостей записан явно.
/// </remarks>
public sealed class AppServices
{
    private readonly AppSettings _settings;

    private AppServices(AppSettings settings)
    {
        _settings = settings;

        var dialect = DbDialects.Create(settings.Database.Provider);
        _connectionFactory = new DbConnectionFactory(dialect, settings.Database.ConnectionString);
        ConnectionFactory = _connectionFactory;
        UnitOfWorkFactory = new PdmUnitOfWorkFactory(_connectionFactory);

        ImportService = new CadImportService(
            new FileSystemCadDocumentCatalog(),
            new JsonCadDocumentReader(),
            UnitOfWorkFactory);

        LifecycleService = new ObjectLifecycleService(UnitOfWorkFactory);
        Calculator = new BomCalculator();
    }

    private readonly DbConnectionFactory _connectionFactory;

    /// <summary>Параметры подключения к базе.</summary>
    public DatabaseSettings Database => _settings.Database;

    /// <summary>Фабрика подключений.</summary>
    public IDbConnectionFactory ConnectionFactory { get; }

    /// <summary>Фабрика единиц работы.</summary>
    public IPdmUnitOfWorkFactory UnitOfWorkFactory { get; }

    /// <summary>Служба импорта выгрузки CAD-системы.</summary>
    public ICadImportService ImportService { get; }

    /// <summary>Служба жизненного цикла версий.</summary>
    public IObjectLifecycleService LifecycleService { get; }

    /// <summary>Расчёты по составу изделия.</summary>
    public IBomCalculator Calculator { get; }

    /// <summary>
    /// Читает <c>appsettings.json</c> и собирает зависимости.
    /// </summary>
    /// <param name="basePath">
    /// Папка с конфигурацией. По умолчанию — папка исполняемого файла.
    /// </param>
    public static AppServices Create(string? basePath = null)
    {
        basePath ??= AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = configuration.Get<AppSettings>()
            ?? throw new InvalidOperationException("Раздел настроек не найден в appsettings.json.");

        return new AppServices(settings);
    }

    /// <summary>Создаёт таблицы и индексы, если их ещё нет.</summary>
    public Task InitializeDatabaseAsync() =>
        new SchemaInitializer(_connectionFactory).InitializeAsync();

    /// <summary>
    /// Создаёт главное окно вместе с моделью представления.
    /// </summary>
    public MainWindow CreateMainWindow()
    {
        var viewModel = new MainWindowViewModel(
            new PdmQueries(UnitOfWorkFactory, Calculator),
            ImportService,
            LifecycleService);

        return new MainWindow { DataContext = viewModel };
    }
}
