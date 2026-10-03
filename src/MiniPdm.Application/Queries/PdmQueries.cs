using MiniPdm.Application.Calculations;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Application.Queries;

/// <summary>
/// Карточка объекта: сам объект и его версии.
/// </summary>
/// <param name="Object">Объект.</param>
/// <param name="Versions">Версии от новых к старым.</param>
/// <param name="CurrentVersion">Текущая версия; <c>null</c>, если все версии аннулированы.</param>
public sealed record ObjectCard(
    PdmObjectRow Object,
    IReadOnlyList<ObjectVersionRow> Versions,
    ObjectVersionRow? CurrentVersion)
{
    /// <summary>
    /// Разрешённые следующие состояния текущей версии.
    /// </summary>
    /// <remarks>
    /// Пустой список означает, что переходов больше нет: например, версия
    /// аннулирована.
    /// </remarks>
    public IReadOnlyList<VersionState> AllowedTransitions =>
        CurrentVersion is null
            ? Array.Empty<VersionState>()
            : VersionStateRules.AllowedTargets(CurrentVersion.State);
}

/// <summary>
/// Результат раскрытия состава.
/// </summary>
/// <param name="Tree">Плоский список узлов в порядке обхода.</param>
/// <param name="Roots">Корневые узлы иерархии.</param>
/// <param name="Mass">Расчёт массы.</param>
/// <param name="Specification">Сводная спецификация.</param>
/// <param name="Cycles">Обнаруженные циклы; пустой список — циклов нет.</param>
public sealed record CompositionView(
    IReadOnlyList<BomTreeNode> Tree,
    IReadOnlyList<BomNode> Roots,
    MassCalculationResult Mass,
    IReadOnlyList<SpecificationRow> Specification,
    IReadOnlyList<BomCycle> Cycles);

/// <summary>
/// Запросы, необходимые интерфейсу.
/// </summary>
/// <remarks>
/// Служба скрывает создание репозитория и порядок вызовов: модель представления
/// работает с ней, а не с хранилищем напрямую.
/// </remarks>
public sealed class PdmQueries
{
    private readonly IPdmUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IBomCalculator _calculator;

    /// <summary>Создаёт службу запросов.</summary>
    public PdmQueries(IPdmUnitOfWorkFactory unitOfWorkFactory, IBomCalculator? calculator = null)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _calculator = calculator ?? new BomCalculator();
    }

    /// <summary>Ищет объекты по обозначению или наименованию.</summary>
    public Task<IReadOnlyList<PdmObjectRow>> SearchAsync(string? query, CancellationToken ct = default) =>
        _unitOfWorkFactory.CreateReadRepository().SearchAsync(query, ct);

    /// <summary>Возвращает объекты заданного типа.</summary>
    public Task<IReadOnlyList<PdmObjectRow>> GetByTypeAsync(PdmObjectType type, CancellationToken ct = default) =>
        _unitOfWorkFactory.CreateReadRepository().GetByTypeAsync(type, ct);

    /// <summary>Возвращает карточку объекта с версиями.</summary>
    public async Task<ObjectCard?> GetCardAsync(long objectId, CancellationToken ct = default)
    {
        var repository = _unitOfWorkFactory.CreateReadRepository();
        var pdmObject = await repository.GetObjectAsync(objectId, ct).ConfigureAwait(false);

        if (pdmObject is null)
        {
            return null;
        }

        var versions = await repository.GetVersionsAsync(objectId, ct).ConfigureAwait(false);

        // Источником истины о рабочей версии служит current_version_id:
        // «последняя по номеру» может оказаться аннулированной.
        var current = pdmObject.CurrentVersionId is { } currentId
            ? versions.SingleOrDefault(version => version.Id == currentId)
            : null;

        return new ObjectCard(pdmObject, versions, current);
    }

    /// <summary>
    /// Раскрывает состав объекта и считает массу, спецификацию и циклы.
    /// </summary>
    public async Task<CompositionView?> GetCompositionAsync(long objectId, CancellationToken ct = default)
    {
        var repository = _unitOfWorkFactory.CreateReadRepository();
        var tree = await repository.GetTreeAsync(objectId, BomCalculator.MaxBomDepth, ct).ConfigureAwait(false);

        if (tree.Count == 0)
        {
            return null;
        }

        // Циклы ищутся по графу прямых связей: раскрытое дерево обрывается по
        // глубине и цикл в нём не виден.
        var links = new List<BomLinkRow>();
        foreach (var node in tree)
        {
            links.AddRange(await repository.GetLinksAsync(node.VersionId, ct).ConfigureAwait(false));
        }

        return new CompositionView(
            tree,
            BomTreeBuilder.Build(tree),
            _calculator.CalculateMass(tree),
            _calculator.BuildSpecification(tree),
            _calculator.FindCycles(BomLinkGraph.ToObjectIds(tree, links)));
    }
}
