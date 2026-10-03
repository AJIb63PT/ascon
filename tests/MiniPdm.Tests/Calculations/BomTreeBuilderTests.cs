using MiniPdm.Application.Calculations;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Tests.Calculations;

/// <summary>
/// Сборка иерархии из плоского результата рекурсивного запроса.
/// </summary>
public sealed class BomTreeBuilderTests
{
    private static BomTreeNode Node(
        long objectId,
        long? parentObjectId,
        int depth,
        PdmObjectType type = PdmObjectType.Part,
        int quantity = 1,
        string? name = null) =>
        new(
            objectId,
            objectId,
            parentObjectId,
            type,
            $"АБВГ.111111.{objectId:000}",
            name ?? $"Объект {objectId}",
            VersionState.InWork,
            1,
            "Сталь",
            0.1m,
            quantity,
            depth);

    [Fact]
    public void Build_WithoutRows_ReturnsEmptyTree()
    {
        Assert.Empty(BomTreeBuilder.Build(Array.Empty<BomTreeNode>()));
    }

    [Fact]
    public void Build_SingleRoot_ReturnsItAsRoot()
    {
        var roots = BomTreeBuilder.Build(new[] { Node(1, parentObjectId: null, depth: 0) });

        var root = Assert.Single(roots);
        Assert.Equal(1, root.ObjectId);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Build_NestedComposition_AttachesChildrenByParentAndDepth()
    {
        var rows = new[]
        {
            Node(1, parentObjectId: null, depth: 0, PdmObjectType.Assembly),
            Node(2, parentObjectId: 1, depth: 1, PdmObjectType.Assembly),
            Node(3, parentObjectId: 2, depth: 2),
        };

        var root = Assert.Single(BomTreeBuilder.Build(rows));

        var child = Assert.Single(root.Children);
        Assert.Equal(2, child.ObjectId);

        var grandChild = Assert.Single(child.Children);
        Assert.Equal(3, grandChild.ObjectId);
    }

    [Fact]
    public void Build_SameAssemblyAtDifferentDepths_KeepsSeparateNodes()
    {
        // Сборка 2 встречается и напрямую в корне, и внутри сборки 3:
        // в плоском списке это две строки с одним и тем же ObjectId.
        var rows = new[]
        {
            Node(1, parentObjectId: null, depth: 0, PdmObjectType.Assembly),
            Node(2, parentObjectId: 1, depth: 1, PdmObjectType.Assembly),
            Node(3, parentObjectId: 1, depth: 1, PdmObjectType.Assembly),
            Node(4, parentObjectId: 2, depth: 2),
            Node(2, parentObjectId: 3, depth: 2, PdmObjectType.Assembly),
        };

        var root = Assert.Single(BomTreeBuilder.Build(rows));
        Assert.Equal(2, root.Children.Count);

        var direct = root.Children.Single(child => child.ObjectId == 2);
        var nested = root.Children.Single(child => child.ObjectId == 3);

        // Одна и та же сборка 2 в двух ветвях: каждая ветвь раскрыта отдельно.
        var part = Assert.Single(direct.Children);
        Assert.Equal(4, part.ObjectId);

        var nestedCopy = Assert.Single(nested.Children);
        Assert.Equal(2, nestedCopy.ObjectId);
        Assert.Empty(nestedCopy.Children);
    }

    [Fact]
    public void Build_KeepsQuantityMultipliedAlongThePath()
    {
        var rows = new[]
        {
            Node(1, parentObjectId: null, depth: 0, PdmObjectType.Assembly),
            Node(2, parentObjectId: 1, depth: 1, PdmObjectType.Assembly, quantity: 4),
            Node(3, parentObjectId: 2, depth: 2, quantity: 12),
        };

        var root = Assert.Single(BomTreeBuilder.Build(rows));
        var assembly = Assert.Single(root.Children);
        var part = Assert.Single(assembly.Children);

        Assert.Equal(4, assembly.Quantity);
        Assert.Equal(12, part.Quantity);
    }

    [Fact]
    public void Build_ExposesDisplayKeyFallingBackToName()
    {
        var rows = new[]
        {
            new BomTreeNode(
                ObjectId: 1,
                VersionId: 1,
                ParentObjectId: null,
                Type: PdmObjectType.StandardPart,
                Designation: null,
                Name: "Болт М8 ГОСТ 7798-70",
                State: VersionState.InWork,
                VersionNo: 1,
                Material: "Сталь",
                MassKg: 0.02m,
                Quantity: 1,
                Depth: 0),
        };

        var root = Assert.Single(BomTreeBuilder.Build(rows));
        Assert.Equal("Болт М8 ГОСТ 7798-70", root.DisplayKey);
    }
}
