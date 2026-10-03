using System.Windows;

// Пространство имён MiniPdm.Application перекрывает System.Windows.Application
// внутри проекта с корневым пространством MiniPdm, поэтому базовый класс
// указывается явно.
namespace MiniPdm.Wpf;

/// <summary>
/// Точка входа приложения.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Создаёт приложение.
    /// </summary>
    public App()
    {
        InitializeComponent();
        Startup += OnStartup;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private void OnStartup(object? sender, StartupEventArgs e)
    {
        AppServices services;

        try
        {
            // Composition root создаётся вручную: набор зависимостей невелик,
            // а конфигурация и состав сборки должны быть видны в одном месте.
            services = AppServices.Create();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Не удалось прочитать appsettings.json.\n\n{ex.Message}",
                "Мини-PDM",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }
        // Схема создаётся до показа окна, иначе первый запрос упал бы на
        // несуществующие таблицы. При недоступной базе приложение завершается
        // с понятным сообщением, а не с пустым окном.
        try
        {
            services.InitializeDatabaseAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Не удалось подготовить базу данных.\n\n{ex.Message}\n\n"
                + "Проверьте раздел Database в appsettings.json и доступность сервера.",
                "Мини-PDM",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        MainWindow = services.CreateMainWindow();
        MainWindow.Show();
    }
}