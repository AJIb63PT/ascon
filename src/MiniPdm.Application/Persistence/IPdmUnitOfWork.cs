namespace MiniPdm.Application.Persistence;

/// <summary>
/// Транзакционная единица работы. Повторный импорт выполняется целиком в одной
/// транзакции: при ошибке не остаётся половины выгруженных объектов.
/// </summary>
public interface IPdmUnitOfWork : IAsyncDisposable
{
    /// <summary>Репозиторий, привязанный к транзакции.</summary>
    IPdmRepository Repository { get; }

    /// <summary>Фиксирует изменения.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Создаёт единицы работы.
/// </summary>
public interface IPdmUnitOfWorkFactory
{
    /// <summary>Открывает транзакцию.</summary>
    Task<IPdmUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает репозиторий для чтения вне транзакции.</summary>
    IPdmRepository CreateReadRepository();
}