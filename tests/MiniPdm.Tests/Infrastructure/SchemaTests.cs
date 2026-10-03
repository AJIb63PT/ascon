using Dapper;
using Microsoft.Data.Sqlite;
using MiniPdm.Infrastructure.Persistence;

namespace MiniPdm.Tests.Infrastructure;

public sealed class SchemaTests : SqliteTestBase
{
    [Fact]
    public async Task SchemaScript_AppliedTwice_DoesNotFail()
    {
        await new SchemaInitializer(ConnectionFactory).InitializeAsync();

        await using var connection = await ConnectionFactory.OpenConnectionAsync();
        var tables = (await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name"))
            .ToArray();

        Assert.Equal(
            new[] { "bom_link", "import_log", "object_version", "pdm_object" },
            tables);
    }

    [Fact]
    public async Task Designation_IsUniqueAmongAssembliesAndParts()
    {
        await using var connection = await ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (1, 'РДЦЛ.304112.302', 'Колесо зубчатое')");

        // Уникальность обозначения обеспечивается частичным индексом, поэтому
        // повторная вставка отвергается базой, а не игнорируется.
        await Assert.ThrowsAsync<SqliteException>(
            () => connection.ExecuteAsync(
                "INSERT INTO pdm_object (object_type, designation, name) VALUES (1, 'РДЦЛ.304112.302', 'Дубль')"));

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pdm_object WHERE designation = 'РДЦЛ.304112.302'");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StandardPart_MayRepeatDesignation_ButNotName()
    {
        await using var connection = await ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (2, NULL, 'Болт М12x40')");
        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (2, NULL, 'Болт М8x25')");

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pdm_object WHERE object_type = 2");
        Assert.Equal(2, count);

        await Assert.ThrowsAsync<SqliteException>(
            () => connection.ExecuteAsync(
                "INSERT INTO pdm_object (object_type, designation, name) VALUES (2, NULL, 'Болт М12x40')"));
    }

    [Fact]
    public async Task BomLinkQuantity_MustBePositive()
    {
        await using var connection = await ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (1, 'АБВГ.111111.001', 'Деталь')");
        var versionId = await connection.QuerySingleAsync<long>(
            "INSERT INTO object_version (object_id, version_no, state, mass_kg, created_at) " +
            "VALUES ((SELECT id FROM pdm_object LIMIT 1), 1, 0, 1.0, '2026-01-01') RETURNING id");

        await Assert.ThrowsAsync<SqliteException>(
            () => connection.ExecuteAsync(
                "INSERT INTO bom_link (parent_version_id, child_object_id, quantity) " +
                $"VALUES ({versionId}, (SELECT id FROM pdm_object LIMIT 1), 0)"));
    }

    [Fact]
    public async Task BomLink_RejectsDuplicateChildWithinSameParentVersion()
    {
        await using var connection = await ConnectionFactory.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (1, 'АБВГ.111111.001', 'Деталь')");
        await connection.ExecuteAsync(
            "INSERT INTO pdm_object (object_type, designation, name) VALUES (1, 'АБВГ.111111.002', 'Деталь2')");
        var versionId = await connection.QuerySingleAsync<long>(
            "INSERT INTO object_version (object_id, version_no, state, created_at) " +
            "VALUES ((SELECT id FROM pdm_object ORDER BY id LIMIT 1), 1, 0, '2026-01-01') RETURNING id");
        var childId = await connection.QuerySingleAsync<long>(
            "SELECT id FROM pdm_object ORDER BY id LIMIT 1 OFFSET 1");

        await connection.ExecuteAsync(
            "INSERT INTO bom_link (parent_version_id, child_object_id, quantity) " +
            $"VALUES ({versionId}, {childId}, 1)");

        await Assert.ThrowsAsync<SqliteException>(
            () => connection.ExecuteAsync(
                "INSERT INTO bom_link (parent_version_id, child_object_id, quantity) " +
                $"VALUES ({versionId}, {childId}, 5)"));
    }
}