using MiniPdm.Application.Persistence;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Участок зацикливания состава.
/// </summary>
/// <param name="ObjectIds">Объекты, образующие цикл, в порядке обхода.</param>
public sealed record BomCycle(IReadOnlyList<long> ObjectIds)
{
    /// <summary>Человекочитаемое описание цикла.</summary>
    public override string ToString() => string.Join(" → ", ObjectIds.Select(id => id.ToString()));
}

/// <summary>
/// Служба расчётов по раскрытому составу.
/// </summary>
public interface IBomCalculator
{
    /// <summary>
    /// Считает массу изделия с учётом вложенности.
    /// </summary>
    /// <remarks>
    /// Масса сборки не берётся из её карточки: она складывается из масс
    /// компонентов, поэтому неполные данные не приводят к занижению итога.
    /// </remarks>
    MassCalculationResult CalculateMass(IReadOnlyList<BomTreeNode> nodes);

    /// <summary>
    /// Сводит раскрытый состав в спецификацию: одна строка на объект.
    /// </summary>
    IReadOnlyList<SpecificationRow> BuildSpecification(IReadOnlyList<BomTreeNode> nodes);

    /// <summary>
    /// Находит циклы по графу прямых связей «родитель → потомок».
    /// </summary>
    /// <remarks>
    /// Раскрытое дерево для этого непригодно: рекурсивный запрос ограничен по
    /// глубине и обрывается раньше, чем цикл станет виден.
    /// </remarks>
    /// <param name="links">Прямые связи состава.</param>
    IReadOnlyList<BomCycle> FindCycles(IReadOnlyCollection<BomLinkRow> links);
}