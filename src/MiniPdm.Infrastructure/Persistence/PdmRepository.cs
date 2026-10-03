using MiniPdm.Application.Persistence;
using MiniPdm.Domain;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Реализация хранилища на Dapper.
/// </summary>
/// <remarks>
/// Идентификаторы берутся через <c>INSERT ... RETURNING id</c>, который поддерживают
/// и PostgreSQL, и SQLite. Это позволяет не хранить диалектные различия в коде.
/// Время передаётся как <see cref="DateTime"/> в UTC, потому что драйвер SQLite
/// не принимает параметры типа <see cref="DateTimeOffset"/>.
/// </remarks>
internal sealed class PdmRepository : IPdmRepository
{
    private const string ObjectColumns =
        "id AS Id, object_type AS ObjectType, designation AS Designation, " +
        "name AS Name, source_file_name AS SourceFileName, current_version_id AS CurrentVersionId";

    private const string VersionColumns =
        "id AS Id, object_id AS ObjectId, version_no AS VersionNo, state AS State, " +
        "material AS Material, mass_kg AS MassKg, created_at AS CreatedAt";

    private readonly DbSessionProvider _sessions;

    public PdmRepository(DbSessionProvider sessions) => _sessions = sessions;

    public async Task<PdmObjectRow?> GetObjectAsync(long objectId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE id = @ObjectId",
            new { ObjectId = objectId },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<PdmObjectRow?> FindByDesignationAsync(string designation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE designation = @Designation",
            new { Designation = designation },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<PdmObjectRow?> FindStandardPartByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE object_type = @StandardPart AND name = @Name",
            new { StandardPart = (int)PdmObjectType.StandardPart, Name = name },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<PdmObjectRow?> FindBySourceFileNameAsync(string sourceFileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);

        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE source_file_name = @SourceFileName",
            new { SourceFileName = sourceFileName },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<PdmObjectRow?> FindByNameAsync(
        PdmObjectType type,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE object_type = @Type AND name = @Name",
            new { Type = (int)type, Name = name },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetSourceFileNamesAsync(
        IReadOnlyCollection<long> objectIds,
        CancellationToken cancellationToken = default)
    {
        if (objectIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        await using var session = await _sessions.OpenAsync(cancellationToken);

        // Идентификаторы формируются кодом, а не пользователем, поэтому
        // безопасная подстановка в список IN допустима.
        var list = string.Join(", ", objectIds);
        var rows = await session.QueryAsync<SourceFileNameDbRow>(
            $"SELECT id AS Id, source_file_name AS SourceFileName FROM pdm_object WHERE id IN ({list})",
            parameters: null,
            cancellationToken);

        return rows
            .Where(row => !string.IsNullOrEmpty(row.SourceFileName))
            .ToDictionary(row => row.Id, row => row.SourceFileName!);
    }

    public async Task<IReadOnlyList<PdmObjectRow>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            var all = await session.QueryAsync<ObjectDbRow>(
                $"SELECT {ObjectColumns} FROM pdm_object ORDER BY (designation IS NULL), designation, name",
                parameters: null,
                cancellationToken);
            return all.Select(row => row.ToRow()).ToArray();
        }

        // ILIKE поддерживается только PostgreSQL, а LIKE в обоих диалектах для
        // ascii-совместимых строк даёт тот же результат. Шаблон экранируется,
        // чтобы «%» в запросе пользователя не превращался в маску.
        // Символ «%» в запросе пользователя должен искаться буквально, поэтому
        // шаблон строится с экранированием, а LIKE получает явный ESCAPE.
        var pattern = $"%{EscapeLike(query.Trim())}%";
        var found = await session.QueryAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object " +
            "WHERE designation LIKE @Pattern ESCAPE '\\' OR name LIKE @Pattern ESCAPE '\\' " +
            "ORDER BY (designation IS NULL), designation, name",
            new { Pattern = pattern },
            cancellationToken);

        return found.Select(row => row.ToRow()).ToArray();
    }

    public async Task<IReadOnlyList<PdmObjectRow>> GetByTypeAsync(PdmObjectType type, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var rows = await session.QueryAsync<ObjectDbRow>(
            $"SELECT {ObjectColumns} FROM pdm_object WHERE object_type = @Type ORDER BY (designation IS NULL), designation, name",
            new { Type = (int)type },
            cancellationToken);

        return rows.Select(row => row.ToRow()).ToArray();
    }

    public async Task<long> InsertObjectAsync(
        PdmObjectType type,
        string? designation,
        string name,
        string? sourceFileName,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        return await session.ExecuteScalarAsync<long>(
            "INSERT INTO pdm_object (object_type, designation, name, source_file_name) " +
            "VALUES (@Type, @Designation, @Name, @SourceFileName) RETURNING id",
            new { Type = (int)type, Designation = designation, Name = name, SourceFileName = sourceFileName },
            cancellationToken);
    }

    public async Task<long> InsertVersionAsync(
        long objectId,
        int versionNo,
        string? material,
        decimal? massKg,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        return await session.ExecuteScalarAsync<long>(
            "INSERT INTO object_version (object_id, version_no, state, material, mass_kg, created_at) " +
            "VALUES (@ObjectId, @VersionNo, @State, @Material, @MassKg, @CreatedAt) RETURNING id",
            new
            {
                ObjectId = objectId,
                VersionNo = versionNo,
                State = (int)VersionState.InWork,
                Material = material,
                MassKg = massKg,
                CreatedAt = DateTime.UtcNow,
            },
            cancellationToken);
    }

    public async Task<int> GetNextVersionNoAsync(long objectId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        return await session.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(version_no), 0) + 1 FROM object_version WHERE object_id = @ObjectId",
            new { ObjectId = objectId },
            cancellationToken);
    }

    public async Task<ObjectVersionRow?> GetCurrentVersionAsync(long objectId, CancellationToken cancellationToken = default)
    {
        // Текущей считается именно назначенная версия: «последняя по номеру» может
        // оказаться аннулированной.
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<VersionDbRow>(
            "SELECT v.id AS Id, v.object_id AS ObjectId, v.version_no AS VersionNo, v.state AS State, "
            + "v.material AS Material, v.mass_kg AS MassKg, v.created_at AS CreatedAt "
            + "FROM object_version v JOIN pdm_object o ON o.current_version_id = v.id WHERE o.id = @ObjectId",
            new { ObjectId = objectId },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<ObjectVersionRow?> GetVersionAsync(long versionId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var row = await session.QuerySingleOrDefaultAsync<VersionDbRow>(
            $"SELECT {VersionColumns} FROM object_version WHERE id = @VersionId",
            new { VersionId = versionId },
            cancellationToken);

        return row?.ToRow();
    }

    public async Task<IReadOnlyList<ObjectVersionRow>> GetVersionsAsync(long objectId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var rows = await session.QueryAsync<VersionDbRow>(
            $"SELECT {VersionColumns} FROM object_version WHERE object_id = @ObjectId ORDER BY version_no DESC",
            new { ObjectId = objectId },
            cancellationToken);

        return rows.Select(row => row.ToRow()).ToArray();
    }

    public async Task UpdateVersionAttributesAsync(
        long versionId,
        string? material,
        decimal? massKg,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        await session.ExecuteAsync(
            "UPDATE object_version SET material = @Material, mass_kg = @MassKg WHERE id = @VersionId",
            new { VersionId = versionId, Material = material, MassKg = massKg },
            cancellationToken);
    }

    public async Task SetVersionStateAsync(long versionId, VersionState state, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        await session.ExecuteAsync(
            "UPDATE object_version SET state = @State WHERE id = @VersionId",
            new { VersionId = versionId, State = (int)state },
            cancellationToken);
    }

    public async Task SetCurrentVersionAsync(long objectId, long versionId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        await session.ExecuteAsync(
            "UPDATE pdm_object SET current_version_id = @VersionId WHERE id = @ObjectId",
            new { ObjectId = objectId, VersionId = versionId },
            cancellationToken);
    }

    public async Task<long?> GetCurrentVersionIdAsync(long objectId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        return await session.ExecuteNullableScalarAsync<long>(
            "SELECT current_version_id FROM pdm_object WHERE id = @ObjectId",
            new { ObjectId = objectId },
            cancellationToken);
    }

    /// <summary>
    /// Оставляет объект без текущей версии — так он выпадает из состава изделия.
    /// </summary>
    public async Task ClearCurrentVersionAsync(long objectId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        await session.ExecuteAsync(
            "UPDATE pdm_object SET current_version_id = NULL WHERE id = @ObjectId",
            new { ObjectId = objectId },
            cancellationToken);
    }

    public async Task ReplaceLinksAsync(
        long parentVersionId,
        IReadOnlyCollection<BomLinkRow> links,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);

        await session.ExecuteAsync(
            "DELETE FROM bom_link WHERE parent_version_id = @ParentVersionId",
            new { ParentVersionId = parentVersionId },
            cancellationToken);

        foreach (var link in links)
        {
            await session.ExecuteAsync(
                "INSERT INTO bom_link (parent_version_id, child_object_id, quantity) " +
                "VALUES (@ParentVersionId, @ChildObjectId, @Quantity)",
                new
                {
                    ParentVersionId = parentVersionId,
                    ChildObjectId = link.ChildObjectId,
                    Quantity = link.Quantity,
                },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<BomLinkRow>> GetLinksAsync(long parentVersionId, CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        var rows = await session.QueryAsync<LinkDbRow>(
            "SELECT parent_version_id AS ParentVersionId, child_object_id AS ChildObjectId, quantity AS Quantity " +
            "FROM bom_link WHERE parent_version_id = @ParentVersionId ORDER BY child_object_id",
            new { ParentVersionId = parentVersionId },
            cancellationToken);

        return rows.Select(row => new BomLinkRow(row.ParentVersionId, row.ChildObjectId, row.Quantity)).ToArray();
    }

    public async Task<IReadOnlyList<BomTreeNode>> GetTreeAsync(
        long rootObjectId,
        int maxDepth,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);

        // Один рекурсивный запрос вместо обхода объектов по уровням.
        // Ограничение глубины защищает от бесконечного разворачивания при
        // зацикливании; сами циклы обнаруживаются отдельной проверкой.
        const string sql = """
            WITH RECURSIVE tree AS (
                SELECT
                    o.id                    AS ObjectId,
                    v.id                    AS VersionId,
                    CAST(NULL AS BIGINT)    AS ParentObjectId,
                    o.object_type           AS ObjectType,
                    o.designation           AS Designation,
                    o.name                  AS Name,
                    v.state                 AS State,
                    v.version_no            AS VersionNo,
                    v.material              AS Material,
                    v.mass_kg               AS MassKg,
                    0                       AS Depth,
                    1                       AS Quantity
                FROM pdm_object o
                JOIN object_version v ON v.id = o.current_version_id
                WHERE o.id = @RootObjectId

                UNION ALL

                SELECT
                    child.id,
                    child_version.id,
                    tree.ObjectId,
                    child.object_type,
                    child.designation,
                    child.name,
                    child_version.state,
                    child_version.version_no,
                    child_version.material,
                    child_version.mass_kg,
                    tree.Depth + 1,
                    tree.Quantity * link.quantity
                FROM tree
                JOIN bom_link link ON link.parent_version_id = tree.VersionId
                JOIN pdm_object child ON child.id = link.child_object_id
                JOIN object_version child_version ON child_version.id = child.current_version_id
                WHERE tree.Depth < @MaxDepth
            )
            SELECT ObjectId, VersionId, ParentObjectId, ObjectType, Designation, Name,
                   State, VersionNo, Material, MassKg, Depth, Quantity
            FROM tree
            ORDER BY Depth, ObjectId
            """;

        await using var session = await _sessions.OpenAsync(cancellationToken);
        var rows = await session.QueryAsync<TreeDbRow>(
            sql,
            new { RootObjectId = rootObjectId, MaxDepth = maxDepth },
            cancellationToken);

        return rows.Select(row => new BomTreeNode(
                row.ObjectId,
                row.VersionId,
                row.ParentObjectId,
                (PdmObjectType)row.ObjectType,
                row.Designation,
                row.Name,
                (VersionState)row.State,
                row.VersionNo,
                row.Material,
                row.MassKg,
                row.Quantity,
                row.Depth))
            .ToArray();
    }

    public async Task AddImportLogEntryAsync(
        DateTimeOffset startedAt,
        string fileName,
        ImportLogSeverity severity,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sessions.OpenAsync(cancellationToken);
        await session.ExecuteAsync(
            "INSERT INTO import_log (started_at, file_name, severity, reason) " +
            "VALUES (@StartedAt, @FileName, @Severity, @Reason)",
            new
            {
                StartedAt = startedAt.UtcDateTime,
                FileName = fileName,
                Severity = (int)severity,
                Reason = reason,
            },
            cancellationToken);
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed class ObjectDbRow
    {
        public long Id { get; init; }

        public int ObjectType { get; init; }

        public string? Designation { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? SourceFileName { get; init; }

        public long? CurrentVersionId { get; init; }

        public PdmObjectRow ToRow() => new(Id, (PdmObjectType)ObjectType, Designation, Name, SourceFileName, CurrentVersionId);
    }

    private sealed class VersionDbRow
    {
        public long Id { get; init; }

        public long ObjectId { get; init; }

        public int VersionNo { get; init; }

        public int State { get; init; }

        public string? Material { get; init; }

        public decimal? MassKg { get; init; }

        public DateTime CreatedAt { get; init; }

        public ObjectVersionRow ToRow() => new(
            Id,
            ObjectId,
            VersionNo,
            (VersionState)State,
            Material,
            MassKg,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)));
    }

    private sealed class SourceFileNameDbRow
    {
        public long Id { get; init; }

        public string? SourceFileName { get; init; }
    }

    private sealed class LinkDbRow
    {
        public long ParentVersionId { get; init; }

        public long ChildObjectId { get; init; }

        public int Quantity { get; init; }
    }

    private sealed class TreeDbRow
    {
        public long ObjectId { get; init; }

        public long VersionId { get; init; }

        public long? ParentObjectId { get; init; }

        public int ObjectType { get; init; }

        public string? Designation { get; init; }

        public string Name { get; init; } = string.Empty;

        public int State { get; init; }

        public int VersionNo { get; init; }

        public string? Material { get; init; }

        public decimal? MassKg { get; init; }

        public int Depth { get; init; }

        public int Quantity { get; init; }
    }
}