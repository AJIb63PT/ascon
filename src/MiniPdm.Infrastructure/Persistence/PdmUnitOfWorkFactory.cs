using System.Data.Common;
using Dapper;
using MiniPdm.Application.Persistence;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Единица работы поверх общей транзакции.
/// </summary>
internal sealed class PdmUnitOfWork : IPdmUnitOfWork
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _committed;

    public PdmUnitOfWork(DbConnection connection, DbTransaction transaction, IPdmRepository repository)
    {
        _connection = connection;
        _transaction = transaction;
        Repository = repository;
    }

    public IPdmRepository Repository { get; }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        // Незафиксированная транзакция откатывается: частично выгруженные
        // объекты не должны оставаться в базе.
        if (!_committed)
        {
            await _transaction.RollbackAsync().ConfigureAwait(false);
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Создаёт единицы работы и репозитории для чтения.
/// </summary>
public sealed class PdmUnitOfWorkFactory : IPdmUnitOfWorkFactory
{
    private readonly DbConnectionFactory _connectionFactory;

    public PdmUnitOfWorkFactory(DbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public IPdmRepository CreateReadRepository() =>
        new PdmRepository(DbSessionProvider.Standalone(_connectionFactory));

    public async Task<IPdmUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (connection is Microsoft.Data.Sqlite.SqliteConnection)
        {
            // SQLite не проверяет внешние ключи, пока это не включено явно.
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON").ConfigureAwait(false);
        }

        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var repository = new PdmRepository(DbSessionProvider.Scoped(connection, transaction));

        return new PdmUnitOfWork(connection, transaction, repository);
    }
}