using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Application.Lifecycle;

/// <summary>
/// Управляет жизненным циклом версий объекта.
/// </summary>
public interface IObjectLifecycleService
{
    /// <summary>
    /// Меняет состояние текущей версии объекта.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">Переход запрещён.</exception>
    /// <exception cref="InvalidOperationException">У объекта нет версий.</exception>
    Task<ObjectVersionRow> ChangeStateAsync(
        long objectId,
        VersionState newState,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает все версии объекта от новых к старым.</summary>
    Task<IReadOnlyList<ObjectVersionRow>> GetVersionsAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает <c>true</c>, если переход возможен.</summary>
    bool CanTransition(VersionState from, VersionState to);
}

/// <summary>
/// Служба жизненного цикла версий.
/// </summary>
public sealed class ObjectLifecycleService : IObjectLifecycleService
{
    private readonly IPdmUnitOfWorkFactory _unitOfWorkFactory;

    public ObjectLifecycleService(IPdmUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    /// <inheritdoc />
    public async Task<ObjectVersionRow> ChangeStateAsync(
        long objectId,
        VersionState newState,
        CancellationToken cancellationToken = default)
    {
        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var repository = unitOfWork.Repository;

        var versions = await repository.GetVersionsAsync(objectId, cancellationToken).ConfigureAwait(false);
        if (versions.Count == 0)
        {
            throw new InvalidOperationException($"У объекта {objectId} нет ни одной версии.");
        }

        // Меняется состояние текущей версии, а не последней по номеру: после
        // аннулирования самой новой версии рабочей остаётся предыдущая, и именно
        // её должен утвердить конструктор.
        var currentVersionId = await repository
            .GetCurrentVersionIdAsync(objectId, cancellationToken)
            .ConfigureAwait(false);

        if (currentVersionId is null)
        {
            throw new InvalidOperationException(
                $"У объекта {objectId} нет текущей версии: все версии аннулированы.");
        }

        var current = versions.Single(version => version.Id == currentVersionId.Value);
        VersionStateRules.EnsureCanTransition(current.State, newState);

        await repository.SetVersionStateAsync(current.Id, newState, cancellationToken).ConfigureAwait(false);

        // Аннулирование меняет состав изделия: текущей должна стать последняя
        // неаннулированная версия, иначе расчёты продолжат использовать
        // отменённые данные.
        //
        // Если текущая версия не аннулирована, назначение остаётся прежним:
        // «последняя по номеру» может быть уже аннулированной, и подменять ею
        // рабочую версию нельзя.
        if (newState != VersionState.Annulled)
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return await ReadAsync(current.Id, cancellationToken).ConfigureAwait(false);
        }

        var newCurrentId = versions
            .Select(version => new
            {
                version.Id,
                version.VersionNo,
                EffectiveState = version.Id == current.Id ? newState : version.State,
            })
            .Where(entry => entry.EffectiveState != VersionState.Annulled)
            .OrderByDescending(entry => entry.VersionNo)
            .Select(entry => entry.Id)
            .FirstOrDefault();

        if (newCurrentId != 0)
        {
            await repository.SetCurrentVersionAsync(objectId, newCurrentId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Аннулированы все версии: объект остаётся в базе как история,
            // но выпадает из состава изделий и расчётов.
            await repository.ClearCurrentVersionAsync(objectId, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(current.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает версию уже после фиксации транзакции: репозиторий привязан к
    /// завершённой транзакции и не может открыть вторую команду.
    /// </summary>
    private async Task<ObjectVersionRow> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await _unitOfWorkFactory.CreateReadRepository()
            .GetVersionAsync(versionId, cancellationToken)
            .ConfigureAwait(false);

        return version ?? throw new InvalidOperationException($"Версия {versionId} не найдена после изменения состояния.");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ObjectVersionRow>> GetVersionsAsync(long objectId, CancellationToken cancellationToken = default) =>
        _unitOfWorkFactory.CreateReadRepository().GetVersionsAsync(objectId, cancellationToken);

    /// <inheritdoc />
    public bool CanTransition(VersionState from, VersionState to) => VersionStateRules.CanTransition(from, to);
}