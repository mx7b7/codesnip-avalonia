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
using TextMateSharp.Grammars;

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
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] Color _selectedAccentColor = Color.Parse("#FF87794E");

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

    [ObservableProperty] private SyntaxEngine _syntaxEngine;

    [ObservableProperty] private ObservableCollection<ThemeName> _lightTextMateThemes;

    [ObservableProperty] private ObservableCollection<ThemeName> _darkTextMateThemes;

    [ObservableProperty] private ThemeName _selectedLightTheme;

    [ObservableProperty] private ThemeName _selectedDarkTheme;

    private static readonly ThemeName[] LightThemes =
                            [
                                ThemeName.Light, ThemeName.LightPlus, ThemeName.QuietLight,
                                ThemeName.SolarizedLight, ThemeName.HighContrastLight,
                                ThemeName.AtomOneLight, ThemeName.VisualStudioLight
                            ];

    private static readonly ThemeName[] DarkThemes =
                            [
                                ThemeName.Abbys, ThemeName.Dark, ThemeName.DarkPlus, ThemeName.DimmedMonokai,
                                ThemeName.KimbieDark, ThemeName.OneDark, ThemeName.Monokai, ThemeName.Red,
                                ThemeName.SolarizedDark, ThemeName.TomorrowNightBlue, ThemeName.HighContrastDark,
                                ThemeName.Dracula, ThemeName.AtomOneDark, ThemeName.VisualStudioDark
                            ];

    public ObservableCollection<FontFamily> SystemFonts { get; } = new ObservableCollection<FontFamily>(FontManager.Current.SystemFonts.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));

    public event Func<Task>? RequestCloseAsync;

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
        SelectedEditorFont = SystemFonts.FirstOrDefault(f => f.Name == _settingsService.EditorFontFamily)
                   ?? SystemFonts.FirstOrDefault(f => f.Name.Contains("Consolas", StringComparison.OrdinalIgnoreCase))
                   ?? SystemFonts.FirstOrDefault(f => f.Name.Contains("Mono", StringComparison.OrdinalIgnoreCase))
                   ?? SystemFonts.FirstOrDefault(f => f.Name.Contains("Courier", StringComparison.OrdinalIgnoreCase))
                   ?? SystemFonts.First();
        IsDarkTheme = settingsService.BaseColor == "Dark";
        HeaderText = "Settings";
        SelectedAccentColor = Color.Parse(settingsService.AccentColor);
        SyntaxEngine = _settingsService.SyntaxEngine;
        LightTextMateThemes = new(LightThemes);
        DarkTextMateThemes = new(DarkThemes);

        SelectedLightTheme = _settingsService.DefaultLightTheme;
        SelectedDarkTheme = _settingsService.DefaultDarkTheme;
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
            // _settingsService.ApplyAccentColor(); // Re-apply accent color to update theme resources avalonia v12 ???
        }
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow?.FindControl<TextEditor>("textEditor") is { } textEditor)
            {
                var vm = desktop.MainWindow.DataContext as MainWindowViewModel;
                var langCode = vm?.SelectedSnippet?.Category?.Language?.Code;
                if (SyntaxEngine == SyntaxEngine.TextMate)
                {
                    if (value == true)
                        HighlightingService.SetTextMateTheme(SelectedDarkTheme);
                    else
                        HighlightingService.SetTextMateTheme(SelectedLightTheme);
                }
                else
                {
                    HighlightingService.ApplyHighlighting(textEditor, langCode);
                }
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

    partial void OnSelectedAccentColorChanged(Color value)
    {
        _settingsService.AccentColor = value.ToString();
        _settingsService.ApplyAccentColor();
    }

    partial void OnSyntaxEngineChanged(SyntaxEngine value)
    {
        _settingsService.SyntaxEngine = value;
    }

    partial void OnSelectedLightThemeChanged(ThemeName value)
    {
        _settingsService.DefaultLightTheme = value;
        if (!IsDarkTheme && HighlightingService.IsTextMateInstalled())
            HighlightingService.SetTextMateTheme(value);

    }

    partial void OnSelectedDarkThemeChanged(ThemeName value)
    {
        _settingsService.DefaultDarkTheme = value;
        if (IsDarkTheme && HighlightingService.IsTextMateInstalled())
            HighlightingService.SetTextMateTheme(value);

    }

    [RelayCommand]
    private async Task ApplySyntaxEngine()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow?.FindControl<TextEditor>("textEditor") is { } textEditor)
            {
                var vm = desktop.MainWindow.DataContext as MainWindowViewModel;
                var langCode = vm?.SelectedSnippet?.Category?.Language?.Code;

                if (SyntaxEngine == SyntaxEngine.TextMate)
                {
                    ThemeName themeName = IsDarkTheme ? SelectedDarkTheme : SelectedLightTheme;
                    textEditor.SyntaxHighlighting = null;

                    if (HighlightingService.IsTextMateInstalled())
                    {
                        if (langCode != null)
                        {
                            if (!HighlightingService.ApplyTextMateHighlighting(langCode))
                                NotificationService.Instance.Show("Syntax Highlighting", $"No TextMate grammar found for language extension '{langCode}'");
                        }
                    }
                    else
                    {
                        HighlightingService.InstallTextMate(textEditor, themeName);
                       
                        if (langCode != null)
                        {
                            if (!HighlightingService.ApplyTextMateHighlighting(langCode))
                                NotificationService.Instance.Show("Syntax Highlighting", $"No TextMate grammar found for language extension '{langCode}'");
                        }
                    }
                }
                else  // XSHD
                {
                    if (HighlightingService.IsTextMateInstalled())
                        HighlightingService.UninstallTextMate(); // Uninstall TextMate to revert to XSHD

                    HighlightingService.ApplyHighlighting(textEditor, langCode);
                }
            }
        }
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
    private async Task Close()
    {
        if (RequestCloseAsync != null)
            await RequestCloseAsync();
    }

    [RelayCommand]
    private async Task ResetSettings()
    {
        try
        {
            var result = await MessageBoxService.Instance.AskYesNoAsync("Reset Settings", "Are you sure you want to reset all settings to their default values?");
            if (result)
            {
                await _settingsService.ResetToDefaults();
                SyntaxEngine = _settingsService.SyntaxEngine;
                InitializeFromCurrentTheme();
                await ApplySyntaxEngineCommand.ExecuteAsync(null);
                //_settingsService.ApplyAccentColor(); //avalonia v12 need to re-apply accent color to update theme resources
                NotificationService.Instance.Show("Settings Reset", "All settings have been reset to default values.", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Reset Failed", $"An error occurred while resetting settings:\n{ex.Message}", Icon.Error);
        }
    }

    public void Dispose()
    {
        RequestCloseAsync = null;
    }

}

