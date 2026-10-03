using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MiniPdm.Application.Import;

// Windows Forms подключён ради диалога выбора папки и добавляет System.Drawing
// в неявные using'и, где Brush означает совсем другое.
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace MiniPdm.Wpf.Converters;

/// <summary>
/// Цвета строк отчёта импорта по результату обработки документа.
/// </summary>
/// <remarks>
/// Заливки берутся светлыми, а не «чистым» зелёным, жёлтым и красным: на
/// насыщенном фоне чёрный текст нечитаем, а таблицу с 45 строками приходится
/// просматривать подряд. Тёмные тона остаются только для текста результата.
///
/// Кисти заморожены: они разделяются между всеми строками таблицы, а
/// пересоздавать их на каждое отображение незачем.
/// </remarks>
public static class ImportSeverityPalette
{
    private static readonly Brush OkBackground = Frozen("#E8F5E9");
    private static readonly Brush OkForeground = Frozen("#1B5E20");
    private static readonly Brush WarningBackground = Frozen("#FFF8E1");
    private static readonly Brush WarningForeground = Frozen("#7A5900");
    private static readonly Brush ErrorBackground = Frozen("#FFEBEE");
    private static readonly Brush ErrorForeground = Frozen("#B71C1C");

    /// <summary>Заливка строки по результату обработки.</summary>
    public static Brush Background(ImportSeverity severity) => severity switch
    {
        ImportSeverity.Ok => OkBackground,
        ImportSeverity.Warning => WarningBackground,
        _ => ErrorBackground,
    };

    /// <summary>Цвет текста результата по итогу обработки.</summary>
    public static Brush Foreground(ImportSeverity severity) => severity switch
    {
        ImportSeverity.Ok => OkForeground,
        ImportSeverity.Warning => WarningForeground,
        _ => ErrorForeground,
    };

    private static Brush Frozen(string hex) => new SolidColorBrush(
        (Color)ColorConverter.ConvertFromString(hex));
}

/// <summary>Подставляет цвет строки отчёта по результату обработки.</summary>
public sealed class ImportSeverityBackgroundConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ImportSeverity severity ? ImportSeverityPalette.Background(severity) : Brushes.Transparent;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Обратное преобразование не требуется.");
}

/// <summary>Подставляет цвет текста результата по итогу обработки.</summary>
public sealed class ImportSeverityForegroundConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ImportSeverity severity ? ImportSeverityPalette.Foreground(severity) : Brushes.Black;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Обратное преобразование не требуется.");
}

/// <summary>Преобразует булево в индекс вкладки.</summary>
/// <remarks>
/// Используется, чтобы автоматически открыть вкладку «Отчёт импорта» при
/// наличии отклонённых документов. Конвертер прост и не хранит состояние.
/// </remarks>
public sealed class TabIndexConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? 3 : 0;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index && index == 3;
}

/// <summary>Показывает элемент, когда значение равно <c>true</c>.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>
/// Показывает элемент, когда значение равно <c>false</c>.
/// </summary>
/// <remarks>
/// Отдельный конвертер, а не параметр у предыдущего: в XAML параметр приходит
/// строкой, и разбор «Inverse» разными конвертерами давал бы разное поведение.
/// </remarks>
public sealed class BooleanToVisibilityInverseConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

/// <summary>Инвертирует логическое значение.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Скрывает элемент, если строка пуста.</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Обратное преобразование не требуется.");
}

/// <summary>Превращает уровень вложенности в отступ строки дерева.</summary>
public sealed class IndentConverter : IValueConverter
{
    /// <summary>Ширина отступа одного уровня, в единицах прибора.</summary>
    public const double LevelWidth = 14d;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int depth ? new Thickness(depth * LevelWidth, 0, 0, 0) : new Thickness(0);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Обратное преобразование не требуется.");
}
