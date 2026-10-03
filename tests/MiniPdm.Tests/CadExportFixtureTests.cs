using System.IO.Compression;
using MiniPdm.Application.Calculations;
using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Application.Lifecycle;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Cad;
using MiniPdm.Infrastructure.Persistence;
using MiniPdm.Tests.Infrastructure;

namespace MiniPdm.Tests;

/// <summary>
/// Сквозная проверка на настоящих данных из архива <c>cad-export.zip</c>.
/// </summary>
/// <remarks>
/// Эти тесты намеренно работают с файловой системой и реальным ридером: это
/// единственный способ убедиться, что формат данных разобран верно. Логика
/// импорта проверена отдельно на подменённых зависимостях.
/// </remarks>
public sealed class CadExportFixtureTests : SqliteTestBase, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "minipdm-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Временные файлы тестов не должны ронять прогон.
        }
    }

    private string Extract(string archiveFolder) =>
        Path.Combine(_root, ExtractFolder(archiveFolder, archiveFolder));

    /// <summary>Распаковывает папку из архива, лежащего в корне репозитория.</summary>
    private string ExtractFolder(string folder, string targetName)
    {
        var archivePath = RepositoryFile("cad-export.zip");
        var target = Path.Combine(_root, targetName);

        using var archive = ZipFile.OpenRead(archivePath);
        var prefix = folder + "/";

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) || entry.Length == 0)
            {
                continue;
            }

            var relative = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.Combine(target, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        if (!Directory.Exists(target))
        {
            throw new DirectoryNotFoundException($"Папка {folder} не найдена в архиве.");
        }

        return target;
    }

    /// <summary>Ищет файл в корне репозитория, поднимаясь от папки тестов.</summary>
    private static string RepositoryFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Файл {fileName} не найден в корне репозитория.");
    }

    private ICadImportService CreateImportService() =>
        new CadImportService(
            new FileSystemCadDocumentCatalog(),
            new JsonCadDocumentReader(),
            new PdmUnitOfWorkFactory(ConnectionFactory));

    /// <summary>
    /// Собирает прямые связи раскрытого состава в едином пространстве
    /// идентификаторов объектов.
    /// </summary>
    /// <remarks>
    /// В хранилище родитель связи — версия, а потомок — объект, поэтому перед
    /// поиском циклов версии родителей переводятся в объекты.
    /// </remarks>
    private static async Task<IReadOnlyList<BomLinkRow>> CollectLinksAsync(
        IPdmRepository repository,
        IReadOnlyList<BomTreeNode> tree)
    {
        var links = new List<BomLinkRow>();

        foreach (var node in tree)
        {
            links.AddRange(await repository.GetLinksAsync(node.VersionId));
        }

        return BomLinkGraph.ToObjectIds(tree, links);
    }

    [Fact]
    public async Task CadExport_ProducesExpectedImportReport()
    {
        var folder = Extract("cad-export");

        var report = await CreateImportService().ImportAsync(folder);

        Assert.Equal(45, report.TotalCount);
        Assert.Equal(38, report.AcceptedCount);
        Assert.Equal(7, report.RejectedCount);
        Assert.Equal(1, report.WarningCount);

        var errors = report.Outcomes.Where(o => o.Severity == ImportSeverity.Error).ToArray();

        // Каждая из намеренно испорченных причин обнаружена.
        Assert.Contains(errors, o => o.FileName == "Отдушина.m3d");
        Assert.Contains(errors, o => o.FileName == "Втулка распорная.m3d");
        Assert.Contains(errors, o => o.FileName == "Кольцо распорное.m3d");
        Assert.Contains(errors, o => o.FileName == "Шайба стопорная.m3d");
        Assert.Contains(errors, o => o.FileName == "Шайба упорная.m3d");
        Assert.Contains(errors, o => o.FileName == "Привод масляного насоса.a3d");
        Assert.Contains(errors, o => o.FileName == "Пробка сливная в сборе.a3d");
    }

    [Fact]
    public async Task CadExport_ReportsReadableReasonsForEveryRejection()
    {
        var folder = Extract("cad-export");

        var report = await CreateImportService().ImportAsync(folder);

        foreach (var rejected in report.Outcomes.Where(o => o.Severity == ImportSeverity.Error))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(rejected.Reason),
                $"Для файла {rejected.FileName} не указана причина отклонения.");
        }
    }

    [Fact]
    public async Task CadExport_WarnsAboutMissingMassOnlyOnce()
    {
        var folder = Extract("cad-export");

        var report = await CreateImportService().ImportAsync(folder);

        var warning = Assert.Single(report.Outcomes, o => o.Severity == ImportSeverity.Warning);
        Assert.Equal("Прокладка маслоуказателя.m3d", warning.FileName);
        Assert.Contains("масса", warning.Reason);
    }

    [Fact]
    public async Task CadExport_AcceptsEveryDocumentWithValidEskdDesignation()
    {
        var folder = Extract("cad-export");

        var report = await CreateImportService().ImportAsync(folder);

        var acceptedDesignations = report.Outcomes
            .Where(o => o.IsImported && o.Type != CadDocumentType.StandardPart)
            .Select(o => o.Designation)
            .ToArray();

        Assert.NotEmpty(acceptedDesignations);
        Assert.All(acceptedDesignations, designation => Assert.True(DesignationRules.IsValid(designation)));
    }

    [Fact]
    public async Task CadExport_BuildsBomTreeForTopAssembly()
    {
        var folder = Extract("cad-export");
        await CreateImportService().ImportAsync(folder);

        var repository = new PdmUnitOfWorkFactory(ConnectionFactory).CreateReadRepository();
        var calculator = new BomCalculator();
        var reducer = await repository.FindByDesignationAsync("РДЦЛ.304112.000");

        Assert.NotNull(reducer);

        var tree = await repository.GetTreeAsync(reducer!.Id, BomCalculator.MaxBomDepth);

        Assert.NotEmpty(tree);
        Assert.Equal(reducer.Id, tree[0].ObjectId);

        // Масса редуктора считается полностью: у всех его деталей она есть.
        var mass = calculator.CalculateMass(tree);
        Assert.True(mass.IsComplete);
        Assert.Empty(mass.NodesWithUnknownMass);
        Assert.True(mass.TotalMassKg > 0);

        // Крышка подшипника используется четыре раза: количество перемножается по пути.
        var cover = Assert.Single(tree, node => node.Name == "Крышка подшипника в сборе");
        Assert.Equal(4, cover.Quantity);

        // У сборки собственной массы нет — она складывается из компонентов.
        Assert.Null(cover.MassKg);

        // Регулировочная прокладка внутри неё: 4 крышки × 3 прокладки = 12.
        var gasket = Assert.Single(tree, node => node.Name == "Прокладка регулировочная");
        Assert.Equal(12, gasket.Quantity);
        Assert.Equal(gasket.MassKg!.Value * gasket.Quantity, gasket.TotalMassKg!.Value);

        // Спецификация собирается и содержит только принятые объекты.
        var specification = calculator.BuildSpecification(tree);
        Assert.NotEmpty(specification);
        Assert.DoesNotContain(specification, row => row.Name == "Шайба стопорная");
        Assert.All(specification, row => Assert.True(row.Quantity > 0));

        // Все узлы — принятые объекты, циклов нет.
        Assert.Empty(calculator.FindCycles(await CollectLinksAsync(repository, tree)));

        // Отдельная проверка неполной массы: маслоуказатель включает прокладку
        // без массы, поэтому полной суммы у него не получится.
        var oilIndicator = await repository.FindByDesignationAsync("РДЦЛ.304112.900");
        var oilTree = await repository.GetTreeAsync(oilIndicator!.Id, BomCalculator.MaxBomDepth);
        var oilMass = calculator.CalculateMass(oilTree);

        Assert.False(oilMass.IsComplete);
        Assert.Contains(oilMass.NodesWithUnknownMass, node => node.Name == "Прокладка маслоуказателя");
    }

    [Fact]
    public async Task CadExport_QuantityIsMultipliedAlongThePath()
    {
        var folder = Extract("cad-export");
        await CreateImportService().ImportAsync(folder);

        var repository = new PdmUnitOfWorkFactory(ConnectionFactory).CreateReadRepository();
        var reducer = await repository.FindByDesignationAsync("РДЦЛ.304112.000");
        var tree = await repository.GetTreeAsync(reducer!.Id, BomCalculator.MaxBomDepth);

        // Количество узла должно быть произведением количеств по всем уровням.
        foreach (var node in tree.Where(node => node.Depth > 0))
        {
            Assert.True(node.Quantity > 0, $"У узла {node.Name} некорректное количество {node.Quantity}.");
        }
    }

    [Fact]
    public async Task ReimportOfCadExport_CreatesNoNewVersions()
    {
        var folder = Extract("cad-export");
        var service = CreateImportService();

        await service.ImportAsync(folder);
        var repository = new PdmUnitOfWorkFactory(ConnectionFactory).CreateReadRepository();
        var reducer = await repository.FindByDesignationAsync("РДЦЛ.304112.000");

        var before = await repository.GetVersionsAsync(reducer!.Id);

        var secondReport = await service.ImportAsync(folder);
        var after = await repository.GetVersionsAsync(reducer.Id);

        Assert.Equal(38, secondReport.AcceptedCount);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task ReimportAfterApproval_WithCadExportV2_CreatesSecondVersion()
    {
        var first = Extract("cad-export");
        var second = Extract("cad-export-v2");
        var service = CreateImportService();
        var unitOfWorkFactory = new PdmUnitOfWorkFactory(ConnectionFactory);
        var lifecycle = new ObjectLifecycleService(unitOfWorkFactory);

        await service.ImportAsync(first);
        var repository = unitOfWorkFactory.CreateReadRepository();

        // Утверждаем деталь, которую конструктор изменил во второй выгрузке.
        var gear = await repository.FindByDesignationAsync("РДЦЛ.304112.302");
        Assert.NotNull(gear);
        await lifecycle.ChangeStateAsync(gear!.Id, VersionState.Approved);

        // И сборку, состав которой изменился.
        var bearingCover = await repository.FindByDesignationAsync("РДЦЛ.304112.500");
        Assert.NotNull(bearingCover);
        await lifecycle.ChangeStateAsync(bearingCover!.Id, VersionState.Approved);

        await service.ImportAsync(second);

        var gearVersions = await repository.GetVersionsAsync(gear.Id);
        Assert.Equal(2, gearVersions.Count);
        Assert.Equal(2, gearVersions[0].VersionNo);
        Assert.Equal(VersionState.InWork, gearVersions[0].State);
        Assert.Equal(VersionState.Approved, gearVersions[1].State);

        // Масса колеса изменилась с 5.86 на 5.92 кг.
        Assert.Equal(5.92m, gearVersions[0].MassKg);
        Assert.Equal(5.86m, gearVersions[1].MassKg);

        var coverVersions = await repository.GetVersionsAsync(bearingCover!.Id);
        Assert.Equal(2, coverVersions.Count);

        // Количество прокладки изменилось с 3 на 4 в новой версии состава.
        var links = await repository.GetLinksAsync(coverVersions[0].Id);
        var gasketObject = await repository.FindByDesignationAsync("РДЦЛ.304112.502");
        Assert.NotNull(gasketObject);
        var gasketLink = Assert.Single(links, link => link.ChildObjectId == gasketObject!.Id);
        Assert.Equal(4, gasketLink.Quantity);

        var oldLinks = await repository.GetLinksAsync(coverVersions[1].Id);
        var oldGasketLink = Assert.Single(oldLinks, link => link.ChildObjectId == gasketObject!.Id);
        Assert.Equal(3, oldGasketLink.Quantity);
    }

    [Fact]
    public async Task ReimportWithoutApproval_KeepsSingleInWorkVersion()
    {
        var first = Extract("cad-export");
        var second = Extract("cad-export-v2");
        var service = CreateImportService();

        await service.ImportAsync(first);
        var repository = new PdmUnitOfWorkFactory(ConnectionFactory).CreateReadRepository();
        var gear = await repository.FindByDesignationAsync("РДЦЛ.304112.302");

        // Версия остаётся в работе — импорт правит её, а не создаёт новую.
        await service.ImportAsync(second);

        var versions = await repository.GetVersionsAsync(gear!.Id);
        Assert.Single(versions);
        Assert.Equal(VersionState.InWork, versions[0].State);

        // Масса обновлена в той же версии: 5.86 → 5.92.
        Assert.Equal(5.92m, versions[0].MassKg);
    }

    [Fact]
    public async Task AnnullingCurrentVersion_PointsObjectToPreviousOne()
    {
        var folder = Extract("cad-export");
        var service = CreateImportService();
        await service.ImportAsync(folder);

        var unitOfWorkFactory = new PdmUnitOfWorkFactory(ConnectionFactory);
        var repository = unitOfWorkFactory.CreateReadRepository();
        var lifecycle = new ObjectLifecycleService(unitOfWorkFactory);

        var gear = await repository.FindByDesignationAsync("РДЦЛ.304112.302");
        await lifecycle.ChangeStateAsync(gear!.Id, VersionState.Approved);
        await lifecycle.ChangeStateAsync(gear.Id, VersionState.Annulled);

        var versions = await repository.GetVersionsAsync(gear.Id);
        Assert.Single(versions);
        Assert.Equal(VersionState.Annulled, versions[0].State);

        // Аннулированная версия выпадает из дерева состава.
        var reducer = await repository.FindByDesignationAsync("РДЦЛ.304112.000");
        var tree = await repository.GetTreeAsync(reducer!.Id, BomCalculator.MaxBomDepth);
        Assert.DoesNotContain(tree, node => node.ObjectId == gear.Id);
    }

    [Fact]
    public async Task ImportWritesJournalForEveryDocument()
    {
        var folder = Extract("cad-export");

        var report = await CreateImportService().ImportAsync(folder);

        Assert.Equal(report.TotalCount, report.Outcomes.Count);
        Assert.All(report.Outcomes, outcome => Assert.False(string.IsNullOrWhiteSpace(outcome.FileName)));
    }
}