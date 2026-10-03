using MiniPdm.Application.Cad;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Application.Import;

/// <summary>
/// Импорт выгрузки CAD-системы.
/// </summary>
/// <remarks>
/// Порядок работы: чтение всех файлов, проверка каждого документа, поиск
/// дубликатов, каскадное отклонение сборок с отклонёнными компонентами и,
/// наконец, запись в одной транзакции. Повторный импорт одного и того же файла
/// не создаёт дублей: объект опознаётся по имени файла-источника.
/// </remarks>
public sealed class CadImportService : ICadImportService
{
    private readonly ICadDocumentCatalog _catalog;
    private readonly ICadDocumentReader _reader;
    private readonly IPdmUnitOfWorkFactory _unitOfWorkFactory;

    public CadImportService(
        ICadDocumentCatalog catalog,
        ICadDocumentReader reader,
        IPdmUnitOfWorkFactory unitOfWorkFactory)
    {
        _catalog = catalog;
        _reader = reader;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    /// <inheritdoc />
    public async Task<ImportReport> ImportAsync(
        string folderPath,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var documents = await ReadAllAsync(folderPath, progress, cancellationToken).ConfigureAwait(false);
        var rejected = Validate(documents);
        RejectAssembliesWithRejectedComponents(documents, rejected);

        var outcomes = await WriteAsync(documents, rejected, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        return new ImportReport(folderPath, startedAt, stopwatch.Elapsed, outcomes);
    }

    private async Task<List<ParsedDocument>> ReadAllAsync(
        string folderPath,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var refs = await _catalog.ListAsync(folderPath, cancellationToken).ConfigureAwait(false);
        var documents = new List<ParsedDocument>(refs.Count);
        var processed = 0;

        foreach (var documentRef in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            var parsed = new ParsedDocument(documentRef.FileName);

            try
            {
                parsed.Document = await _reader.ReadAsync(documentRef.Path, cancellationToken).ConfigureAwait(false);
            }
            catch (CadDocumentReadException ex)
            {
                parsed.Error = ex.Message;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                parsed.Error = $"Непредвиденная ошибка чтения: {ex.Message}";
            }

            documents.Add(parsed);
            progress?.Report(new ImportProgress(processed, refs.Count, documentRef.FileName));
        }

        return documents;
    }

    /// <summary>Проверяет документы и заполняет карту отклонений.</summary>
    private static Dictionary<string, string> Validate(List<ParsedDocument> documents)
    {
        var rejected = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var parsed in documents)
        {
            if (parsed.Error is not null)
            {
                rejected[parsed.FileName] = parsed.Error;
                continue;
            }

            var document = parsed.Document!;
            parsed.ObjectType = MapType(document.Type);

            var problem = ValidateSingle(document);
            if (problem is not null)
            {
                rejected[parsed.FileName] = problem;
                continue;
            }

            if (document.Type != CadDocumentType.Assembly && document.MassKg is null)
            {
                parsed.Warning = "Не заполнена масса изделия";
            }
        }

        RejectDuplicates(documents, rejected);
        return rejected;
    }

    private static string? ValidateSingle(CadDocument document)
    {
        var isStandardPart = document.Type == CadDocumentType.StandardPart;

        if (isStandardPart)
        {
            if (!string.IsNullOrWhiteSpace(document.Designation))
            {
                return $"У стандартного изделия не должно быть обозначения, а указано «{document.Designation}»";
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(document.Designation))
            {
                return "Не заполнено обозначение";
            }

            if (!DesignationRules.TryValidate(document.Designation, out var reason))
            {
                return reason;
            }
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            return "Не заполнено наименование";
        }

        if (document.Type != CadDocumentType.Assembly)
        {
            return null;
        }

        if (document.Components.Count == 0)
        {
            return "У сборки не задан ни один компонент";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in document.Components)
        {
            if (component.Quantity <= 0)
            {
                return $"Количество компонента «{component.FileName}» должно быть больше нуля";
            }

            if (!seen.Add(component.FileName))
            {
                return $"Компонент «{component.FileName}» указан в составе более одного раза";
            }
        }

        return null;
    }

    /// <summary>Отклоняет документы с конфликтующими обозначениями и наименованиями.</summary>
    private static void RejectDuplicates(List<ParsedDocument> documents, Dictionary<string, string> rejected)
    {
        var byDesignation = new Dictionary<string, List<ParsedDocument>>(StringComparer.Ordinal);
        var byStandardName = new Dictionary<string, List<ParsedDocument>>(StringComparer.Ordinal);

        foreach (var parsed in documents)
        {
            if (parsed.Error is not null || rejected.ContainsKey(parsed.FileName))
            {
                continue;
            }

            var document = parsed.Document!;

            if (document.Type != CadDocumentType.StandardPart
                && !string.IsNullOrWhiteSpace(document.Designation))
            {
                Add(byDesignation, document.Designation, parsed);
            }

            if (document.Type == CadDocumentType.StandardPart)
            {
                Add(byStandardName, document.Name, parsed);
            }
        }

        foreach (var (designation, group) in byDesignation)
        {
            if (group.Count < 2)
            {
                continue;
            }

            foreach (var parsed in group)
            {
                rejected[parsed.FileName] =
                    $"Обозначение «{designation}» встречается в файлах: {string.Join(", ", group.Select(p => p.FileName))}";
            }
        }

        foreach (var (name, group) in byStandardName)
        {
            if (group.Count < 2)
            {
                continue;
            }

            foreach (var parsed in group)
            {
                rejected[parsed.FileName] =
                    $"Наименование «{name}» встречается у нескольких стандартных изделий: {string.Join(", ", group.Select(p => p.FileName))}";
            }
        }
    }

    /// <summary>
    /// Отклоняет сборки, ссылающиеся на отклонённые или отсутствующие документы.
    /// Повторяется до стабилизации, так как отклонение может распространиться вверх по цепочке.
    /// </summary>
    private static void RejectAssembliesWithRejectedComponents(
        List<ParsedDocument> documents,
        Dictionary<string, string> rejected)
    {
        var byFileName = documents.ToDictionary(parsed => parsed.FileName, StringComparer.Ordinal);
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var parsed in documents)
            {
                if (rejected.ContainsKey(parsed.FileName) || parsed.Error is not null)
                {
                    continue;
                }

                var document = parsed.Document!;
                if (document.Type != CadDocumentType.Assembly)
                {
                    continue;
                }

                foreach (var component in document.Components)
                {
                    if (!byFileName.ContainsKey(component.FileName))
                    {
                        rejected[parsed.FileName] =
                            $"Файл компонента «{component.FileName}» не найден в папке выгрузки";
                        changed = true;
                        break;
                    }

                    if (rejected.TryGetValue(component.FileName, out var cause))
                    {
                        rejected[parsed.FileName] =
                            $"Компонент «{component.FileName}» отклонён: {cause}";
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Записывает принятые документы в одной транзакции.
    /// </summary>
    /// <remarks>
    /// Перед записью выполняется сверка с уже загруженными данными: документ,
    /// совпадающий с существующим объектом по наименованию, обновляет его, а не
    /// создаёт дубль и не обрывает всю транзакцию ошибкой уникального индекса.
    /// </remarks>
    private async Task<IReadOnlyList<ImportOutcome>> WriteAsync(
        List<ParsedDocument> documents,
        Dictionary<string, string> rejected,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<ImportOutcome>(documents.Count);
        var idByFileName = new Dictionary<string, long>(StringComparer.Ordinal);

        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var repository = unitOfWork.Repository;

        var accepted = documents
            .Where(parsed => !rejected.ContainsKey(parsed.FileName))
            .ToList();

        // Сначала создаются или опознаются сами объекты: без их идентификаторов
        // нельзя записать связи в составе сборок.
        foreach (var parsed in accepted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = parsed.Document!;

            var objectId = await EnsureObjectAsync(repository, parsed, document, cancellationToken).ConfigureAwait(false);
            idByFileName[parsed.FileName] = objectId;
        }

        foreach (var parsed in accepted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = parsed.Document!;
            var objectId = idByFileName[parsed.FileName];
            var outcome = await WriteVersionAsync(repository, parsed, document, objectId, idByFileName, cancellationToken)
                .ConfigureAwait(false);

            outcomes.Add(outcome);
        }

        foreach (var parsed in documents.Where(p => rejected.ContainsKey(p.FileName)))
        {
            outcomes.Add(new ImportOutcome(
                parsed.FileName,
                ImportSeverity.Error,
                rejected[parsed.FileName],
                parsed.Document?.Type,
                parsed.Document?.Designation,
                parsed.Document?.Name));
        }

        foreach (var outcome in outcomes)
        {
            await repository.AddImportLogEntryAsync(
                DateTimeOffset.UtcNow,
                outcome.FileName,
                outcome.Severity switch
                {
                    ImportSeverity.Error => ImportLogSeverity.Error,
                    ImportSeverity.Warning => ImportLogSeverity.Warning,
                    _ => ImportLogSeverity.Info,
                },
                outcome.Reason,
                cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return outcomes.OrderBy(outcome => outcome.FileName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Ищет объект в базе или создаёт новый.
    /// </summary>
/// <remarks>
/// Сопоставление идёт по имени файла-источника, затем по обозначению и лишь
/// после этого — по наименованию среди объектов того же типа. Обозначение стоит
/// перед наименованием, потому что оно уникально и меняется вместе с изделием,
/// а наименование конструктор переименовать может.
///
/// Стандартные изделия обозначения не имеют, для них остаётся сопоставление по
/// наименованию.
/// </remarks>
    private static async Task<long> EnsureObjectAsync(
        IPdmRepository repository,
        ParsedDocument parsed,
        CadDocument document,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindBySourceFileNameAsync(parsed.FileName, cancellationToken).ConfigureAwait(false);

        if (existing is null
            && !string.IsNullOrWhiteSpace(document.Designation)
            && DesignationRules.IsValid(document.Designation))
        {
            existing = await repository.FindByDesignationAsync(document.Designation, cancellationToken).ConfigureAwait(false);
        }

        if (existing is null)
        {
            existing = await repository
                .FindByNameAsync(parsed.ObjectType!.Value, document.Name, cancellationToken)
                .ConfigureAwait(false);
        }

        if (existing is not null)
        {
            parsed.ObjectId = existing.Id;
            return existing.Id;
        }

        var objectId = await repository.InsertObjectAsync(
            parsed.ObjectType!.Value,
            document.Designation,
            document.Name,
            parsed.FileName,
            cancellationToken).ConfigureAwait(false);

        parsed.ObjectId = objectId;
        return objectId;
    }

    private static async Task<ImportOutcome> WriteVersionAsync(
        IPdmRepository repository,
        ParsedDocument parsed,
        CadDocument document,
        long objectId,
        IReadOnlyDictionary<string, long> idByFileName,
        CancellationToken cancellationToken)
    {
        var links = document.Components
            .Select(component => new BomLinkRow(0, idByFileName[component.FileName], component.Quantity))
            .ToArray();

        var current = await repository.GetCurrentVersionAsync(objectId, cancellationToken).ConfigureAwait(false);

        // Первичная загрузка объекта.
        if (current is null)
        {
            var versionNo = await repository.GetNextVersionNoAsync(objectId, cancellationToken).ConfigureAwait(false);
            var versionId = await repository
                .InsertVersionAsync(objectId, versionNo, document.Material, document.MassKg, cancellationToken)
                .ConfigureAwait(false);

            await repository.SetCurrentVersionAsync(objectId, versionId, cancellationToken).ConfigureAwait(false);
            await ReplaceLinksAsync(repository, versionId, links, cancellationToken).ConfigureAwait(false);

            parsed.ChangedVersionNo = versionNo;
            return BuildOutcome(parsed, document, objectId, versionNo);
        }

        var attributesChanged = !string.Equals(current.Material, document.Material, StringComparison.Ordinal)
            || current.MassKg != document.MassKg;

        var linksChanged = await HaveLinksChangedAsync(repository, current.Id, links, idByFileName, cancellationToken)
            .ConfigureAwait(false);

        if (!attributesChanged && !linksChanged)
        {
            // Документ не изменился: новую версию не создаём.
            return BuildOutcome(parsed, document, objectId, current.VersionNo, versionChanged: false);
        }

        // Последняя версия ещё в работе — правим её.
        if (current.State == VersionState.InWork)
        {
            await repository
                .UpdateVersionAttributesAsync(current.Id, document.Material, document.MassKg, cancellationToken)
                .ConfigureAwait(false);
            await ReplaceLinksAsync(repository, current.Id, links, cancellationToken).ConfigureAwait(false);

            parsed.ChangedVersionNo = current.VersionNo;
            return BuildOutcome(parsed, document, objectId, current.VersionNo);
        }

        // Последняя версия утверждена или аннулирована: создаём следующую.
        var newVersionNo = await repository.GetNextVersionNoAsync(objectId, cancellationToken).ConfigureAwait(false);
        var newVersionId = await repository
            .InsertVersionAsync(objectId, newVersionNo, document.Material, document.MassKg, cancellationToken)
            .ConfigureAwait(false);

        await repository.SetCurrentVersionAsync(objectId, newVersionId, cancellationToken).ConfigureAwait(false);
        await ReplaceLinksAsync(repository, newVersionId, links, cancellationToken).ConfigureAwait(false);

        parsed.ChangedVersionNo = newVersionNo;
        return BuildOutcome(parsed, document, objectId, newVersionNo);
    }

    private static Task ReplaceLinksAsync(
        IPdmRepository repository,
        long versionId,
        IReadOnlyCollection<BomLinkRow> links,
        CancellationToken cancellationToken)
    {
        // ParentVersionId проставляется здесь: идентификатор версии известен только сейчас.
        return repository.ReplaceLinksAsync(
            versionId,
            links.Select(link => link with { ParentVersionId = versionId }).ToArray(),
            cancellationToken);
    }

    /// <summary>
    /// Сравнивает состав версии с входящим документом по именам файлов компонентов.
    /// </summary>
    private static async Task<bool> HaveLinksChangedAsync(
        IPdmRepository repository,
        long versionId,
        IReadOnlyCollection<BomLinkRow> incoming,
        IReadOnlyDictionary<string, long> idByFileName,
        CancellationToken cancellationToken)
    {
        if (incoming.Count == 0)
        {
            return (await repository.GetLinksAsync(versionId, cancellationToken).ConfigureAwait(false)).Count > 0;
        }

        var stored = await repository.GetLinksAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (stored.Count != incoming.Count)
        {
            return true;
        }

        var childIds = stored.Select(link => link.ChildObjectId).ToArray();
        var fileNamesById = await repository
            .GetSourceFileNamesAsync(childIds, cancellationToken)
            .ConfigureAwait(false);

        // Сопоставление идёт по именам файлов: идентификаторы в базе могли быть
        // выданы в другом порядке, а документ опознаётся по имени источника.
        var fileNameById = new Dictionary<long, string>(idByFileName.Count);
        foreach (var (fileName, id) in idByFileName)
        {
            fileNameById[id] = fileName;
        }

        var incomingByFileName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in incoming)
        {
            if (fileNameById.TryGetValue(candidate.ChildObjectId, out var mappedFileName))
            {
                incomingByFileName[mappedFileName] = candidate.Quantity;
            }
            else
            {
                return true;
            }
        }

        foreach (var link in stored)
        {
            if (!fileNamesById.TryGetValue(link.ChildObjectId, out var fileName))
            {
                // Состав ссылается на объект без известного источника — считаем изменённым.
                return true;
            }

            if (!incomingByFileName.TryGetValue(fileName, out var quantity) || quantity != link.Quantity)
            {
                return true;
            }
        }

        return false;
    }

    private static ImportOutcome BuildOutcome(
        ParsedDocument parsed,
        CadDocument document,
        long objectId,
        int versionNo,
        bool versionChanged = true)
    {
        var reason = parsed.Warning
            ?? (versionChanged ? $"Записана версия {versionNo} (в работе)" : "Документ не изменился");

        return new ImportOutcome(
            parsed.FileName,
            parsed.Warning is null ? ImportSeverity.Ok : ImportSeverity.Warning,
            reason,
            document.Type,
            document.Designation,
            document.Name,
            objectId,
            versionNo);
    }

    private static PdmObjectType MapType(CadDocumentType type) => type switch
    {
        CadDocumentType.Assembly => PdmObjectType.Assembly,
        CadDocumentType.Part => PdmObjectType.Part,
        CadDocumentType.StandardPart => PdmObjectType.StandardPart,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип документа."),
    };

    private static void Add<TKey>(
        Dictionary<TKey, List<ParsedDocument>> map,
        TKey key,
        ParsedDocument value)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<ParsedDocument>();
            map[key] = list;
        }

        list.Add(value);
    }

    /// <summary>Документ в процессе проверки.</summary>
    private sealed class ParsedDocument
    {
        public ParsedDocument(string fileName) => FileName = fileName;

        public string FileName { get; }

        public CadDocument? Document { get; set; }

        public string? Error { get; set; }

        public string? Warning { get; set; }

        public PdmObjectType? ObjectType { get; set; }

        public long? ObjectId { get; set; }

        public int? ChangedVersionNo { get; set; }
    }
}