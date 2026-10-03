using System.Globalization;
using System.Text.Json;
using MiniPdm.Application.Cad;

namespace MiniPdm.Infrastructure.Cad;

/// <summary>
/// Читает выгрузку CAD-системы в формате JSON.
/// </summary>
/// <remarks>
/// Единственный класс в приложении, знающий про JSON и файловую систему.
/// Любая ошибка формата превращается в <see cref="CadDocumentReadException"/>,
/// чтобы импорт мог продолжить обработку остальных документов.
/// </remarks>
public sealed class JsonCadDocumentReader : ICadDocumentReader
{
    /// <summary>Поддерживаемая версия формата выгрузки.</summary>
    public const int SupportedFormatVersion = 1;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <inheritdoc />
    public async Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CadDocumentReadException($"Не удалось прочитать файл: {ex.Message}", ex);
        }

        try
        {
            return Parse(text, Path.GetFileName(path));
        }
        catch (JsonException ex)
        {
            throw new CadDocumentReadException($"Некорректный JSON: {ex.Message}", ex);
        }
        catch (FormatException ex)
        {
            throw new CadDocumentReadException($"Некорректное значение поля: {ex.Message}", ex);
        }
    }

    /// <summary>Разбирает содержимое документа. Вынесено отдельно ради тестируемости.</summary>
    /// <param name="json">Содержимое файла.</param>
    /// <param name="expectedFileName">Имя файла на диске — идентификатор документа.</param>
    public static CadDocument Parse(string json, string expectedFileName)
    {
        using var document = JsonDocument.Parse(json, ParseOptions);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new CadDocumentReadException("Корень документа должен быть объектом JSON.");
        }

        var formatVersion = GetInt32(root, "formatVersion") ?? SupportedFormatVersion;
        if (formatVersion != SupportedFormatVersion)
        {
            throw new CadDocumentReadException(
                $"Неподдерживаемая версия формата: {formatVersion}. Ожидается {SupportedFormatVersion}.");
        }

        var typeText = GetString(root, "type")
            ?? throw new CadDocumentReadException("Не заполнено поле «type».");
        var type = ParseType(typeText);

        var declaredFileName = GetString(root, "fileName");
        if (!string.IsNullOrWhiteSpace(declaredFileName)
            && !string.Equals(declaredFileName, expectedFileName, StringComparison.Ordinal))
        {
            throw new CadDocumentReadException(
                $"Поле «fileName» («{declaredFileName}») не совпадает с именем файла («{expectedFileName}»).");
        }

        var name = GetString(root, "name")
            ?? throw new CadDocumentReadException("Не заполнено поле «name».");

        var (material, massKg) = ReadProperties(root);

        return new CadDocument
        {
            FormatVersion = formatVersion,
            FileName = expectedFileName,
            Type = type,
            Designation = NullIfBlank(GetString(root, "designation")),
            Name = name,
            Material = material,
            MassKg = massKg,
            Components = ReadComponents(root),
        };
    }

    private static CadDocumentType ParseType(string value) => value.Trim() switch
    {
        "Assembly" => CadDocumentType.Assembly,
        "Part" => CadDocumentType.Part,
        "StandardPart" => CadDocumentType.StandardPart,
        _ => throw new CadDocumentReadException($"Неизвестный тип документа: «{value}»."),
    };

    private static (string? Material, decimal? MassKg) ReadProperties(JsonElement root)
    {
        if (!TryGetProperty(root, "properties", out var properties))
        {
            return (null, null);
        }

        if (properties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return (null, null);
        }

        if (properties.ValueKind != JsonValueKind.Object)
        {
            throw new CadDocumentReadException("Поле «properties» должно быть объектом.");
        }

        var material = NullIfBlank(GetString(properties, "material"));
        var massKg = GetDecimal(properties, "mass");

        if (massKg < 0)
        {
            throw new FormatException($"Отрицательная масса: {massKg.Value.ToString(CultureInfo.InvariantCulture)}.");
        }

        return (material, massKg);
    }

    private static IReadOnlyList<CadComponent> ReadComponents(JsonElement root)
    {
        if (!TryGetProperty(root, "components", out var components))
        {
            return Array.Empty<CadComponent>();
        }

        if (components.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Array.Empty<CadComponent>();
        }

        if (components.ValueKind != JsonValueKind.Array)
        {
            throw new CadDocumentReadException("Поле «components» должно быть массивом.");
        }

        var result = new List<CadComponent>(components.GetArrayLength());
        var index = 0;
        foreach (var element in components.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new CadDocumentReadException($"Элемент components[{index}] должен быть объектом.");
            }

            var file = GetString(element, "file")
                ?? throw new CadDocumentReadException($"Не заполнено поле «file» в components[{index}].");

            var count = GetInt32(element, "count")
                ?? throw new CadDocumentReadException($"Не заполнено поле «count» в components[{index}].");

            result.Add(new CadComponent(file, count));
        }

        return result;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static decimal? GetDecimal(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}