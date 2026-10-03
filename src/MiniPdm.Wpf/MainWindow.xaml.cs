using System.Windows;
using System.Windows.Input;
using MiniPdm.Wpf.ViewModels;

namespace MiniPdm.Wpf;

/// <summary>
/// Главное окно: дерево состава, карточка объекта, импорт и поиск.
/// </summary>
/// <remarks>
/// Код представления отвечает только за диалоги и за перенос событий в команды
/// модели представления: вся логика находится в <see cref="MainWindowViewModel"/>.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>Создаёт окно.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.SelectionRequested += OnSelectionRequested;
            await viewModel.LoadAsync();
        }
    }

    private async void OnSelectionRequested(object? sender, EventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshSelectionAsync();
        }
    }

    private void OnSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        // Enter в поле поиска равносилен нажатию кнопки.
        if (ViewModel is { } viewModel)
        {
            viewModel.SearchText = SearchBox.Text;
            e.Handled = true;
            _ = viewModel.SearchAsync();
        }
    }

    private void OnBrowseImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Папка выгрузки CAD-системы",
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true || ViewModel is not { } viewModel)
        {
            return;
        }

        _ = viewModel.ImportCommand.ExecuteAsync(dialog.FolderName);
    }

    private void OnExportSpecification(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        // Явно указан Microsoft.Win32: после подключения Windows Forms тип
        // SaveFileDialog стал неоднозначным.
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Сохранить спецификацию",
            Filter = "CSV (*.csv)|*.csv",
            FileName = "specification.csv",
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.ExportSpecificationCommand.Execute(dialog.FileName);
        }
    }

    private void OnSearchClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            _ = viewModel.SearchAsync();
        }
    }
}

/// <summary>
/// Диалог выбора папки.
/// </summary>
/// <remarks>
/// В .NET 8 такого диалога в WPF нет, а решение на базе Shell.Application
/// показывает лишнее окно. Поэтому папка выбирается через <c>FolderBrowserDialog</c>
/// из Windows Forms, который точно работает и не требует подключения пакета.
/// </remarks>
internal sealed class OpenFolderDialog
{
    /// <summary>Заголовок окна.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Разрешено ли выбрать несколько папок.</summary>
    public bool Multiselect { get; init; }

    /// <summary>Выбранная папка.</summary>
    public string? FolderName { get; private set; }

    /// <summary>Показывает окно выбора.</summary>
    /// <returns><c>true</c>, если пользователь подтвердил выбор.</returns>
    public bool ShowDialog(Window owner)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Title,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return false;
        }

        FolderName = dialog.SelectedPath;
        return true;
    }
}
