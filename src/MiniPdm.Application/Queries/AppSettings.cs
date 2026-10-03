namespace MiniPdm.Application.Queries;

/// <summary>
/// Параметры подключения к базе, прочитанные из конфигурации.
/// </summary>
public sealed class DatabaseSettings
{
    /// <summary>Провайдер: <c>PostgreSql</c> или <c>Sqlite</c>.</summary>
    public string Provider { get; set; } = "PostgreSql";

    /// <summary>Строка подключения к базе.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Создавать ли таблицы при запуске.</summary>
    public bool ApplySchemaOnStartup { get; set; } = true;
}

/// <summary>Корневой раздел настроек приложения.</summary>
public sealed class AppSettings
{
    /// <summary>Параметры базы данных.</summary>
    public DatabaseSettings Database { get; set; } = new();
}
