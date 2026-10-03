using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Application.Persistence;
using MiniPdm.Infrastructure.Persistence;
using MiniPdm.Tests.Fakes;
using MiniPdm.Tests.Infrastructure;

namespace MiniPdm.Tests;

/// <summary>
/// Базовый класс тестов импорта: SQLite в памяти плюс подменяемые ридер и каталог.
/// Файловая система в тестах не используется.
/// </summary>
public abstract class ImportTestBase : SqliteTestBase
{
    protected FakeCadDocumentCatalog Catalog { get; } = new();

    protected FakeCadDocumentReader Reader { get; } = new();

    protected ICadImportService CreateService()
    {
        var unitOfWorkFactory = new PdmUnitOfWorkFactory(ConnectionFactory);
        return new CadImportService(Catalog, Reader, unitOfWorkFactory);
    }

    protected Task<ImportReport> ImportAsync(CancellationToken cancellationToken = default) =>
        CreateService().ImportAsync("/fake/export", progress: null, cancellationToken);

    protected IPdmRepository Read() => new PdmUnitOfWorkFactory(ConnectionFactory).CreateReadRepository();
}