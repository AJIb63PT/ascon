using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Узел состава изделия вместе с потомками.
/// </summary>
/// <remarks>
/// Рекурсивный запрос возвращает плоский список, в котором один и тот же объект
/// может встречаться в разных ветвях. Иерархия собирается отдельным шагом, чтобы
/// представление не занималось разбором плоского списка.
/// </remarks>
/// <param name="ObjectId">Объект узла.</param>
/// <param name="VersionId">Использованная версия.</param>
/// <param name="Type">Тип объекта.</param>
/// <param name="Designation">Обозначение.</param>
/// <param name="Name">Наименование.</param>
/// <param name="State">Состояние версии.</param>
/// <param name="Material">Материал.</param>
/// <param name="MassKg">Масса одного экземпляра.</param>
/// <param name="Quantity">Количество с учётом вложенности.</param>
/// <param name="Depth">Уровень вложенности.</param>
/// <param name="Children">Потомки.</param>
public sealed record BomNode(
    long ObjectId,
    long VersionId,
    PdmObjectType Type,
    string? Designation,
    string Name,
    VersionState State,
    string? Material,
    decimal? MassKg,
    int Quantity,
    int Depth,
    IReadOnlyList<BomNode> Children)
{
    /// <summary>Обозначение или наименование стандартного изделия.</summary>
    public string DisplayKey => string.IsNullOrWhiteSpace(Designation) ? Name : Designation;
}

/// <summary>Собирает иерархию из плоского результата рекурсивного запроса.</summary>
public static class BomTreeBuilder
{
    /// <summary>
    /// Преобразует плоский список узлов в дерево.
    /// </summary>
/// <remarks>
/// Родитель определяется парой «объект родителя + глубина». Одного идентификатора
/// мало: одна и та же сборка может стоять в нескольких ветвях на разной глубине,
/// и каждому её вхождению нужен собственный потомок.
///
/// Ключ достаточен, потому что повтор одного и того же родителя на одной
/// глубине невозможен: повтор компонента внутри одной сборки отклоняется при
/// импорте, а значит и родитель не может оказаться в двух ветвях на одном
/// уровне.
/// </remarks>
    /// <param name="rows">Плоский список узлов.</param>
    /// <returns>Корневые узлы дерева; обычно это один узел.</returns>
    public static IReadOnlyList<BomNode> Build(IReadOnlyList<BomTreeNode> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return Array.Empty<BomNode>();
        }

        // Ключ узла во плоском списке: родитель плюс глубина. Ключ нужен и для
        // поиска потомков, и для защиты от повторного построения.
        var childrenByKey = new Dictionary<(long? ParentObjectId, int Depth), List<BomTreeNode>>();

        foreach (var row in rows)
        {
            var key = (row.ParentObjectId, row.Depth);
            if (!childrenByKey.TryGetValue(key, out var children))
            {
                children = new List<BomTreeNode>();
                childrenByKey[key] = children;
            }

            children.Add(row);
        }

        return BuildNodes(childrenByKey, parentObjectId: null, depth: 0);
    }

    private static IReadOnlyList<BomNode> BuildNodes(
        IReadOnlyDictionary<(long? ParentObjectId, int Depth), List<BomTreeNode>> childrenByKey,
        long? parentObjectId,
        int depth)
    {
        if (!childrenByKey.TryGetValue((parentObjectId, depth), out var rows))
        {
            return Array.Empty<BomNode>();
        }

        var result = new List<BomNode>(rows.Count);

        foreach (var row in rows)
        {
            var children = BuildNodes(childrenByKey, row.ObjectId, depth + 1);

            result.Add(new BomNode(
                row.ObjectId,
                row.VersionId,
                row.Type,
                row.Designation,
                row.Name,
                row.State,
                row.Material,
                row.MassKg,
                row.Quantity,
                row.Depth,
                children));
        }

        return result;
    }
}
