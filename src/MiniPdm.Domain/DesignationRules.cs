using System.Text.RegularExpressions;

namespace MiniPdm.Domain;

/// <summary>
/// Правила формирования и проверки обозначения по ЕСКД.
/// </summary>
/// <remarks>
/// Упрощённый формат: четыре заглавные кириллические буквы, точка, шесть цифр,
/// точка, три цифры. Пример: <c>АБВГ.301245.001</c>.
/// </remarks>
public static partial class DesignationRules
{
    /// <summary>Шаблон формата обозначения.</summary>
    [GeneratedRegex(@"^[А-ЯЁ]{4}\.\d{6}\.\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatRegex();

    /// <summary>Проверяет, соответствует ли обозначение формату ЕСКД.</summary>
    /// <param name="designation">Проверяемое обозначение.</param>
    public static bool IsValid(string? designation)
        => !string.IsNullOrWhiteSpace(designation) && FormatRegex().IsMatch(designation);

    /// <summary>
    /// Возвращает признак корректности обозначения и понятное описание проблемы.
    /// </summary>
    /// <param name="designation">Проверяемое обозначение.</param>
    public static bool TryValidate(string? designation, out string reason)
    {
        if (string.IsNullOrWhiteSpace(designation))
        {
            reason = "Обозначение не указано";
            return false;
        }

        if (!FormatRegex().IsMatch(designation))
        {
            reason = $"Обозначение «{designation}» не соответствует формату ЕСКД " +
                     "(четыре заглавные кириллические буквы, точка, шесть цифр, точка, три цифры)";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Указывает, обязательно ли обозначение для объекта данного типа.
    /// У стандартных изделий обозначение отсутствует.
    /// </summary>
    /// <param name="objectType">Тип объекта.</param>
    public static bool IsRequired(PdmObjectType objectType) => objectType is not PdmObjectType.StandardPart;
}