using MiniPdm.Application.Persistence;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Приводит связи состава к единому пространству идентификаторов объектов.
/// </summary>
/// <remarks>
/// В хранилище родитель связи задан версией, а потомок — объектом. Смешивать
/// эти пространства нельзя: номер версии и номер объекта равны лишь случайно,
/// поэтому цикл то терялся бы, то случайное совпадение принималось бы за
/// замыкание. Поиск циклов работает только с идентификаторами объектов.
/// </remarks>
public static class BomLinkGraph
{
    /// <summary>
    /// Переводит идентификатор родителя каждой связи из номера версии в номер
    /// объекта.
    /// </summary>
    /// <param name="tree">Раскрытый состав, из которого берётся соответствие.</param>
    /// <param name="links">Прямые связи состава.</param>
    /// <returns>
    /// Связи в едином пространстве идентификаторов объектов. Связь, версия
    /// родителя которой отсутствует в раскрытом составе, возвращается без
    /// изменения: её невозможно ни сопоставить, ни проверить на замыкание.
    /// </returns>
    /// <remarks>
    /// Соответствие достаточно полно, если связи собраны для всех вершин
    /// раскрытого состава: тогда версия родителя всегда присутствует в дереве.
    /// </remarks>
    public static IReadOnlyList<BomLinkRow> ToObjectIds(
        IReadOnlyList<BomTreeNode> tree,
        IReadOnlyList<BomLinkRow> links)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(links);

        var objectIdByVersionId = new Dictionary<long, long>();

        foreach (var node in tree)
        {
            // Одна версия встречается в плоском списке столько раз, сколько ветвей
            // её используют, поэтому соответствие дополняется, а не перезаписывается.
            objectIdByVersionId[node.VersionId] = node.ObjectId;
        }

        return links
            .Select(link => objectIdByVersionId.TryGetValue(link.ParentVersionId, out var parentObjectId)
                ? link with { ParentVersionId = parentObjectId }
                : link)
            .ToArray();
    }
}
