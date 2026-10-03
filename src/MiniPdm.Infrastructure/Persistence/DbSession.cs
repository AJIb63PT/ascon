using System.Data;
using System.Data.Common;
using Dapper;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Обёртка над подключением и, при наличии, транзакцией. Скрывает от репозитория
/// разницу между автономным подключением и подключением в рамках единицы работы.
/// </summary>
internal sealed class DbSession : IAsyncDisposable
{
    private readonly bool _ownsConnection;

    public DbSession(DbConnection connection, DbTransaction? transaction, bool ownsConnection)
    {
        Connection = connection;
        Transaction = transaction;
        _ownsConnection = ownsConnection;
    }

    public DbConnection Connection { get; }

    public DbTransaction? Transaction { get; }

    /// <summary>Открывает новое подключение, которым владеет сессия.</summary>
    public static async Task<DbSession> OpenAsync(
        Func<DbConnection> connectionFactory,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // SQLite по умолчанию не проверяет внешние ключи.
        if (transaction is null && connection is Microsoft.Data.Sqlite.SqliteConnection)
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON").ConfigureAwait(false);
        }

        return new DbSession(connection, transaction, ownsConnection: true);
    }

    /// <summary>Создаёт сессию поверх уже открытого подключения, не замыкая его.</summary>
    public static DbSession Borrow(DbConnection connection, DbTransaction? transaction) =>
        new(connection, transaction, ownsConnection: false);

    public Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default) =>
        Connection.ExecuteAsync(new CommandDefinition(sql, parameters, Transaction, cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default) =>
        (await Connection.QueryAsync<T>(new CommandDefinition(sql, parameters, Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToArray();

    /// <summary>
    /// Возвращает единственную строку или <c>null</c>, если строк нет.
    /// </summary>
    /// <remarks>
    /// Все обращения идут по первичному ключу, поэтому строка может быть только одна.
    /// </remarks>
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var rows = await QueryAsync<T>(sql, parameters, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>
    /// Выполняет запрос, возвращающий одно скалярное значение.
    /// </summary>
    /// <exception cref="InvalidOperationException">Запрос не вернул значение.</exception>
    public async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var result = await Connection
            .ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Запрос не вернул значение.");
    }

    /// <summary>
    /// Выполняет запрос, возвращающий одно скалярное значение или <c>null</c>,
    /// если значение равно <c>NULL</c>.
    /// </summary>
    public Task<T?> ExecuteNullableScalarAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) =>
        Connection.ExecuteScalarAsync<T?>(
            new CommandDefinition(sql, parameters, Transaction, cancellationToken: cancellationToken));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}