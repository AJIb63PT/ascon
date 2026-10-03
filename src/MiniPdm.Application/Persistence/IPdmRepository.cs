using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>
/// Хранилище объектов, версий и состава.
/// </summary>
/// <remarks>
/// Интерфейс не содержит SQL: реализация лежит в слое инфраструктуры и может быть
/// заменена. Экземпляр либо работает на собственном подключении, либо привязан
/// к транзакции <see cref="IPdmUnitOfWork"/>.
/// </remarks>
public interface IPdmRepository
{
    /// <summary>Возвращает объект по идентификатору.</summary>
    Task<PdmObjectRow?> GetObjectAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Ищет объект по обозначению среди сборок и деталей.</summary>
    Task<PdmObjectRow?> FindByDesignationAsync(string designation, CancellationToken cancellationToken = default);

    /// <summary>Ищет стандартное изделие по наименованию.</summary>
    Task<PdmObjectRow?> FindStandardPartByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет объекты по обозначению или наименованию. Пустая строка возвращает все объекты.
    /// </summary>
    Task<IReadOnlyList<PdmObjectRow>> SearchAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все объекты заданного типа.</summary>
    Task<IReadOnlyList<PdmObjectRow>> GetByTypeAsync(PdmObjectType type, CancellationToken cancellationToken = default);

    /// <summary>Ищет объект по имени файла в выгрузке CAD-системы.</summary>
    Task<PdmObjectRow?> FindBySourceFileNameAsync(string sourceFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет объект по наименованию среди объектов того же типа.
    /// </summary>
    /// <remarks>
    /// Нужен при повторном импорте: объект может прийти из другого файла
    /// выгрузки, и тогда совпадение ищется не по имени источника.
    /// </remarks>
    Task<PdmObjectRow?> FindByNameAsync(
        PdmObjectType type,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает имена файлов выгрузки для указанных объектов.</summary>
    Task<IReadOnlyDictionary<long, string>> GetSourceFileNamesAsync(
        IReadOnlyCollection<long> objectIds,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт объект и возвращает его идентификатор.</summary>
    Task<long> InsertObjectAsync(
        PdmObjectType type,
        string? designation,
        string name,
        string? sourceFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт версию объекта и возвращает её идентификатор.</summary>
    Task<long> InsertVersionAsync(
        long objectId,
        int versionNo,
        string? material,
        decimal? massKg,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает номер следующей версии объекта.</summary>
    Task<int> GetNextVersionNoAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает текущую версию объекта.</summary>
    Task<ObjectVersionRow?> GetCurrentVersionAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает версию по идентификатору.</summary>
    Task<ObjectVersionRow?> GetVersionAsync(long versionId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все версии объекта, от новых к старым.</summary>
    Task<IReadOnlyList<ObjectVersionRow>> GetVersionsAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Обновляет атрибуты версии, находящейся в работе.</summary>
    Task UpdateVersionAttributesAsync(
        long versionId,
        string? material,
        decimal? massKg,
        CancellationToken cancellationToken = default);

    /// <summary>Меняет состояние версии.</summary>
    Task SetVersionStateAsync(long versionId, VersionState state, CancellationToken cancellationToken = default);

    /// <summary>Назначает объекту текущую версию.</summary>
    Task SetCurrentVersionAsync(long objectId, long versionId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает идентификатор текущей версии объекта или <c>null</c>.</summary>
    Task<long?> GetCurrentVersionIdAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Снимает назначение текущей версии: объект выпадает из состава изделий.
    /// </summary>
    Task ClearCurrentVersionAsync(long objectId, CancellationToken cancellationToken = default);

    /// <summary>Полностью заменяет состав версии. Существующие связи удаляются.</summary>
    Task ReplaceLinksAsync(
        long parentVersionId,
        IReadOnlyCollection<BomLinkRow> links,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает состав версии.</summary>
    Task<IReadOnlyList<BomLinkRow>> GetLinksAsync(long parentVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Раскрывает состав объекла одним рекурсивным запросом.
    /// </summary>
    /// <param name="rootObjectId">Объект, с которого начинается раскрытие.</param>
    /// <param name="maxDepth">
    /// Ограничение глубины. Нужно как страховка от зацикливания на некорректных данных:
    /// обнаружение циклов — отдельная операция.
    /// </param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<BomTreeNode>> GetTreeAsync(
        long rootObjectId,
        int maxDepth,
        CancellationToken cancellationToken = default);

    /// <summary>Записывает строку журнала импорта.</summary>
    Task AddImportLogEntryAsync(
        DateTimeOffset startedAt,
        string fileName,
        ImportLogSeverity severity,
        string? reason,
        CancellationToken cancellationToken = default);
}