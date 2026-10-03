using Microsoft.Data.Sqlite;
using MiniPdm.Infrastructure.Persistence;

namespace MiniPdm.Tests.Infrastructure;

/// <summary>
/// Автономная база SQLite в памяти со схемой, готовая к работе.
/// </summary>
/// <remarks>
/// Используется общая кэшированная база в памяти (<c>Mode=Memory;Cache=Shared</c>):
/// подключение с <c>Data Source=:memory:</c> каждый раз создаёт новую пустую базу,
/// а удержание одного открытого подключения не даёт общей базе исчезнуть.
/// Благодаря этому <c>dotnet test</c> не требует запущенного сервера БД.
/// </remarks>
public sealed class SqliteTestDb : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;

    private SqliteTestDb(string connectionString, DbConnectionFactory factory)
    {
        ConnectionString = connectionString;
        ConnectionFactory = factory;
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
    }

    /// <summary>Строка подключения к тестовой базе.</summary>
    public string ConnectionString { get; }

    /// <summary>Фабрика подключений для репозиториев.</summary>
    public DbConnectionFactory ConnectionFactory { get; }

    /// <summary>Создаёт пустую базу со схемой.</summary>
    /// <param name="name">Уникальное имя базы в памяти.</param>
    public static async Task<SqliteTestDb> CreateAsync(string? name = null)
    {
        var dbName = name ?? "minipdm_" + Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";

        var db = new SqliteTestDb(
            connectionString,
            new DbConnectionFactory(new SqliteDialect(), connectionString));

        await new SchemaInitializer(db.ConnectionFactory).InitializeAsync();
        return db;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _keepAlive.DisposeAsync();
        SqliteConnection.ClearAllPools();
    }
}

/// <summary>
/// Базовый класс для тестов, которым нужна база. xUnit создаёт новый экземпляр класса
/// на каждый тест, поэтому каждая проверка получает собственную изолированную базу.
/// </summary>
public abstract class SqliteTestBase : IAsyncLifetime
{
    private SqliteTestDb? _db;

    /// <summary>База со схемой, готовая к работе.</summary>
    protected SqliteTestDb Db => _db ?? throw new InvalidOperationException("Тестовая база не инициализирована");

    /// <summary>Фабрика подключений для репозиториев.</summary>
    protected DbConnectionFactory ConnectionFactory => Db.ConnectionFactory;

    /// <inheritdoc />
    public async Task InitializeAsync() => _db = await SqliteTestDb.CreateAsync();

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}