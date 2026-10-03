using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Создаёт подключения к СУБД. Единственное место, где выбирается конкретный ADO.NET-провайдер.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Активный диалект.</summary>
    IDbDialect Dialect { get; }

    /// <summary>Создаёт закрытое подключение. Вызывающий код обязан его закрыть.</summary>
    DbConnection CreateConnection();

    /// <summary>Создаёт и открывает подключение.</summary>
    /// <param name="ct">Токен отмены.</param>
    Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// Реализация фабрики подключений поверх Npgsql и Microsoft.Data.Sqlite.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>Создаёт фабрику подключений.</summary>
    /// <param name="dialect">Диалект СУБД.</param>
    /// <param name="connectionString">Строка подключения.</param>
    public DbConnectionFactory(IDbDialect dialect, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        Dialect = dialect;
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public IDbDialect Dialect { get; }

    /// <inheritdoc />
    public DbConnection CreateConnection() => Dialect.Provider switch
    {
        DbProvider.PostgreSql => new NpgsqlConnection(_connectionString),
        DbProvider.Sqlite => new SqliteConnection(_connectionString),
        _ => throw new InvalidOperationException($"Провайдер {Dialect.Provider} не поддерживается"),
    };

    /// <inheritdoc />
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}