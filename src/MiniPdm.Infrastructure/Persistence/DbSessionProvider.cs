using System.Data;
using System.Data.Common;
using MiniPdm.Application.Persistence;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Поставщик сессий БД: либо новое подключение на каждую команду, либо сессия
/// общей транзакции.
/// </summary>
internal sealed class DbSessionProvider
{
    private readonly Func<CancellationToken, Task<DbSession>> _factory;

    private DbSessionProvider(Func<CancellationToken, Task<DbSession>> factory) => _factory = factory;

    /// <summary>Каждая команда открывает собственное подключение.</summary>
    public static DbSessionProvider Standalone(DbConnectionFactory connectionFactory) =>
        new(cancellationToken => DbSession.OpenAsync(
            () => connectionFactory.CreateConnection(),
            transaction: null,
            cancellationToken));

    /// <summary>Все команды используют общее подключение и общую транзакцию.</summary>
    public static DbSessionProvider Scoped(DbConnection connection, DbTransaction? transaction) =>
        new(_ => Task.FromResult(DbSession.Borrow(connection, transaction)));

    public Task<DbSession> OpenAsync(CancellationToken cancellationToken) => _factory(cancellationToken);
}