using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Применяет скрипт схемы из репозитория. Скрипты идемпотентны, поэтому повторный
/// запуск при создании базы безопасен.
/// </summary>
public sealed class SchemaInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>Создаёт инициализатор схемы.</summary>
    /// <param name="connectionFactory">Фабрика подключений.</param>
    public SchemaInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Создаёт таблицы и индексы, если их ещё нет.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dialect = _connectionFactory.Dialect;
        var statements = EmbeddedSchemaDialect.SplitStatements(dialect.GetSchemaScript());

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        // SQLite по умолчанию не проверяет внешние ключи; без этого каскадные удаления
        // и ссылочная целостность не работали бы.
        if (dialect.Provider == DbProvider.Sqlite)
        {
            await ExecutePragmasAsync(connection, ct).ConfigureAwait(false);
        }

        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: ct))
                .ConfigureAwait(false);
        }
    }

    /// <summary>Удаляет все таблицы приложения. Используется в тестах.</summary>
    /// <param name="ct">Токен отмены.</param>
    public async Task DropAsync(CancellationToken ct = default)
    {
        var statements = new[]
        {
            "DROP TABLE IF EXISTS bom_link",
            "DROP TABLE IF EXISTS import_log",
            "DROP TABLE IF EXISTS object_version",
            "DROP TABLE IF EXISTS pdm_object",
        };

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: ct))
                .ConfigureAwait(false);
        }
    }

    private static async Task ExecutePragmasAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection is not SqliteConnection) return;

        await connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys = ON;", cancellationToken: ct))
            .ConfigureAwait(false);
    }
}