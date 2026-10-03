using MiniPdm.Application.Calculations;
using MiniPdm.Application.Lifecycle;
using MiniPdm.Application.Queries;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Persistence;

namespace MiniPdm.Tests;

/// <summary>
/// Проверка службы запросов, которую использует интерфейс.
/// </summary>
/// <remarks>
/// Сценарий тот же, что и в <see cref="CadImportServiceTests"/>, но проверяется
/// не сам импорт, а то, что интерфейс получает из базы ожидаемую карточку,
/// состав, массу и спецификацию.
/// </remarks>
public sealed class PdmQueriesTests : ImportTestBase
{
    private PdmQueries Queries() =>
        new(new PdmUnitOfWorkFactory(ConnectionFactory), new BomCalculator());

    private async Task<long> ImportDefaultAsync()
    {
        foreach (var document in new[]
                 {
                     Cad.Assembly(Cad.AssemblyFile, "АБВГ.111111.001", "Гайка", Cad.Component(Cad.BoltFile, 2), Cad.Component(Cad.WasherFile, 2)),
                     Cad.Part(Cad.BoltFile, "АБВГ.111111.002", "Болт", 0.01m),
                     Cad.StandardPart(Cad.WasherFile, "Шайба ГОСТ 11371-78", 0.002m),
                 })
        {
            Catalog.Add(document.FileName);
            Reader.WithDocument(document);
        }

        var report = await ImportAsync();
        Assert.False(report.HasErrors, "Эталонный набор должен импортироваться без ошибок.");

        var assembly = await Read().FindByDesignationAsync("АБВГ.111111.001");
        Assert.NotNull(assembly);

        return assembly!.Id;
    }

    [Fact]
    public async Task SearchAsync_FindsObjectByDesignationFragment()
    {
        await ImportDefaultAsync();

        var found = await Queries().SearchAsync("111111.001");

        Assert.Equal("Гайка", Assert.Single(found).Name);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsAllObjects()
    {
        await ImportDefaultAsync();

        // Гайка, болт и шайба — по одному объекту на документ.
        Assert.Equal(3, (await Queries().SearchAsync(null)).Count);
    }

    [Fact]
    public async Task GetCardAsync_ReturnsCurrentVersionWithAllowedTransitions()
    {
        var objectId = await ImportDefaultAsync();

        var card = await Queries().GetCardAsync(objectId);

        Assert.NotNull(card);
        Assert.Equal("Гайка", card!.Object.Name);
        Assert.NotNull(card.CurrentVersion);
        Assert.Equal(VersionState.InWork, card.CurrentVersion!.State);
        Assert.Equal(
            new[] { VersionState.Approved, VersionState.Annulled }.Order(),
            card.AllowedTransitions.Order());
    }

    [Fact]
    public async Task GetCardAsync_ForUnknownObject_ReturnsNull()
    {
        await ImportDefaultAsync();

        Assert.Null(await Queries().GetCardAsync(9999));
    }

    [Fact]
    public async Task GetCardAsync_AfterApproval_HasNoAnnulledTransition()
    {
        var objectId = await ImportDefaultAsync();
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));

        await lifecycle.ChangeStateAsync(objectId, VersionState.Approved);

        var card = await Queries().GetCardAsync(objectId);

        Assert.Equal(VersionState.Approved, card!.CurrentVersion!.State);
        Assert.Equal(new[] { VersionState.Annulled }, card.AllowedTransitions);
    }

    [Fact]
    public async Task GetCompositionAsync_ReturnsHierarchyMassAndSpecification()
    {
        var objectId = await ImportDefaultAsync();

        var composition = await Queries().GetCompositionAsync(objectId);

        Assert.NotNull(composition);

        // Иерархия: гайка → болт и шайба.
        var root = Assert.Single(composition!.Roots);
        Assert.Equal(objectId, root.ObjectId);
        Assert.Equal(2, root.Children.Count);

        // Масса: 2 × 0.01 + 2 × 0.002 = 0.024 кг.
        Assert.True(composition.Mass.IsComplete);
        Assert.Equal(0.024m, composition.Mass.TotalMassKg);

        Assert.Equal(2, composition.Specification.Count);
        Assert.Empty(composition.Cycles);
    }

    [Fact]
    public async Task GetCompositionAsync_MultipliesQuantityInSpecification()
    {
        var objectId = await ImportDefaultAsync();

        var composition = await Queries().GetCompositionAsync(objectId);

        var bolt = Assert.Single(composition!.Specification, row => row.Name == "Болт");
        Assert.Equal(2, bolt.Quantity);
    }

    [Fact]
    public async Task GetCompositionAsync_AfterAnnullingLastVersion_ReturnsNull()
    {
        var objectId = await ImportDefaultAsync();
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));

        await lifecycle.ChangeStateAsync(objectId, VersionState.Annulled);

        // Аннулированная версия не отображается в дереве состава.
        Assert.Null(await Queries().GetCompositionAsync(objectId));
    }
}
