using MiniPdm.Application.Persistence;

namespace MiniPdm.Application.Calculations;

/// <summary>
/// Результат расчёта массы изделия.
/// </summary>
/// <param name="TotalMassKg">
/// Итоговая масса. <c>null</c>, если хотя бы у одной позиции состава масса не заполнена:
/// в этом случае итог был бы занижен, поэтому он не показывается.
/// </param>
/// <param name="NodesWithUnknownMass">Позиции с незаполненной массой.</param>
public sealed record MassCalculationResult(decimal? TotalMassKg, IReadOnlyList<BomTreeNode> NodesWithUnknownMass)
{
    /// <summary>Возвращает <c>true</c>, если итог посчитан полностью.</summary>
    public bool IsComplete => TotalMassKg.HasValue;
}