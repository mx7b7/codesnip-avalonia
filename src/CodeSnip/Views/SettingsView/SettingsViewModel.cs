using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using CodeSnip.Services;
using CodeSnip.Views.MainWindowView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeSnip.Views.SettingsView;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;

    private readonly DatabaseService _databaseService;

    // Database
    [ObservableProperty] private string _integrityCheckBadge = "";

    [ObservableProperty] private string _vacuumBadge = "";

    [ObservableProperty] private string _reindexBadge = "";

    [ObservableProperty] private string _backupBadge = "";

    [ObservableProperty] private IBrush _badgeColor = Brushes.Transparent;

    // Theme
    [ObservableProperty] private bool isDarkTheme = true;

    // Editor
    [ObservableProperty] private bool _scrollBelowDocument;

    [ObservableProperty] private bool _tabToSpaces;

    [ObservableProperty] private bool _emailLinks;

    [ObservableProperty] private bool _hyperLinks;

    [ObservableProperty] private bool _highlightLine;

    [ObservableProperty] private int _intendationSize;

    // Main Window
    [ObservableProperty] private double _splitViewOpenPaneLength;

    [ObservableProperty] private bool _loadSnippetsOnStartup;

    [ObservableProperty] private bool _showEmptyLanguages;

    [ObservableProperty] private bool _showEmptyCategories;

    [ObservableProperty] private bool _showLineNumbers;

    [ObservableProperty] private string _editorFontFamily;

    [ObservableProperty] private int _editorFontSize;

    [ObservableProperty] private FontFamily _selectedEditorFont;

    public ObservableCollection<FontFamily> SystemFonts { get; } = new ObservableCollection<FontFamily>(FontManager.Current.SystemFonts);

    public event Action? RequestClose;

    public string? HeaderText { get; private set; }

    public SettingsViewModel(SettingsService settingsService, DatabaseService databaseService)
    {
        _settingsService = settingsService;
        _databaseService = databaseService;
        LoadSnippetsOnStartup = _settingsService.LoadSnippetsOnStartup;
        SplitViewOpenPaneLength = _settingsService.SplitViewOpenPaneLength;
        ScrollBelowDocument = _settingsService.ScrollBelowDocument;
        TabToSpaces = _settingsService.TabToSpaces;
        EmailLinks = _settingsService.EnableEmailLinks;
        HyperLinks = _settingsService.EnableHyperinks;
        HighlightLine = _settingsService.HighlightLine;
        IntendationSize = _settingsService.IntendationSize;
        ShowLineNumbers = _settingsService.ShowLineNumbers;
        EditorFontFamily = _settingsService.EditorFontFamily;
        EditorFontSize = _settingsService.EditorFontSize;
        ShowEmptyLanguages = _settingsService.ShowEmptyLanguages;
        ShowEmptyCategories = _settingsService.ShowEmptyCategories;
        SplitViewOpenPaneLength = _settingsService.SplitViewOpenPaneLength;
        SelectedEditorFont = SystemFonts.FirstOrDefault(f => f.Name == _settingsService.EditorFontFamily) ?? new FontFamily("Consolas");
        IsDarkTheme = settingsService.BaseColor == "Dark"; HeaderText = "Settings";
    }

    public void InitializeFromCurrentTheme()
    {
        if (Application.Current is App app)
        {
            var current = app.RequestedThemeVariant;
            IsDarkTheme = current == ThemeVariant.Dark;
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is App app)
        {
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
            _settingsService.BaseColor = value ? "Dark" : "Light";
        }
        // V1
        //if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        //{
        //    var mainWindow = desktop.MainWindow;
        //    if (mainWindow != null)
        //    {
        //        var textEditor = mainWindow.FindControl<TextEditor>("textEditor");
        //        var mainViewModel = mainWindow.DataContext as MainWindowViewModel;

        //        if (textEditor != null && mainViewModel?.SelectedSnippet?.Category?.Language?.Code != null)
        //        {
        //            HighlightingService.ApplyHighlighting(textEditor, mainViewModel.SelectedSnippet.Category.Language.Code);
        //        }
        //    }
        //}

        // V2
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow?.FindControl<TextEditor>("textEditor") is { } textEditor)
            {
                var vm = desktop.MainWindow.DataContext as MainWindowViewModel;
                var langCode = vm?.SelectedSnippet?.Category?.Language?.Code;
                HighlightingService.ApplyHighlighting(textEditor, langCode);
            }
        }
    }

    partial void OnSelectedEditorFontChanged(FontFamily value)
    {
        EditorFontFamily = value.Name; // Font name for SettingsService
    }

    partial void OnEditorFontFamilyChanged(string value)
    {
        _settingsService.EditorFontFamily = value;
    }

    partial void OnEditorFontSizeChanged(int value)
    {
        _settingsService.EditorFontSize = value;
    }

    [RelayCommand]
    private async Task IntegrityCheck()
    {
        var result = await _databaseService.RunIntegrityCheckAsync();
        if (result)
        {
            IntegrityCheckBadge = "✓";
            BadgeColor = Brushes.LightGreen;
            await Task.Delay(1500);
            IntegrityCheckBadge = "";
            BadgeColor = Brushes.Transparent;
        }
        else
        {
            IntegrityCheckBadge = "✗";
            BadgeColor = Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task Vacuum()
    {
        var result = await _databaseService.RunVacuumAsync();
        if (result)
        {
            VacuumBadge = "✓";
            BadgeColor = Brushes.LightGreen;
            await Task.Delay(1500);
            //_onDatabaseActionCompleted?.Invoke(); // Notify that database action is completed an remove notifications icon from main window
            VacuumBadge = "";
            BadgeColor = Brushes.Transparent;
        }
        else
        {
            VacuumBadge = "✗";
            BadgeColor = Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task Reindex()
    {
        var result = await _databaseService.RunReindexAsync();
        if (result)
        {
            ReindexBadge = "✓";
            BadgeColor = Brushes.LightGreen;
            await Task.Delay(1500);
            ReindexBadge = "";
            BadgeColor = Brushes.Transparent;
        }
        else
        {
            ReindexBadge = "✗";
            BadgeColor = Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task Backup()
    {
        try
        {
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            string dbFilePath = Path.Combine(appFolder, "snippets.sqlite");
            if (!File.Exists(dbFilePath))
            {
                await MessageBoxService.Instance.OkAsync("Backup Failed", "Database file not found.", Icon.Error);
                BackupBadge = "✗";
                BadgeColor = Brushes.Red;
                return;
            }

            string backupFolder = Path.Combine(appFolder, "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string backupFileName = $"snippets-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.sqlite";
            string backupFilePath = Path.Combine(backupFolder, backupFileName);

            File.Copy(dbFilePath, backupFilePath);

            BackupBadge = "✓";
            BadgeColor = Brushes.LightGreen;
            NotificationService.Instance.Show("Backup Success", $"Backup created:\n{backupFileName}", NotificationType.Success);
            await Task.Delay(1000);
            BackupBadge = "";
            BadgeColor = Brushes.Transparent;

        }
        catch (Exception ex)
        {
            BackupBadge = "✗";
            BadgeColor = Brushes.Red;
            await MessageBoxService.Instance.OkAsync("Backup Failed", ex.Message, Icon.Error);
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    public void Dispose() => RequestClose = null;

}

