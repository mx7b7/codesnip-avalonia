using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Indentation.CSharp;
using CodeSnip.Helpers;
using CodeSnip.Services;
using CodeSnip.Views.CodeRunnerView;
using CodeSnip.Views.CompilerSettingsView;
using CodeSnip.Views.HighlightingEditorView;
using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SnippetView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodeSnip.Views.MainWindowView;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService = new();
    private readonly SettingsService settingsService = new();

    private readonly DefaultIndentationStrategy defaultIndentationStrategy = new();
    private readonly CSharpIndentationStrategy csharpIndentationStrategy = new();

    private static readonly IBrush _darkThemeHighlighterBrush =
    new ImmutableSolidColorBrush(Color.FromArgb(60, 220, 220, 220));

    private static readonly IBrush _lightThemeHighlighterBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(60, 100, 100, 100));

    public TextEditor? Editor { get; set; }
    public TextEditorOptions EditorOptions = new();
    private readonly Geometry? _panelOpenIcon;
    private readonly Geometry? _panelCloseIcon;

    [ObservableProperty] private string? _databaseStatusTooltip;
    [ObservableProperty] private bool _isDatabaseAlertActive;
    [ObservableProperty] private string? _databaseStatusPopupMessage;
    [ObservableProperty] Color _selectedAccentColor = Color.Parse("#FF87794E");

    public ObservableCollection<Language> Languages { get; } = [];

    [ObservableProperty] private bool _isPaneOpen = true;
    [ObservableProperty] private string _windowTitle = "CodeSnip";
    [ObservableProperty] private Snippet? _selectedSnippet;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private bool _isEditorModified;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private Snippet? _editingSnippet;
    [ObservableProperty] private string _editorText = string.Empty;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private SnippetFilterMode _filterMode = SnippetFilterMode.Name;
    [ObservableProperty] private bool isLeftOverlayOpen;
    [ObservableProperty] private bool isRightOverlayOpen;
    [ObservableProperty] private object? leftOverlayContent;
    [ObservableProperty] private object? rightOverlayContent;
    [ObservableProperty] private double leftOverlayWidth = 0;
    [ObservableProperty] private double rightOverlayWidth = 0;
    [ObservableProperty] private bool _disableIndentation = false;
    [ObservableProperty] private object? _selectedNode;
    private int _lastSnippetId = -1;
    private string _oldLangCode = string.Empty;
    private bool _isInternalTextUpdate = true;

    #region SETTINGS PROPERTIES

    [ObservableProperty] private bool _isLoadSnippetEnabled = false;
    [ObservableProperty] private double _splitViewOpenPaneLength = 280;
    [ObservableProperty] private bool _showEmptyLanguages = false;
    [ObservableProperty] private bool _showEmptyCategories = false;
    [ObservableProperty] private FontFamily _editorFontFamily = new("Inter");
    [ObservableProperty] private double _editorFontSize = 14;
    [ObservableProperty] private double _windowWidth = 1200;
    [ObservableProperty] private double _windowHeight = 720;
    [ObservableProperty] public WindowState _windowState = WindowState.Normal;
    [ObservableProperty] private bool _showLineNumbers = true;

    #endregion

    public enum SnippetFilterMode
    {
        Name,
        Tag
    }

    // Languages with C-style braces {} that use CSharpIndentationStrategy and BraceFoldingStrategy
    private static readonly HashSet<string> braceStyleLanguages =
    new(StringComparer.OrdinalIgnoreCase)
    {
            // Original
            "as", "cpp", "cs", "d", "fx", "java", "js", "json", "nut", "php", "rs", "swift",
            "kt", "kts", "groovy", "dart", "v", "sv", "zig", "mm", "h", "c", "go",
            "css", "hcl"
    };

    public MainWindowViewModel()
    {
        _panelOpenIcon = Application.Current?.FindResource("PanelLeftOpen") as Geometry;
        _panelCloseIcon = Application.Current?.FindResource("PanelLeftClose") as Geometry;

        LoadSettingsIntoViewModel();

    }

    private void LoadSettingsIntoViewModel()
    {
        // Window settings
        SplitViewOpenPaneLength = settingsService.SplitViewOpenPaneLength;
        WindowWidth = settingsService.WindowWidth;
        WindowHeight = settingsService.WindowHeight;
        //WindowState = settingsService.WindowState; // Disabled: width/height is in fullscreen mode when restore from maximized
        ShowEmptyCategories = settingsService.ShowEmptyCategories;
        ShowEmptyLanguages = settingsService.ShowEmptyLanguages;
        IsLoadSnippetEnabled = settingsService.LoadSnippetsOnStartup;
        SelectedAccentColor = Color.Parse(settingsService.AccentColor);

        // Editor settings
        EditorOptions.AllowScrollBelowDocument = settingsService.ScrollBelowDocument;
        EditorOptions.EnableEmailHyperlinks = settingsService.EnableEmailLinks;
        EditorOptions.EnableHyperlinks = settingsService.EnableHyperinks;
        EditorOptions.ConvertTabsToSpaces = settingsService.TabToSpaces;
        EditorOptions.HighlightCurrentLine = settingsService.HighlightLine;
        EditorOptions.IndentationSize = settingsService.IntendationSize;
        ShowLineNumbers = settingsService.ShowLineNumbers;
        EditorFontFamily = new FontFamily(settingsService.EditorFontFamily);
        EditorFontSize = settingsService.EditorFontSize;
        // FIX: AvaloniaEdit.Rendering.VisualLinesInvalidException
        // Disabled rectangle selection due to a critical bug in AvaloniaEdit:
        // Selecting text up to the right edge of the editor causes a VisualLinesInvalidException
        // which crashes the application. This occurs in versions 11.4.x and 12.0 RC1.
        EditorOptions.EnableRectangularSelection = false;
        //---------------------------------------------------------------------------------------
    }

    public void InitializeEditor(TextEditor textEditor)
    {
        textEditor.Options = EditorOptions;

        if (settingsService.SyntaxEngine == SyntaxEngine.TextMate)
        {
            if (textEditor.ActualThemeVariant == ThemeVariant.Dark)
            {
                HighlightingService.InstallTextMate(textEditor, settingsService.DefaultDarkTheme);
            }
            else
            {
                HighlightingService.InstallTextMate(textEditor, settingsService.DefaultLightTheme);
            }
            textEditor.SyntaxHighlighting = null;
        }
        textEditor.TextArea.TextView.CurrentLineBackground = new ImmutableSolidColorBrush(Colors.Transparent);
        ReplaceCurrentLineRenderer();
        textEditor.TextArea.SelectionBrush = new SolidColorBrush(SelectedAccentColor, 0.30);
    }

    private void ReplaceCurrentLineRenderer()
    {
        var isDarkTheme = Editor?.ActualThemeVariant == ThemeVariant.Dark;
        Editor?.TextArea.TextView.CurrentLineBorder = new Pen(isDarkTheme ? _darkThemeHighlighterBrush : _lightThemeHighlighterBrush);
    }

    public Geometry? OpenCloseIcon
    {
        get
        {
            return (_panelOpenIcon != null && _panelCloseIcon != null)
                ? (IsPaneOpen ? _panelCloseIcon! : _panelOpenIcon!)
                : null;
        }
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsPaneOpen = !IsPaneOpen;
        OnPropertyChanged(nameof(OpenCloseIcon));
    }

    public void LoadSnippets()
    {
        var languages = _databaseService.GetSnippets();
        PopulateLanguagesCollection(languages);
    }

    private static void EnsureDirectoryStructure()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        string[] requiredDirs =
        {
        Path.Combine(baseDir, "Tools"),
        Path.Combine(baseDir, "Tools", "Interpreters"),
        Path.Combine(baseDir, "Highlighting"),
        Path.Combine(baseDir, "Highlighting", "Dark"),
        Path.Combine(baseDir, "Highlighting", "Light"),
        Path.Combine(baseDir, "Highlighting", "Grammars")
    };

        foreach (var dir in requiredDirs)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }

    public async Task InitializeAsync()
    {
        await Task.Run(() => _databaseService.InitializeDatabaseIfNeeded());

        IsLoadSnippetEnabled = !settingsService.LoadSnippetsOnStartup;

        if (settingsService.LoadSnippetsOnStartup)
        {

            var languages = await Task.Run(() => _databaseService.GetSnippets());

            var languageList = languages.ToList();

            if (languageList.Count == 0 && _databaseService.GetSnippets().Any()) // Check if loading failed
            {
                await MessageBoxService.Instance.OkAsync("Error", "Database Load Error \nCould not load snippets.\nThe database file might be corrupted.", Icon.Error);
                return;
            }
            else
            {
                PopulateLanguagesCollection(languageList);
                if (settingsService.LastSnippet != null)
                {
                    RestoreSelectedSnippetState(settingsService.LastSnippet);
                }
                await UpdateDatabaseHealthStatusAsync();
            }
        }
        EnsureDirectoryStructure();
    }

    public async Task UpdateDatabaseHealthStatusAsync()
    {
        var (needVacuum, fragmentationPercent) = await _databaseService.IsVacuumNeeded();

        IsDatabaseAlertActive = needVacuum;

        if (needVacuum)
        {
            DatabaseStatusTooltip = $"Database is fragmented: {fragmentationPercent:P1} - click for details.";
            DatabaseStatusPopupMessage = $"⚠️  Database fragmentation: {fragmentationPercent:P1}\n\n" +
                                   $"**Run VACUUM** to optimize database.";
        }
    }

    [RelayCommand]
    private async Task VacuumDatabaseAsync()
    {
        var success = await _databaseService.RunVacuumAsync();
        if (success)
        {
            DatabaseStatusPopupMessage = "VACUUM completed successfully!\nDatabase fragmentation has been resolved.";
            IsDatabaseAlertActive = false;
        }
    }

    private void PopulateLanguagesCollection(IEnumerable<Language> languages)
    {
        Languages.Clear();
        foreach (var lang in languages)
        {
            bool languageHasAnySnippets = false;
            foreach (var cat in lang.Categories)
            {
                bool categoryHasSnippets = cat.Snippets.Any();
                if (categoryHasSnippets)
                {
                    languageHasAnySnippets = true;
                }

                // A category is visible if the setting is on, OR if it has snippets.
                cat.IsVisible = ShowEmptyCategories || categoryHasSnippets;
            }
            // A language is visible if the setting is on, OR if it has any snippets.
            lang.IsVisible = ShowEmptyLanguages || languageHasAnySnippets;
            Languages.Add(lang);
        }
    }

    public void ExpandAndSelectSnippet(int languageId, int categoryId, int snippetId)
    {
        var lang = Languages.FirstOrDefault(l => l.Id == languageId);
        if (lang == null) return;

        lang.IsExpanded = true;

        var cat = lang.Categories.FirstOrDefault(c => c.Id == categoryId);
        if (cat == null) return;

        cat.IsExpanded = true;

        var snip = cat.Snippets.FirstOrDefault(s => s.Id == snippetId);

        if (snip == null) return;

        snip.IsSelected = true;
        SelectedSnippet = snip;
        SelectedCategory = cat; // SlelectedCategory is needed for AddSnippet command
                                // Selection in the TreeView should be handled via data binding in the View.
    }

    partial void OnEditorTextChanged(string value)
    {

        if (_isInternalTextUpdate) return;

        IsEditorModified = true;
        UpdateWindowTitle();
    }

    private void UpdateSnippetInMemory(Snippet snippet)
    {
        if (snippet == null) return;

        var language = Languages.FirstOrDefault(l => l.Id == snippet.Category?.Language?.Id);
        if (language == null) return;

        var category = language.Categories.FirstOrDefault(c => c.Id == snippet.CategoryId);
        if (category == null) return;

        var snippetInCollection = category.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
        if (snippetInCollection != null)
        {
            snippetInCollection.Code = snippet.Code;
            snippetInCollection.DateModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    partial void OnDisableIndentationChanged(bool value)
    {
        if (Editor?.TextArea == null) return;
        Editor.TextArea.IndentationStrategy = value ? null : GetIndentationStrategy();
    }

    private IIndentationStrategy? GetIndentationStrategy()
    {
        var langCode = SelectedSnippet?.Category?.Language?.Code ?? "cs";
        return braceStyleLanguages.Contains(langCode)
            ? csharpIndentationStrategy
            : defaultIndentationStrategy;
    }

    private void UpdateIndentationStrategy()
    {
        OnDisableIndentationChanged(DisableIndentation);
    }

    public async Task ChangeSelectedSnippetAsync(Snippet? newSnippet)
    {
        if (newSnippet == null) return;

        if (IsEditorModified && EditingSnippet != null)
        {
            var message = $"You have unsaved changes for ''{EditingSnippet.Title}'.\nDo you want to save them?\n";
            var result = await MessageBoxService.Instance.AskYesNoAsync($"Unsaved Changes", message);

            if (result)
            {
                try
                {
                    _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);
                    EditingSnippet.Code = EditorText;
                    UpdateSnippetInMemory(EditingSnippet);
                    IsEditorModified = false;
                    StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error saving snippet '{EditingSnippet?.Title}': {ex.Message}";
                    await MessageBoxService.Instance.OkAsync("Save Error", $"Failed to save snippet '{EditingSnippet?.Title}'.\n\nDetails: {ex.Message}", Icon.Error);
                    // If save failed, keep IsEditorModified as true and don't proceed with changing the selected snippet
                    return;
                }
            }
            else
            {
                IsEditorModified = false;
            }

        }
        // Lazy load the snippet code if it hasn't been loaded yet.
        if (!newSnippet.IsCodeLoaded)
        {
            try
            {
                newSnippet.Code = _databaseService.GetSnippetCode(newSnippet.Id);
                newSnippet.IsCodeLoaded = true;
            }
            catch (Exception ex)
            {
                await MessageBoxService.Instance.OkAsync("Load Error", $"Failed to load content for snippet '{newSnippet.Title}'.\n\nDetails: {ex.Message}", Icon.Error);
                // Revert the TreeView selection and stop the switch.
                SelectedSnippet = EditingSnippet;
                return;
            }
        }

        SelectedSnippet = newSnippet;
        EditingSnippet = newSnippet;
        SelectedCategory = newSnippet.Category;

        _isInternalTextUpdate = true;
        EditorText = newSnippet.Code ?? string.Empty;
        _isInternalTextUpdate = false;

        UpdateWindowTitle();
        UpdateIndentationStrategy();
        StatusMessage = "Ready";
    }

    public void UpdateWindowTitle()
    {
        var title = "CodeSnip";
        if (SelectedSnippet != null)
            title += " - " + SelectedSnippet.Title;
        if (IsEditorModified)
            title += " *";
        WindowTitle = title;
    }

    partial void OnLeftOverlayContentChanged(object? oldValue, object? newValue)
    {
        if (newValue is IOverlayViewModel overlay)
            overlay.CloseOverlayAsync = CloseLeftOverlayAsync;

    }

    partial void OnRightOverlayContentChanged(object? oldValue, object? newValue)
    {
        if (newValue is IOverlayViewModel overlay)
            overlay.CloseOverlayAsync = CloseRightOverlayAsync;
    }

    public string SaveSelectedSnippetState()
    {
        if (SelectedSnippet == null)
            return string.Empty;

        // Find the parents of the snippet
        var lang = Languages.FirstOrDefault(l => l.Categories.Any(c => c.Snippets.Contains(SelectedSnippet)));
        if (lang == null) return string.Empty;

        var cat = lang.Categories.FirstOrDefault(c => c.Snippets.Contains(SelectedSnippet));
        if (cat == null) return string.Empty;

        // Format: languageId:categoryId:snippetId
        return $"{lang.Id}:{cat.Id}:{SelectedSnippet.Id}";
    }

    public void RestoreSelectedSnippetState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;

        var parts = state.Split(':');
        if (parts.Length != 3) return;

        if (!int.TryParse(parts[0], out int languageId)) return;
        if (!int.TryParse(parts[1], out int categoryId)) return;
        if (!int.TryParse(parts[2], out int snippetId)) return;

        ExpandAndSelectSnippet(languageId, categoryId, snippetId);
    }

    private async Task SaveSettings()
    {
        settingsService.SplitViewOpenPaneLength = (int)SplitViewOpenPaneLength;
        settingsService.LastSnippet = SaveSelectedSnippetState();
        settingsService.WindowState = WindowState;
        if (WindowState == WindowState.Maximized)
        {
            WindowWidth = 1200;   // Reset: because is in fullscreen mode
            WindowHeight = 720;
        }
        settingsService.WindowWidth = WindowWidth;
        settingsService.WindowHeight = WindowHeight;

        await settingsService.SaveSettingsAsync();
    }

    public void OnWindowClosing(CancelEventArgs e)
    {
        if (IsEditorModified && EditingSnippet != null)
        {
            PerformSave();
        }
        _ = SaveSettings();
    }

    partial void OnSelectedAccentColorChanged(Color value)
    {
        settingsService.AccentColor = value.ToString();
        settingsService.ApplyAccentColor();

        Editor?.TextArea.SelectionBrush = new SolidColorBrush(value, 0.30);
    }

    [RelayCommand]
    private async Task ChangeSelectedSnippet(Snippet snippet)
    {
        await ChangeSelectedSnippetAsync(snippet);

        var langCode = snippet.Category?.Language?.Code ?? string.Empty;
        if (!_oldLangCode.Equals(langCode, StringComparison.Ordinal))
        {
            if (settingsService.SyntaxEngine == SyntaxEngine.TextMate)
            {
                if (!HighlightingService.ApplyTextMateHighlighting(langCode))
                    NotificationService.Instance.Show("Syntax Highlighting", $"No TextMate grammar found for language extension '{langCode}'");
            }
            else
            {
                HighlightingService.ApplyHighlighting(Editor!, langCode);
            }
            _oldLangCode = langCode;
        }
        _lastSnippetId = snippet.Id;
        Editor!.TextArea.Caret.Offset = 0;
        Editor.ScrollToHome();
        Editor.Document.UndoStack.ClearAll();
    }

    partial void OnSelectedNodeChanged(object? value)
    {
        switch (value)
        {
            case Snippet snippet:
                if (snippet.Id == _lastSnippetId)
                    return;

                ChangeSelectedSnippetCommand.ExecuteAsync(snippet);
                break;
            case Category category:
                SelectedCategory = category;
                break;
            case Language language:
                SelectedCategory = language.Categories.FirstOrDefault();
                break;
        }
    }

    #region FILTERING

    partial void OnFilterTextChanged(string? oldValue, string newValue)
    {
        ApplySnippetFilter();

        // If the filter has just been cleared (transition from filled to empty string)
        if (!string.IsNullOrWhiteSpace(oldValue) && string.IsNullOrWhiteSpace(newValue))
        {
            // 1. First collapse all nodes
            foreach (var lang in Languages)
            {
                lang.IsExpanded = false;
                foreach (var cat in lang.Categories)
                {
                    cat.IsExpanded = false;
                }
            }

            // 2. Then expand only the path to the currently selected snippet
            if (SelectedSnippet != null)
            {
                if (SelectedSnippet.Category?.Language != null)
                {
                    SelectedSnippet.Category.Language.IsExpanded = true;
                }
                if (SelectedSnippet.Category != null)
                {
                    SelectedSnippet.Category.IsExpanded = true;
                }
                SelectedSnippet.IsSelected = true;
            }
        }
    }

    partial void OnFilterModeChanged(SnippetFilterMode value)
    {
        ApplySnippetFilter();
    }

    private void ApplySnippetFilter()
    {
        bool isFilterActive = !string.IsNullOrWhiteSpace(FilterText);

        foreach (var lang in Languages)
        {
            bool langVisible = false;
            foreach (var cat in lang.Categories)
            {
                bool catVisible = false;
                foreach (var snip in cat.Snippets)
                {
                    // The snippet is visible if the filter is not active, or if it matches the filter
                    bool snipVisible = !isFilterActive || FilterMatch(snip);
                    snip.IsVisible = snipVisible;
                    if (snipVisible)
                    {
                        catVisible = true; // If at least one snippet is visible, the category is also visible
                    }
                }
                cat.IsVisible = catVisible;
                if (catVisible)
                {
                    langVisible = true; // If at least one category is visible, the language is also visible
                }
            }
            lang.IsVisible = langVisible;

            // Automatically expand nodes if the filter is active and they are visible
            if (isFilterActive && langVisible)
            {
                lang.IsExpanded = true;
                foreach (var cat in lang.Categories)
                {
                    if (cat.IsVisible)
                    {
                        cat.IsExpanded = true;
                    }
                }
            }
        }
    }

    private bool MatchOnWordStart(string? textToSearch, string filter)
    {
        if (string.IsNullOrEmpty(textToSearch))
            return false;

        var words = textToSearch.Split([' ', ',', ';', ':', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);

        return words.Any(word => word.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
    }

    private bool FilterMatch(Snippet snippet)
    {
        if (string.IsNullOrWhiteSpace(FilterText)) return true;

        var filterWords = FilterText.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (filterWords.Length == 0) return true;

        foreach (var word in filterWords)
        {
            bool wordMatch = FilterMode switch
            {
                SnippetFilterMode.Name => MatchOnWordStart(snippet.Title, word),
                SnippetFilterMode.Tag => MatchOnWordStart(snippet.Tag, word),
                _ => false
            };

            if (!wordMatch)
                return false;
        }
        return true;
    }


    #endregion

    #region TOOLBAR ACTIONS

    private void PerformSave()
    {
        try
        {
            // This method assumes EditingSnippet is not null.
            _databaseService.UpdateSnippetCode(EditingSnippet!.Id, EditorText);

            EditingSnippet.Code = EditorText; // need this because otherwise the old text is displayed ...
            UpdateSnippetInMemory(EditingSnippet);
            StatusMessage = $"Snippet '{EditingSnippet.Title}' saved at {DateTime.Now:HH:mm:ss}";
            IsEditorModified = false;
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving snippet '{EditingSnippet?.Title}': {ex.Message}";
            _ = MessageBoxManager.GetMessageBoxStandard("Save Error", $"Failed to save snippet '{EditingSnippet?.Title}'.\n\nDetails: {ex.Message}", ButtonEnum.Ok).ShowAsync();
        }
    }

    [RelayCommand]
    private async Task SaveCode()
    {
        if (EditingSnippet is null)
        {
            NotificationService.Instance.Show("Cannot Save", "There is no active snippet to save.\nPlease select a snippet first.");
            return;
        }

        if (IsEditorModified)
        {
            PerformSave();

        }
    }

    [RelayCommand]
    private async Task DeleteSnippet()
    {
        if (IsLeftOverlayOpen) return;

        if (SelectedSnippet is null)
        {
            NotificationService.Instance.Show("No Snippet Selected", "Select a snippet from the list before attempting to delete it.");
            return;
        }

        var confirm = await MessageBoxService.Instance.AskYesNoAsync("Delete Confirmation", $"Are you sure you want to delete the snippet '{SelectedSnippet.Title}'?\n");

        if (!confirm)
            return;

        try
        {
            string snippetTitle = SelectedSnippet.Title;
            _databaseService.DeleteSnippet(SelectedSnippet.Id);

            var category = Languages
                .SelectMany(l => l.Categories)
                .FirstOrDefault(c => c.Id == SelectedSnippet.CategoryId);

            category?.Snippets.Remove(SelectedSnippet);

            // Reset the VM state and clear the editor
            SelectedSnippet = null;

            _isInternalTextUpdate = true;
            EditorText = string.Empty;
            _isInternalTextUpdate = false;

            IsEditorModified = false;
            EditingSnippet = null;
            SelectedCategory = null;
            UpdateWindowTitle();

            NotificationService.Instance.Show("CodeSnip", $"Snippet '{snippetTitle}' deleted successfully.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting snippet '{SelectedSnippet?.Title}'";
            await MessageBoxService.Instance.OkAsync("Delete Error", $"Failed to delete snippet '{SelectedSnippet?.Title}'.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    [RelayCommand]
    private async Task AddSnippet()
    {
        if (IsLeftOverlayOpen) return;

        if (!Languages.Any())
        {
            NotificationService.Instance.Show("No Languages Available", "Please add a language and category before adding a snippet.");
            return;
        }

        LeftOverlayContent = new SnippetViewModel(
            isEditMode: false,
            snippet: new Snippet(),
            languages: [.. Languages],
            databaseService: _databaseService,
            preselectedCategory: SelectedCategory);

        LeftOverlayWidth = 400;
        IsLeftOverlayOpen = true;
    }

    [RelayCommand]
    private async Task EditSnippet()
    {
        if (IsLeftOverlayOpen) return;

        if (SelectedSnippet is null)
        {
            NotificationService.Instance.Show("No Snippet Selected", "Select a snippet from the list before attempting to edit it.");
            return;
        }

        LeftOverlayContent = new SnippetViewModel(
           isEditMode: true,
           snippet: SelectedSnippet,
           languages: [.. Languages],
           databaseService: _databaseService,
           preselectedCategory: SelectedSnippet?.Category);

        LeftOverlayWidth = 400;
        IsLeftOverlayOpen = true;
    }

    [RelayCommand]
    private async Task OpenCodeRunnerView()
    {
        if (IsRightOverlayOpen) return;
        if (SelectedSnippet is null)
        {
            NotificationService.Instance.Show("No Snippet Selected", "Select a snippet from the list before attempting to run it.");
            return;
        }
        string langCode = SelectedSnippet?.Category?.Language?.Code ?? "d";
        if (EditorText != string.Empty)
        {
            RightOverlayContent = new CodeRunnerViewModel(langCode, EditorText, () => EditorText);
            RightOverlayWidth = 600;
            IsRightOverlayOpen = true;
        }
    }

    [RelayCommand]
    private async Task EditLanguageCategory()
    {
        if (IsLeftOverlayOpen) return;

        if (IsLoadSnippetEnabled) return;

        var vm = new LanguageCategoryViewModel(
            dbService: _databaseService);

        vm.RequestCloseAsync += OnLanguageCategoryViewClosedAsync;

        LeftOverlayContent = vm;
        LeftOverlayWidth = 400;
        IsLeftOverlayOpen = true;
    }

    private async Task OnLanguageCategoryViewClosedAsync()
    {
        if (LeftOverlayContent is LanguageCategoryViewModel vm)
        {
            vm.RequestCloseAsync -= OnLanguageCategoryViewClosedAsync;
        }

        Snippet? tmpSnippet = null;
        if (SelectedSnippet != null)
        {
            tmpSnippet = SelectedSnippet;
            if (IsEditorModified)
            {
                PerformSave();
            }
        }
        LoadSnippets(); // Reload the entire snippet collection from the database

        if (tmpSnippet != null)
        {
            // Flatten the entire collection of snippets from the newly loaded data and check if our snippet's ID is still present.
            bool snippetStillExists = Languages
                .SelectMany(l => l.Categories)
                .SelectMany(c => c.Snippets)
                .Any(s => s.Id == tmpSnippet.Id);

            if (snippetStillExists)
            {
                // The snippet was not deleted. Restore the selection in the TreeView
                ExpandAndSelectSnippet(
                    tmpSnippet.Category?.Language?.Id ?? 0,
                    tmpSnippet.CategoryId,
                    tmpSnippet.Id);
            }
            else
            {
                // The snippet was deleted. Reset the editor state completely.
                SelectedSnippet = null;
                EditingSnippet = null;
                EditorText = string.Empty;
                IsEditorModified = false;
                UpdateWindowTitle();
                StatusMessage = "The previously selected snippet was deleted.";
            }
        }
        await CloseLeftOverlayAsync(); // Close the overlay panel
    }

    [RelayCommand]
    private async Task LoadSnippetsDatabase()
    {
        LoadSnippets();
        IsLoadSnippetEnabled = false;
        if (settingsService.LastSnippet != null)
        {
            RestoreSelectedSnippetState(settingsService.LastSnippet);
        }
        await UpdateDatabaseHealthStatusAsync();
    }

    [RelayCommand]
    private void OpenCompilerSettings()
    {
        if (IsRightOverlayOpen) return;

        RightOverlayContent = new CompilerSettingsViewModel();

        RightOverlayWidth = 300;
        IsRightOverlayOpen = true;
    }

    [RelayCommand]
    private void OpenHighlightingEditor()
    {
        if (settingsService.SyntaxEngine == SyntaxEngine.TextMate)
        {
            NotificationService.Instance.Show("Not Supported", "The syntax highlighting editor is not supported when using the TextMate engine.");
            return;
        }

        if (!IsRightOverlayOpen &&
         Editor?.SyntaxHighlighting != null &&
         SelectedSnippet != null)
        {
            string langCode = SelectedSnippet.Category?.Language?.Code ?? "d";
            RightOverlayContent = new HighlightingEditorViewModel(Editor.SyntaxHighlighting, Editor, langCode);
            RightOverlayWidth = 600;
            IsRightOverlayOpen = true;
        }
        else if (SelectedSnippet == null)
        {
            NotificationService.Instance.Show("No Snippet Selected", "Select a snippet from the list before attempting to edit syntax highlighting.");
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (IsRightOverlayOpen) return;

        var vm = new SettingsView.SettingsViewModel(settingsService, _databaseService);

        vm.RequestCloseAsync += OnSettingsViewClosedAsync;


        RightOverlayContent = vm;
        RightOverlayWidth = 350;
        IsRightOverlayOpen = true;
    }

    private async Task OnSettingsViewClosedAsync()
    {
        if (RightOverlayContent is SettingsView.SettingsViewModel vm)
        {
            vm.RequestCloseAsync -= OnSettingsViewClosedAsync;

            bool oldShowEmptyLanguages = ShowEmptyLanguages;
            bool oldShowEmptyCategories = ShowEmptyCategories;

            // Editor
            settingsService.ScrollBelowDocument = vm.ScrollBelowDocument;
            settingsService.HighlightLine = vm.HighlightLine;
            settingsService.EnableEmailLinks = vm.EmailLinks;
            settingsService.EnableHyperinks = vm.HyperLinks;
            settingsService.TabToSpaces = vm.TabToSpaces;
            settingsService.IntendationSize = vm.IntendationSize;
            settingsService.ShowLineNumbers = vm.ShowLineNumbers;
            settingsService.EditorFontFamily = vm.EditorFontFamily;
            settingsService.EditorFontSize = vm.EditorFontSize;
            // Main Window
            settingsService.LoadSnippetsOnStartup = vm.LoadSnippetsOnStartup;
            settingsService.ShowEmptyLanguages = vm.ShowEmptyLanguages;
            settingsService.ShowEmptyCategories = vm.ShowEmptyCategories;
            settingsService.SplitViewOpenPaneLength = vm.SplitViewOpenPaneLength;
            // INSTANT APPLICATION:
            //Editor
            EditorOptions.AllowScrollBelowDocument = vm.ScrollBelowDocument;
            EditorOptions.HighlightCurrentLine = vm.HighlightLine;
            EditorOptions.EnableEmailHyperlinks = vm.EmailLinks;
            EditorOptions.ConvertTabsToSpaces = vm.TabToSpaces;
            EditorOptions.IndentationSize = vm.IntendationSize;
            EditorOptions.EnableHyperlinks = vm.HyperLinks;
            ShowLineNumbers = vm.ShowLineNumbers;
            EditorFontFamily = new FontFamily(vm.EditorFontFamily);
            EditorFontSize = vm.EditorFontSize;
            // Main Window
            ShowEmptyLanguages = vm.ShowEmptyLanguages;
            ShowEmptyCategories = vm.ShowEmptyCategories;
            SplitViewOpenPaneLength = vm.SplitViewOpenPaneLength;
            SelectedAccentColor = vm.SelectedAccentColor;

            if (oldShowEmptyLanguages != ShowEmptyLanguages || oldShowEmptyCategories != ShowEmptyCategories)
            {
                Snippet? tmpSnippet = null;
                if (EditingSnippet != null)
                {
                    tmpSnippet = EditingSnippet;
                }
                LoadSnippets(); // Reload to apply visibility changes
                if (tmpSnippet != null)
                {
                    ExpandAndSelectSnippet(
                        tmpSnippet.Category?.Language?.Id ?? 0,
                        tmpSnippet.CategoryId,
                        tmpSnippet.Id);
                }
            }
            await CloseRightOverlayAsync();

            ReplaceCurrentLineRenderer();
            //settingsService.SaveSettings(); // Moved to OnWindowClosing
        }
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        try
        {
            if (IsRightOverlayOpen) return;// prevent theme toggle when right overlay is open, because some views there (like HighlightingEditor) do not support dynamic theme change for avalonedit xshd loading

            if (Application.Current is App app)
            {
                var current = app.RequestedThemeVariant ?? ThemeVariant.Light; // fallback
                var next = current == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;

                app.RequestedThemeVariant = next;

                settingsService.BaseColor = next == ThemeVariant.Dark ? "Dark" : "Light";
                // settingsService.ApplyAccentColor(); //Re - apply accent color because Avalonia 12 resets resources

                if (Editor != null)
                {
                    if (settingsService.SyntaxEngine == SyntaxEngine.TextMate)
                    {
                        if (next == ThemeVariant.Dark)
                        {
                            HighlightingService.SetTextMateTheme(settingsService.DefaultDarkTheme);
                        }
                        else
                        {
                            HighlightingService.SetTextMateTheme(settingsService.DefaultLightTheme);
                        }
                    }
                    else
                    {
                        HighlightingService.ApplyHighlighting(Editor, SelectedSnippet?.Category?.Language?.Code);
                    }

                    ReplaceCurrentLineRenderer();
                }
            }
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Theme Error", $"An error occurred while changing the theme.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    [RelayCommand]
    private void IncreasePaneWidth()
    {
        SplitViewOpenPaneLength = Math.Min(SplitViewOpenPaneLength + 2, 500);
        StatusMessage = $"Pane width: {SplitViewOpenPaneLength}px";
    }

    [RelayCommand]
    private void DecreasePaneWidth()
    {
        SplitViewOpenPaneLength = Math.Max(SplitViewOpenPaneLength - 2, 50);
        StatusMessage = $"Pane width: {SplitViewOpenPaneLength}px";
    }

    [RelayCommand]
    private void IncreaseFontSize()
    {
        if (EditorFontSize < 36)
            EditorFontSize++;
    }

    [RelayCommand]
    private void DecreaseFontSize()
    {
        if (EditorFontSize > 8)
            EditorFontSize--;
    }

    [RelayCommand]
    private void ResetFontSize() => EditorFontSize = settingsService.EditorFontSize;

    #endregion

    #region CONTEXT MENU COMMANDS

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Editor!.Document.UndoStack.Undo();
    private bool CanUndo() => Editor!.Document.UndoStack.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Editor!.Document.UndoStack.Redo();
    private bool CanRedo() => Editor!.Document.UndoStack.CanRedo;

    [RelayCommand(CanExecute = nameof(CanCut))]
    private void Cut() => Editor!.Cut();
    private bool CanCut() => !string.IsNullOrEmpty(Editor!.SelectedText);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy() => Editor!.Copy();
    private bool CanCopy() => !string.IsNullOrEmpty(Editor!.SelectedText);

    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll() => Editor!.SelectAll();
    private bool CanSelectAll() => !string.IsNullOrEmpty(Editor!.Text);

    [RelayCommand]
    private void Paste() => Editor!.Paste();

    [RelayCommand]
    private void ToggleSingleLineComment()
    {
        var editor = Editor;
        if (editor == null) return;

        string? code = SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            try
            {
                CommentService.ToggleCommentByExtension(editor, code, useMultiLine: false);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
    }

    [RelayCommand]
    private void ToggleMultiLineComment()
    {
        if (Editor == null) return;

        string? code = SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            try
            {
                CommentService.ToggleCommentByExtension(Editor, code, useMultiLine: true);
            }
            catch { }
        }
    }

    [RelayCommand]
    private void ToggleCommentSelection()
    {
        if (Editor == null) return;

        string? code = SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            try
            {
                CommentService.ToggleInlineCommentByExtension(Editor, code);
            }
            catch { }
        }
    }

    [RelayCommand]
    private async Task ExportToPng()
    {
        if (SelectedSnippet == null)
        {
            NotificationService.Instance.Show("No Snippet Selected", "Select a snippet first to determine the filename.");
            return;
        }

        if (Editor == null) return;

        if (settingsService.SyntaxEngine == SyntaxEngine.TextMate)
        {
            NotificationService.Instance.Show("Not Supported", "Exporting as image is not supported when using the TextMate engine.");
            return;
        }

        string code;
        bool isSelection = false;
        string displayTitle = SelectedSnippet.Title;
        string fileNameTitle = SelectedSnippet.Title;

        if (Editor.TextArea.Selection is RectangleSelection selection && !selection.IsEmpty)
        {
            code = selection.GetText();
            isSelection = true;
            displayTitle = "";
        }
        else
        {
            code = Editor.Text;
        }

        int lineCount = code.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;

        if (lineCount > 250)
        {
            string message = isSelection
            ? $"Selection has {lineCount} lines. The maximum allowed for image export is 250."
            : $"Snippet has {lineCount} lines. The maximum is 250.\n\nPlease use Rectangle Selection (Alt + Mouse Drag) to select a smaller part.";

            await MessageBoxService.Instance.OkAsync("Snippet Too Large", message, Icon.Warning);
            return;
        }

        try
        {
            string invalidChars = new(Path.GetInvalidFileNameChars());
            foreach (char c in invalidChars) fileNameTitle = fileNameTitle.Replace(c.ToString(), "_");
            // Replace one or more whitespace characters with a single underscore
            fileNameTitle = Regex.Replace(fileNameTitle, @"\s+", "_");

            var bitmap = await ImageExporter.ExportToImageAsync(
                                            code,
                                            displayTitle,
                                            Editor.SyntaxHighlighting,
                                            Editor.FontFamily,
                                            Editor.FontSize,
                                            Editor.ShowLineNumbers
                                        ) ?? throw new Exception("Image exporter returned null.");

            string exportsDir = Path.Combine(AppContext.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportsDir);

            string filePath = Path.Combine(exportsDir, $"{fileNameTitle}.png");

            using var fs = File.Create(filePath);
            bitmap?.Save(fs);

            NotificationService.Instance.Show("Export Success", $"Image exported successfully as '{fileNameTitle}.png' in Exports directory!");
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", $"Failed to export as image: {ex.Message}", Icon.Error);
        }
    }


    #endregion

    // CLOSE LEFT PANEL
    [RelayCommand]
    public async Task CloseLeftOverlayAsync()
    {
        if (LeftOverlayContent is IDisposable d)
            d.Dispose();

        LeftOverlayWidth = 0;
        await Task.Delay(250);

        LeftOverlayContent = null;
        IsLeftOverlayOpen = false;

    }

    // CLOSE RIGHT PANEL
    [RelayCommand]
    public async Task CloseRightOverlayAsync()
    {
        if (RightOverlayContent is IDisposable d)
            d.Dispose();

        RightOverlayWidth = 0;
        await Task.Delay(250);

        RightOverlayContent = null;
        IsRightOverlayOpen = false;

    }


}
