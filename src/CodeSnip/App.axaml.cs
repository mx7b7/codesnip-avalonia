using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeSnip.Services;
using CodeSnip.Views.MainWindowView;
using CodeSnip.Views.SplashScreenView;
using MsBox.Avalonia.Enums;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeSnip
{
    public partial class App : Application
    {
        private static int _errorCount = 0; // Keep track of errors to prevent multiple popups

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // Register global UI thread exception handler BEFORE anything else
                Dispatcher.UIThread.UnhandledException += OnUnhandledException;

                // Show splash screen and load main window inside it
                desktop.MainWindow = new SplashScreen(async () =>
                {

                    var mainWindow = new MainWindow()
                    {
                        DataContext = new MainWindowViewModel()
                    };

                    desktop.MainWindow = mainWindow;

                    // Initialize ViewModel safely (catch async exceptions)
                    try
                    {
                        await ((MainWindowViewModel)mainWindow.DataContext!).InitializeAsync();
                        mainWindow.Show();
                        mainWindow.Focus();

                        await Task.Delay(1000);

                        string crashFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codesnip_crash.txt");
                        if (File.Exists(crashFilePath))
                        {
                            string crashInfo = File.ReadAllText(crashFilePath);
                            string message = "IMPORTANT: If you want to keep this crash log, please copy 'codesnip_crash.txt' " +
                                             "from the app folder BEFORE closing this dialog, as it will be deleted automatically.\n\n" +
                                             $"CodeSnip detected a crash in the previous session. Here are the details:\n\n{crashInfo}";

                            await MessageBoxService.Instance.OkAsync("Previous Crash Detected", message, Icon.Warning);

                            if (File.Exists(crashFilePath))
                                File.Delete(crashFilePath);

                        }
                    }
                    catch (Exception ex)
                    {
                        // Forward async exceptions to global handler
                        var args = CreateArgs(ex);
                        OnUnhandledException(this, args);
                    }
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        private DispatcherUnhandledExceptionEventArgs CreateArgs(Exception ex)
        {
            var ctor = typeof(DispatcherUnhandledExceptionEventArgs)
                .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .First();

            return (DispatcherUnhandledExceptionEventArgs)ctor.Invoke([ex, false]);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (_errorCount >= 1)
            {
                e.Handled = true;
                return;
            }

            _errorCount++;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
                string log = $@"[{timestamp}]
Exception: {e.Exception.Message}
InnerException: {e.Exception.InnerException?.Message}

StackTrace:
{e.Exception.StackTrace}";

                File.WriteAllText("codesnip_crash.txt", log);
            }
            catch { }

            e.Handled = true;
        }
        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}