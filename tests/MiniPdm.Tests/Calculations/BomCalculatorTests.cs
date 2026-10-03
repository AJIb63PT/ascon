using MiniPdm.Application.Calculations;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Tests.Calculations;

/// <summary>
/// Проверка расчётов по раскрытому составу.
/// </summary>
public sealed class BomCalculatorTests
{
    private readonly BomCalculator _calculator = new();

    /// <summary>Собирает узел раскрытого состава.</summary>
    private static BomTreeNode Node(
        long objectId,
        PdmObjectType type,
        decimal? massKg,
        int quantity,
        int depth,
        long? parentObjectId = null,
        string? designation = "АБВГ.111111.001",
        string name = "Деталь") =>
        new(
            ObjectId: objectId,
            VersionId: objectId,
            ParentObjectId: parentObjectId,
            Type: type,
            Designation: designation,
            Name: name,
            State: VersionState.Approved,
            VersionNo: 1,
            Material: "Сталь",
            MassKg: massKg,
            Quantity: quantity,
            Depth: depth);

    [Fact]
    public void CalculateMass_SumsQuantitiesAcrossAllLevels()
    {
        // Сборка из двух болтов и двух шайб: 2*0.01 + 2*0.002
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, massKg: null, quantity: 1, depth: 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Part, massKg: 0.01m, quantity: 2, depth: 1, parentObjectId: 1),
            Node(3, PdmObjectType.StandardPart, massKg: 0.002m, quantity: 2, depth: 1, parentObjectId: 1, designation: null),
        };

        var result = _calculator.CalculateMass(nodes);

        Assert.True(result.IsComplete);
        Assert.Equal(0.024m, result.TotalMassKg);
        Assert.Empty(result.NodesWithUnknownMass);
    }

    [Fact]
    public void CalculateMass_MultipliesQuantityByPathDepth()
    {
        // Узел на глубине 2: количество 4 = 2 (сборка) * 2 (промежуточная сборка).
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Assembly, null, 2, 1, parentObjectId: 1, designation: "АБВГ.111111.001"),
            Node(3, PdmObjectType.Part, 0.5m, 4, 2, parentObjectId: 2),
        };

        var result = _calculator.CalculateMass(nodes);

        Assert.Equal(2.0m, result.TotalMassKg);
    }

    [Fact]
    public void CalculateMass_ReturnsNullWhenAnyPartMassIsUnknown()
    {
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Part, 0.01m, 2, 1, parentObjectId: 1),
            Node(3, PdmObjectType.Part, null, 1, 1, parentObjectId: 1, designation: "АБВГ.111111.002"),
        };

        var result = _calculator.CalculateMass(nodes);

        Assert.False(result.IsComplete);
        Assert.Null(result.TotalMassKg);
        Assert.Contains(result.NodesWithUnknownMass, node => node.ObjectId == 3);
    }

    [Fact]
    public void CalculateMass_IgnoresAssemblyOwnMass()
    {
        // У сборки заполнена масса в карточке, но доверять ей нельзя:
        // итог должен считаться по компонентам.
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, 999m, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Part, 0.01m, 2, 1, parentObjectId: 1),
        };

        var result = _calculator.CalculateMass(nodes);

        Assert.Equal(0.02m, result.TotalMassKg);
    }

    [Fact]
    public void CalculateMass_TreatsAssemblyWithoutOwnMassAsComplete()
    {
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Part, 0.01m, 2, 1, parentObjectId: 1),
        };

        var result = _calculator.CalculateMass(nodes);

        Assert.True(result.IsComplete);
        Assert.Equal(0.02m, result.TotalMassKg);
    }

    [Fact]
    public void BuildSpecification_MergesRepeatedObjectsIntoOneRow()
    {
        // Один и тот же болт (идентификатор 3) попадает в состав дважды:
        // напрямую и через промежуточную сборку. В спецификации — одна строка.
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Assembly, null, 1, 1, parentObjectId: 1, designation: "АБВГ.111111.001"),
            Node(3, PdmObjectType.Part, 0.01m, 2, 1, parentObjectId: 1, designation: "АБВГ.111111.002"),
            Node(3, PdmObjectType.Part, 0.01m, 3, 2, parentObjectId: 2, designation: "АБВГ.111111.002"),
        };

        var rows = _calculator.BuildSpecification(nodes);

        var bolt = Assert.Single(rows, row => row.ObjectId == 3);
        Assert.Equal(5, bolt.Quantity);
        Assert.Equal(2, bolt.Occurrences);
        Assert.Equal(0.05m, bolt.TotalMassKg);
    }

    [Fact]
    public void BuildSpecification_ExcludesRootAssembly()
    {
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Part, 0.01m, 1, 1, parentObjectId: 1, designation: "АБВГ.111111.001"),
        };

        var rows = _calculator.BuildSpecification(nodes);

        Assert.DoesNotContain(rows, row => row.ObjectId == 1);
        Assert.Single(rows);
    }

    [Fact]
    public void BuildSpecification_PutsAssembliesFirst()
    {
        var nodes = new[]
        {
            Node(1, PdmObjectType.Assembly, null, 1, 0, designation: "АБВГ.111111.000"),
            Node(2, PdmObjectType.Assembly, null, 1, 1, parentObjectId: 1, designation: "АБВГ.111111.001"),
            Node(3, PdmObjectType.Part, 0.01m, 1, 1, parentObjectId: 1, designation: "АБВГ.111111.002"),
            Node(4, PdmObjectType.StandardPart, 0.002m, 1, 1, parentObjectId: 1, designation: null, name: "Болт"),
        };

        var rows = _calculator.BuildSpecification(nodes);

        // Корневая сборка в спецификацию не входит, поэтому сборка здесь одна.
        Assert.Equal(PdmObjectType.Assembly, rows[0].Type);
        Assert.Equal(PdmObjectType.Part, rows[1].Type);
        Assert.Equal(PdmObjectType.StandardPart, rows[2].Type);
    }

    [Fact]
    public void FindCycles_DetectsMutualReference()
    {
        // Раскрытое дерево не может содержать цикл: рекурсивный запрос
        // ограничен по глубине. Цикл ищется по графу прямых связей.
        var links = new[]
        {
            new BomLinkRow(ParentVersionId: 1, ChildObjectId: 2, Quantity: 1),
            new BomLinkRow(ParentVersionId: 2, ChildObjectId: 1, Quantity: 1),
        };

        var cycles = _calculator.FindCycles(links);

        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void FindCycles_DetectsSelfReference()
    {
        var links = new[]
        {
            new BomLinkRow(ParentVersionId: 1, ChildObjectId: 1, Quantity: 1),
        };

        var cycles = _calculator.FindCycles(links);

        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void FindCycles_ReturnsEmptyForAcyclicLinks()
    {
        var links = new[]
        {
            new BomLinkRow(ParentVersionId: 1, ChildObjectId: 2, Quantity: 1),
            new BomLinkRow(ParentVersionId: 2, ChildObjectId: 3, Quantity: 1),
        };

        Assert.Empty(_calculator.FindCycles(links));
    }

    [Fact]
    public void FindCycles_AfterTranslation_FindsCycleWhenVersionIdsDifferFromObjectIds()
    {
        // Объекты 1 и 2 содержат друг друга, но их версии имеют номера 40 и 41.
        // Без перевода номер версии сравнивался бы с номером объекта, и цикл
        // либо терялся, либо случайное совпадение считалось замыканием.
        var tree = new[]
        {
            new BomTreeNode(
                ObjectId: 1, VersionId: 40, ParentObjectId: null, Type: PdmObjectType.Assembly,
                Designation: "АБВГ.111111.001", Name: "Сборка 1", State: VersionState.Approved,
                VersionNo: 1, Material: null, MassKg: null, Quantity: 1, Depth: 0),
            new BomTreeNode(
                ObjectId: 2, VersionId: 41, ParentObjectId: 1, Type: PdmObjectType.Assembly,
                Designation: "АБВГ.111111.002", Name: "Сборка 2", State: VersionState.Approved,
                VersionNo: 1, Material: null, MassKg: null, Quantity: 1, Depth: 1),
        };

        var links = new[]
        {
            new BomLinkRow(ParentVersionId: 40, ChildObjectId: 2, Quantity: 1),
            new BomLinkRow(ParentVersionId: 41, ChildObjectId: 1, Quantity: 1),
        };

        var cycles = _calculator.FindCycles(BomLinkGraph.ToObjectIds(tree, links));

        var cycle = Assert.Single(cycles);
        Assert.Equal(2, cycle.ObjectIds.Count);
    }
}