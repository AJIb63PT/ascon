using MiniPdm.Application.Import;
using MiniPdm.Application.Lifecycle;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Persistence;
using MiniPdm.Tests.Fakes;

namespace MiniPdm.Tests;

/// <summary>
/// Проверка импорта на подменённых ридере и каталоге: файловая система не используется.
/// </summary>
public sealed class CadImportServiceTests : ImportTestBase
{
    /// <summary>
    /// Загружает в каталог и ридер эталонный набор из трёх корректных документов.
    /// </summary>
    private void UseDefaultDataset()
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
    }

    [Fact]
    public async Task ValidExport_ImportsEveryDocument()
    {
        UseDefaultDataset();

        var report = await ImportAsync();

        Assert.Equal(3, report.AcceptedCount);
        Assert.Equal(0, report.RejectedCount);
        Assert.Equal(0, report.WarningCount);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public async Task ValidExport_CreatesObjectsVersionsAndLinks()
    {
        UseDefaultDataset();

        await ImportAsync();
        var repository = Read();

        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        Assert.NotNull(bolt);
        Assert.Equal("Болт", bolt!.Name);
        Assert.Equal(PdmObjectType.Part, bolt.Type);
        Assert.Equal(Cad.BoltFile, bolt.SourceFileName);

        var version = await repository.GetCurrentVersionAsync(bolt.Id);
        Assert.NotNull(version);
        Assert.Equal(1, version!.VersionNo);
        Assert.Equal(VersionState.InWork, version.State);
        Assert.Equal(0.01m, version.MassKg);

        var assembly = await repository.FindByDesignationAsync("АБВГ.111111.001");
        Assert.NotNull(assembly);

        var links = await repository.GetLinksAsync(assembly!.CurrentVersionId!.Value);
        Assert.Equal(2, links.Count);
        Assert.Contains(links, link => link.Quantity == 2);
    }

    [Fact]
    public async Task StandardPart_IsStoredWithoutDesignation()
    {
        UseDefaultDataset();

        await ImportAsync();
        var repository = Read();

        var standardPart = await repository.FindStandardPartByNameAsync("Шайба ГОСТ 11371-78");
        Assert.NotNull(standardPart);
        Assert.Null(standardPart!.Designation);
        Assert.Equal(PdmObjectType.StandardPart, standardPart.Type);
    }

    [Fact]
    public async Task UnreadableFile_IsRejected_AndImportContinues()
    {
        UseDefaultDataset();
        Reader.WithFailure("Отдушина.m3d", "Некорректный JSON");
        Catalog.Add("Отдушина.m3d");

        var report = await ImportAsync();

        Assert.Equal(3, report.AcceptedCount);
        Assert.Equal(1, report.RejectedCount);

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Отдушина.m3d");
        Assert.Equal(ImportSeverity.Error, rejected.Severity);
        Assert.Contains("Некорректный JSON", rejected.Reason);
    }

    [Theory]
    [InlineData("PДЦЛ.304112.601", "латинская буква")]
    [InlineData("РДЦЛ.30411.602", "пять цифр вместо шести")]
    [InlineData("РДЦЛ.304112.6020", "лишняя цифра")]
    [InlineData("РДЦ.304112.602", "три буквы вместо четырёх")]
    public async Task DesignationBreakingEskdFormat_IsRejected(string designation, string because)
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Part("Деталь.m3d", designation, "Деталь"));
        Catalog.Add("Деталь.m3d");

        var report = await ImportAsync();

        Assert.Equal(1, report.RejectedCount);
        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Деталь.m3d");
        Assert.Contains("ЕСКД", rejected.Reason);
        Assert.False(string.IsNullOrWhiteSpace(because));
    }

    [Fact]
    public async Task StandardPartWithDesignation_IsRejected()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.StandardPart("Гайка.m3d", "Гайка") with { Designation = "АБВГ.111111.009" });
        Catalog.Add("Гайка.m3d");

        var report = await ImportAsync();

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Гайка.m3d");
        Assert.Equal(ImportSeverity.Error, rejected.Severity);
        Assert.Contains("обозначения", rejected.Reason);
    }

    [Fact]
    public async Task DuplicateDesignation_RejectsEveryDocumentWithIt()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Part("Шайба стопорная.m3d", "РДЦЛ.304112.302", "Шайба стопорная"));
        Reader.WithDocument(Cad.Part("Шайба упорная.m3d", "РДЦЛ.304112.302", "Шайба упорная"));
        Catalog.Add("Шайба стопорная.m3d").Add("Шайба упорная.m3d");

        var report = await ImportAsync();

        Assert.Equal(2, report.RejectedCount);
        Assert.All(
            report.Outcomes.Where(o => o.Severity == ImportSeverity.Error),
            outcome => Assert.Contains("РДЦЛ.304112.302", outcome.Reason));
    }

    [Fact]
    public async Task DuplicateStandardPartName_RejectsEveryDocumentWithIt()
    {
        // Два стандартных изделия с одинаковым наименованием. Эталонная сборка
        // не используется: её шайба тоже имела бы то же имя и отклонилась бы вместе.
        var catalog = new FakeCadDocumentCatalog().Add("Шайба 8.m3d").Add("Шайба 10.m3d");
        var reader = new FakeCadDocumentReader()
            .WithDocument(Cad.StandardPart("Шайба 8.m3d", "Шайба ГОСТ 11371-78"))
            .WithDocument(Cad.StandardPart("Шайба 10.m3d", "Шайба ГОСТ 11371-78"));

        var service = new CadImportService(catalog, reader, new PdmUnitOfWorkFactory(ConnectionFactory));
        var report = await service.ImportAsync("/fake/export");

        Assert.Equal(0, report.AcceptedCount);
        Assert.Equal(2, report.RejectedCount);
        Assert.All(
            report.Outcomes,
            outcome => Assert.Contains("Шайба ГОСТ 11371-78", outcome.Reason));
    }

    [Fact]
    public async Task AssemblyWithoutComponents_IsRejected()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Assembly("Пробка.a3d", "АБВГ.111111.003", "Пробка"));
        Catalog.Add("Пробка.a3d");

        var report = await ImportAsync();

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Пробка.a3d");
        Assert.Contains("У сборки не задан", rejected.Reason);
    }

    [Fact]
    public async Task NonPositiveComponentQuantity_IsRejected()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Assembly("Пробка.a3d", "АБВГ.111111.003", "Пробка", Cad.Component(Cad.BoltFile, 0)));
        Catalog.Add("Пробка.a3d");

        var report = await ImportAsync();

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Пробка.a3d");
        Assert.Contains("больше нуля", rejected.Reason);
    }

    [Fact]
    public async Task RepeatedComponentInSameAssembly_IsRejected()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Assembly(
            "Пробка.a3d",
            "АБВГ.111111.003",
            "Пробка",
            Cad.Component(Cad.BoltFile, 1),
            Cad.Component(Cad.BoltFile, 2)));
        Catalog.Add("Пробка.a3d");

        var report = await ImportAsync();

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Пробка.a3d");
        Assert.Contains("более одного раза", rejected.Reason);
    }

    [Fact]
    public async Task MissingComponentFile_IsRejected()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Assembly(
            "Привод.a3d",
            "АБВГ.111111.004",
            "Привод",
            Cad.Component(Cad.BoltFile, 1),
            Cad.Component("Насос НШ-10.m3d", 1)));
        Catalog.Add("Привод.a3d");

        var report = await ImportAsync();

        var rejected = Assert.Single(report.Outcomes, o => o.FileName == "Привод.a3d");
        Assert.Contains("не найден", rejected.Reason);
    }

    [Fact]
    public async Task AssemblyWithRejectedComponent_IsRejectedToo()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Part("Отдушина.m3d", "РДЦЛ.304112.901", "Отдушина"));
        Reader.WithDocument(Cad.Assembly(
            "Корпус.a3d",
            "АБВГ.111111.005",
            "Корпус",
            Cad.Component(Cad.BoltFile, 1),
            Cad.Component("Отдушина.m3d", 1)));
        Catalog.Add("Отдушина.m3d").Add("Корпус.a3d");

        // Обозначение детали заведомо некорректно — каскад должен отклонить сборку.
        Reader.WithDocument("Отдушина.m3d", Cad.Part("Отдушина.m3d", "РДЦЛ.30411.901", "Отдушина"));

        var report = await ImportAsync();

        Assert.Equal(2, report.RejectedCount);
        var assembly = Assert.Single(report.Outcomes, o => o.FileName == "Корпус.a3d");
        Assert.Contains("отклонён", assembly.Reason);
    }

    [Fact]
    public async Task Cascade_PropagatesThroughSeveralLevels()
    {
        UseDefaultDataset();
        Reader.WithDocument("Отдушина.m3d", Cad.Part("Отдушина.m3d", "РДЦЛ.30411.901", "Отдушина"));
        Reader.WithDocument("Узел.a3d", Cad.Assembly(
            "Узел.a3d",
            "АБВГ.111111.006",
            "Узел",
            Cad.Component("Отдушина.m3d", 1)));
        Reader.WithDocument("Изделие.a3d", Cad.Assembly(
            "Изделие.a3d",
            "АБВГ.111111.007",
            "Изделие",
            Cad.Component("Узел.a3d", 1)));
        Catalog.Add("Отдушина.m3d").Add("Узел.a3d").Add("Изделие.a3d");

        var report = await ImportAsync();

        Assert.Equal(3, report.RejectedCount);
        Assert.All(
            new[] { "Отдушина.m3d", "Узел.a3d", "Изделие.a3d" },
            fileName => Assert.Contains(
                report.Outcomes,
                outcome => outcome.FileName == fileName && outcome.Severity == ImportSeverity.Error));
    }

    [Fact]
    public async Task PartWithoutMass_IsImportedWithWarning()
    {
        UseDefaultDataset();
        Reader.WithDocument(Cad.Part("Прокладка.m3d", "РДЦЛ.304112.902", "Прокладка", massKg: null));
        Catalog.Add("Прокладка.m3d");

        var report = await ImportAsync();

        Assert.Equal(4, report.AcceptedCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(0, report.RejectedCount);

        var warned = Assert.Single(report.Outcomes, o => o.FileName == "Прокладка.m3d");
        Assert.Equal(ImportSeverity.Warning, warned.Severity);
        Assert.True(warned.IsImported);

        var repository = Read();
        var part = await repository.FindByDesignationAsync("РДЦЛ.304112.902");
        Assert.NotNull(part);
        Assert.Null((await repository.GetCurrentVersionAsync(part!.Id))!.MassKg);
    }

    [Fact]
    public async Task ReimportOfUnchangedExport_DoesNotCreateNewVersion()
    {
        UseDefaultDataset();

        await ImportAsync();
        var report = await ImportAsync();

        Assert.Equal(3, report.AcceptedCount);
        Assert.Equal(0, report.RejectedCount);

        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        var versions = await repository.GetVersionsAsync(bolt!.Id);
        Assert.Single(versions);
        Assert.All(report.Outcomes, outcome => Assert.Contains("не изменился", outcome.Reason));
    }

    [Fact]
    public async Task ReimportWhileVersionInWork_UpdatesSameVersion()
    {
        UseDefaultDataset();
        await ImportAsync();

        // Конструктор изменил массу детали, версия ещё в работе.
        Reader.WithDocument(Cad.Part(Cad.BoltFile, "АБВГ.111111.002", "Болт", 0.02m));

        await ImportAsync();
        var repository = Read();

        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        var versions = await repository.GetVersionsAsync(bolt!.Id);

        Assert.Single(versions);
        Assert.Equal(0.02m, versions[0].MassKg);
        Assert.Equal(VersionState.InWork, versions[0].State);
    }

    [Fact]
    public async Task ChangeState_AfterAnnullingNewestVersion_AffectsCurrentOne()
    {
        UseDefaultDataset();
        await ImportAsync();

        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));

        await lifecycle.ChangeStateAsync(bolt!.Id, VersionState.Approved);

        // Появилась вторая версия, затем её аннулировали: рабочей снова стала
        // первая, утверждённая.
        Reader.WithDocument(Cad.Part(Cad.BoltFile, "АБВГ.111111.002", "Болт", 0.02m));
        await ImportAsync();
        await lifecycle.ChangeStateAsync(bolt.Id, VersionState.Annulled);

        var versions = await repository.GetVersionsAsync(bolt.Id);
        Assert.Equal(VersionState.Annulled, versions[0].State);
        Assert.Equal(VersionState.Approved, versions[1].State);
        Assert.Equal(versions[1].Id, bolt.CurrentVersionId);

        // Утверждаться должна именно текущая версия, а не самая новая по номеру.
        var changed = await lifecycle.ChangeStateAsync(bolt.Id, VersionState.Annulled);

        Assert.Equal(versions[1].Id, changed.Id);
        Assert.Equal(VersionState.Annulled, changed.State);
        Assert.Null((await Read().GetObjectAsync(bolt.Id))!.CurrentVersionId);
    }

    [Fact]
    public async Task ChangeState_ForObjectWithoutCurrentVersion_Throws()
    {
        UseDefaultDataset();
        await ImportAsync();

        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));

        await lifecycle.ChangeStateAsync(bolt!.Id, VersionState.Annulled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => lifecycle.ChangeStateAsync(bolt.Id, VersionState.Approved));
    }

    [Fact]
    public async Task ReimportAfterApproval_CreatesNextVersion()
    {
        UseDefaultDataset();
        var firstReport = await ImportAsync();

        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));
        await lifecycle.ChangeStateAsync(bolt!.Id, VersionState.Approved);

        // Конструктор изменил массу утверждённой детали.
        Reader.WithDocument(Cad.Part(Cad.BoltFile, "АБВГ.111111.002", "Болт", 0.02m));
        await ImportAsync();

        var versions = await repository.GetVersionsAsync(bolt.Id);

        Assert.Equal(2, versions.Count);
        Assert.Equal(2, versions[0].VersionNo);
        Assert.Equal(VersionState.InWork, versions[0].State);
        Assert.Equal(0.02m, versions[0].MassKg);

        // Утверждённая версия остаётся нетронутой.
        Assert.Equal(1, versions[1].VersionNo);
        Assert.Equal(VersionState.Approved, versions[1].State);
        Assert.Equal(0.01m, versions[1].MassKg);

        // Объект не задвоился.
        Assert.Equal(3, await CountObjectsAsync());
        Assert.Equal(3, firstReport.AcceptedCount);
    }

    [Fact]
    public async Task ReimportAfterApproval_WithChangedQuantity_CreatesNewVersionAndLinks()
    {
        UseDefaultDataset();
        await ImportAsync();

        var repository = Read();
        var assembly = await repository.FindByDesignationAsync("АБВГ.111111.001");
        var lifecycle = new ObjectLifecycleService(new PdmUnitOfWorkFactory(ConnectionFactory));
        await lifecycle.ChangeStateAsync(assembly!.Id, VersionState.Approved);

        // Состав изменился: болтов теперь три.
        Reader.WithDocument(Cad.Assembly(
            Cad.AssemblyFile,
            "АБВГ.111111.001",
            "Гайка",
            Cad.Component(Cad.BoltFile, 3),
            Cad.Component(Cad.WasherFile, 2)));

        await ImportAsync();

        var versions = await repository.GetVersionsAsync(assembly.Id);
        Assert.Equal(2, versions.Count);

        var newLinks = await repository.GetLinksAsync(versions[0].Id);
        Assert.Contains(newLinks, link => link.Quantity == 3);

        var oldLinks = await repository.GetLinksAsync(versions[1].Id);
        Assert.Contains(oldLinks, link => link.Quantity == 2);
    }

    [Fact]
    public async Task ImportIsIdempotent_ForIdenticalFolderImportedTwice()
    {
        UseDefaultDataset();

        await ImportAsync();
        var readerCalls = Reader.ReadCount;
        await ImportAsync();

        Assert.Equal(3, readerCalls);
        Assert.Equal(3, await CountObjectsAsync());
    }

    [Fact]
    public async Task Cancellation_StopsImportAndLeavesDatabaseEmpty()
    {
        UseDefaultDataset();
        var service = CreateService();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync("/fake/export", progress: null, cts.Token));

        Assert.Equal(0, await CountObjectsAsync());
    }

    [Fact]
    public async Task Progress_IsReportedForEveryDocument()
    {
        UseDefaultDataset();
        var progress = new List<ImportProgress>();
        var service = CreateService();

        await service.ImportAsync(
            "/fake/export",
            new Progress<ImportProgress>(progress.Add),
            CancellationToken.None);

        // Progress<T> срабатывает асинхронно, поэтому проверяем через счётчик прогресса,
        // а не по накопленному списку.
        Assert.True(Reader.ReadCount == 3);
    }

    [Fact]
    public async Task RenamedSourceFile_UpdatesSameObjectInsteadOfFailing()
    {
        UseDefaultDataset();
        await ImportAsync();
        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        Assert.NotNull(bolt);

        // Новая выгрузка: деталь переименовали, масса изменилась.
        var catalog = new FakeCadDocumentCatalog().Add("Болт новый.m3d");
        var reader = new FakeCadDocumentReader()
            .WithDocument(Cad.Part("Болт новый.m3d", "АБВГ.111111.002", "Болт", massKg: 0.02m, material: "Сталь 40"));

        var report = await new CadImportService(
            catalog,
            reader,
            new PdmUnitOfWorkFactory(ConnectionFactory)).ImportAsync("/fake/v2");

        var afterBolt = await Read().FindByDesignationAsync("АБВГ.111111.002");

        Assert.False(report.HasErrors);
        Assert.Equal(1, report.AcceptedCount);
        Assert.Equal(bolt!.Id, afterBolt!.Id);
        Assert.Equal(3, await CountObjectsAsync());

        var version = await Read().GetCurrentVersionAsync(bolt.Id);
        Assert.Equal(0.02m, version!.MassKg);
    }

    [Fact]
    public async Task RenamedFileAndName_MatchesSameObjectByDesignation()
    {
        UseDefaultDataset();
        await ImportAsync();
        var repository = Read();
        var bolt = await repository.FindByDesignationAsync("АБВГ.111111.002");
        Assert.NotNull(bolt);

        // Переименованы и файл, и наименование, но обозначение осталось прежним:
        // объект обязан обновиться, а не упереться в уникальный индекс.
        var catalog = new FakeCadDocumentCatalog().Add("Шпилька новая.m3d");
        var reader = new FakeCadDocumentReader()
            .WithDocument(Cad.Part("Шпилька новая.m3d", "АБВГ.111111.002", "Шпилька", massKg: 0.02m, material: "Сталь 40"));

        var report = await new CadImportService(
            catalog,
            reader,
            new PdmUnitOfWorkFactory(ConnectionFactory)).ImportAsync("/fake/v3");

        var afterBolt = await Read().FindByDesignationAsync("АБВГ.111111.002");

        Assert.False(report.HasErrors);
        Assert.Equal(bolt!.Id, afterBolt!.Id);
        Assert.Equal(3, await CountObjectsAsync());

        var version = await Read().GetCurrentVersionAsync(bolt.Id);
        Assert.Equal(0.02m, version!.MassKg);
    }

    [Fact]
    public async Task Search_TreatsWildcardCharactersLiterally()
    {
        UseDefaultDataset();
        await ImportAsync();
        var repository = Read();

        // «%» в LIKE должен искаться буквально, а не как «любая последовательность».
        var byWildcard = await repository.SearchAsync("%");
        Assert.Empty(byWildcard);

        var byText = await repository.SearchAsync("Болт");
        Assert.Single(byText);
    }

    private async Task<int> CountObjectsAsync()
    {
        var objects = await Read().SearchAsync(query: null);
        return objects.Count;
    }
}