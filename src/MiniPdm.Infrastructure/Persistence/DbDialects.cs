namespace MiniPdm.Infrastructure.Persistence;

/// <summary>Диалект PostgreSQL.</summary>
public sealed class PostgresDialect : EmbeddedSchemaDialect
{
    /// <inheritdoc />
    public override DbProvider Provider => DbProvider.PostgreSql;

    /// <inheritdoc />
    public override string SchemaResourceName => "schema.postgres.sql";
}

/// <summary>Диалект SQLite.</summary>
public sealed class SqliteDialect : EmbeddedSchemaDialect
{
    /// <inheritdoc />
    public override DbProvider Provider => DbProvider.Sqlite;

    /// <inheritdoc />
    public override string SchemaResourceName => "schema.sqlite.sql";
}

/// <summary>Фабрика диалектов по имени провайдера из конфигурации.</summary>
public static class DbDialects
{
    /// <summary>Создаёт диалект по значению из конфигурации.</summary>
    /// <param name="provider">Имя провайдера, регистр не важен.</param>
    /// <exception cref="InvalidOperationException">Провайдер не поддерживается.</exception>
    public static IDbDialect Create(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        null or "" or "postgresql" or "postgres" or "npgsql" => new PostgresDialect(),
        "sqlite" => new SqliteDialect(),
        _ => throw new InvalidOperationException(
            $"Неизвестный провайдер СУБД «{provider}». Поддерживаются: PostgreSql, Sqlite"),
    };
}