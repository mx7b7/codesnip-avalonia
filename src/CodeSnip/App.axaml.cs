using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeSnip.Views.MainWindowView;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CodeSnip
{
    public partial class App : Application
    {
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
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                // Call InitializeAsync on the ViewModel after the MainWindow is set up
                // Dispatcher.UIThread.InvokeAsync ensures it runs on the UI thread
                // and Task.Run(() => ...) ensures the actual work is offloaded.
                Dispatcher.UIThread.InvokeAsync(async () => await ((MainWindowViewModel)desktop.MainWindow.DataContext).InitializeAsync());

                // Hook up the unhandled exception handler
                Dispatcher.UIThread.UnhandledException += OnUnhandledException;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static int _errorCount = 0; // Keep track of errors to prevent multiple popups

        private async void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (_errorCount >= 1)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            _errorCount++;

            if (e.Exception is InvalidOperationException ex && ex.Message.Contains("matched 0 characters"))
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
                {
                    await Task.Delay(50);
                    await vm.HandleHighlightingErrorAsync(ex.Message);
                }
            }
            else if (e.Exception != null)
            {
                // For other unhandled exceptions, show a generic error
                // Introduce a small delay to allow the UI thread to stabilize
                await Task.Delay(50);

                await MessageBoxManager.GetMessageBoxStandard("Application Error", $"An unhandled error occurred:\n\n{e.Exception.Message}", ButtonEnum.Ok)
                                     .ShowAsync(); // Pass mainWindow as owner
            }
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