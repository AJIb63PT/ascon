using System.Reflection;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Общая часть диалектов: загрузка скрипта схемы из внедрённого ресурса и разбор на команды.
/// </summary>
public abstract class EmbeddedSchemaDialect : IDbDialect
{
    private const string ResourcePrefix = "MiniPdm.Infrastructure.db.";

    /// <inheritdoc />
    public abstract DbProvider Provider { get; }

    /// <inheritdoc />
    public abstract string SchemaResourceName { get; }

    private string? _script;

    /// <inheritdoc />
    public string GetSchemaScript() => _script ??= LoadScript();

    private string LoadScript()
    {
        var assembly = typeof(EmbeddedSchemaDialect).GetTypeInfo().Assembly;
        var resourceName = ResourcePrefix + SchemaResourceName;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Скрипт схемы «{resourceName}» не найден в сборке {assembly.GetName().Name}. " +
                "Доступные ресурсы: " + string.Join(", ", assembly.GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Разбивает скрипт на отдельные команды по точке с запятой в конце строки.
    /// </summary>
    /// <remarks>
    /// Текст в комментариях и строковых литералах не разбирается: автор скриптов
    /// контролирует содержимое, поэтому достаточно простого и предсказуемого правила.
    /// </remarks>
    /// <param name="script">Текст скрипта.</param>
    public static IReadOnlyList<string> SplitStatements(string script)
    {
        var statements = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var rawLine in script.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            current.Append(line).Append('\n');

            if (trimmed.EndsWith(';'))
            {
                statements.Add(current.ToString().Trim());
                current.Clear();
            }
        }

        var tail = current.ToString().Trim();
        if (tail.Length > 0)
        {
            statements.Add(tail);
        }

        return statements;
    }
}