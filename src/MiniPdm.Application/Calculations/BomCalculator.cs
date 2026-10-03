using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Расчёты по раскрытому составу изделия.
/// </summary>
public sealed class BomCalculator : IBomCalculator
{
    /// <summary>Максимальная глубина раскрытия.</summary>
    public const int MaxBomDepth = 64;

    /// <inheritdoc />
    public MassCalculationResult CalculateMass(IReadOnlyList<BomTreeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        // Масса детали и стандартного изделия берётся из карточки, масса сборки
        // складывается из компонентов. Позиции без массы делают итог недостоверным,
        // поэтому он не выдаётся вовсе, а не показывается заниженным.
        var unknown = nodes
            .Where(node => !node.HasMass && node.Type != PdmObjectType.Assembly)
            .ToArray();

        if (unknown.Length > 0)
        {
            return new MassCalculationResult(null, unknown);
        }

        decimal total = 0m;
        foreach (var node in nodes)
        {
            if (node.Type == PdmObjectType.Assembly)
            {
                continue;
            }

            total += node.TotalMassKg!.Value;
        }

        return new MassCalculationResult(total, Array.Empty<BomTreeNode>());
    }

    /// <inheritdoc />
    public IReadOnlyList<SpecificationRow> BuildSpecification(IReadOnlyList<BomTreeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        // Корневую сборку в спецификацию не включаем: спецификация перечисляет
        // состав изделия, а не само изделие.
        return nodes
            .Where(node => node.ParentObjectId is not null)
            .GroupBy(node => node.ObjectId)
            .Select(group => new SpecificationRow(
                group.Key,
                group.First().Type,
                group.First().Designation,
                group.First().Name,
                group.First().Material,
                group.Sum(node => node.Quantity),
                group.First().MassKg,
                group.Count()))
            .OrderBy(row => row.Type == PdmObjectType.Assembly ? 0 : 1)
            .ThenBy(row => row.DisplayKey, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<BomCycle> FindCycles(IReadOnlyCollection<BomLinkRow> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        // Цикл есть тогда и только тогда, когда при обходе связей «родитель →
        // потомок» встречается объект, уже пройденный на этом пути.
        var childrenByParent = new Dictionary<long, List<long>>();
        foreach (var link in links)
        {
            if (!childrenByParent.TryGetValue(link.ParentVersionId, out var children))
            {
                children = new List<long>();
                childrenByParent[link.ParentVersionId] = children;
            }

            children.Add(link.ChildObjectId);
        }

        var cycles = new List<BomCycle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var start in childrenByParent.Keys.OrderBy(id => id))
        {
            foreach (var cycle in Walk(start, childrenByParent))
            {
                // Один и тот же цикл обходится с каждой своей вершины, поэтому
                // путь приводится к каноническому виду: начинается с наименьшего
                // идентификатора и идёт по возрастанию.
                var key = Canonical(cycle);

                if (seen.Add(string.Join(",", key.Select(id => id.ToString()))))
                {
                    cycles.Add(new BomCycle(key));
                }
            }
        }

        return cycles;
    }

    /// <summary>
    /// Приводит путь цикла к каноническому виду: поворачивает к началу с
    /// наименьшим идентификатором и разворачивает, если после поворота путь не
    /// возрастает.
    /// </summary>
    /// <remarks>
    /// Без этого один цикл сообщался столько раз, сколько в нём вершин: обход
    /// начинал его с каждой из них.
    /// </remarks>
    private static IReadOnlyList<long> Canonical(IReadOnlyList<long> cycle)
    {
        var smallest = cycle.Min();
        var smallestAt = Enumerable.Range(0, cycle.Count).First(index => cycle[index] == smallest);

        var rotated = cycle.Skip(smallestAt).Concat(cycle.Take(smallestAt)).ToArray();

        return IsAscending(rotated) ? rotated : rotated.Reverse().ToArray();
    }

    private static bool IsAscending(IReadOnlyList<long> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i - 1] >= values[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Обход в глубину с отметками посещённых вершин текущего пути.
    /// Вершина, встреченная повторно на том же пути, замыкает цикл.
    /// </summary>
    private static IEnumerable<IReadOnlyList<long>> Walk(
        long start,
        IReadOnlyDictionary<long, List<long>> childrenByParent)
    {
        var onPath = new HashSet<long>();
        var path = new List<long>();
        return Visit(start);

        IEnumerable<IReadOnlyList<long>> Visit(long objectId)
        {
            if (!onPath.Add(objectId))
            {
                var index = path.IndexOf(objectId);
                if (index >= 0)
                {
                    // Возвращаем именно цикл, а не весь путь до корня.
                    yield return path.Skip(index).ToArray();
                }

                yield break;
            }

            path.Add(objectId);

            if (childrenByParent.TryGetValue(objectId, out var children))
            {
                foreach (var child in children)
                {
                    foreach (var cycle in Visit(child))
                    {
                        yield return cycle;
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            onPath.Remove(objectId);
        }
    }
}