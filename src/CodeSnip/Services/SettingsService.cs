using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using MsBox.Avalonia.Enums;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodeSnip.Services;

public class SettingsService
{
    private const string SettingsFile = "codesnip.json";
    private AppSettings _settings;

    public bool LoadSnippetsOnStartup
    {
        get => _settings.MainWindow.LoadSnippetsOnStartup;
        set => _settings.MainWindow.LoadSnippetsOnStartup = value;
    }


    public string LastSnippet
    {
        get => _settings.MainWindow.LastSnippet;
        set => _settings.MainWindow.LastSnippet = value;
    }

    public double SplitViewOpenPaneLength
    {
        get => _settings.MainWindow.SplitViewPanelLength;
        set => _settings.MainWindow.SplitViewPanelLength = value;
    }

    public double WindowWidth
    {
        get => _settings.MainWindow.WindowWidth;
        set => _settings.MainWindow.WindowWidth = value;
    }

    public double WindowHeight
    {
        get => _settings.MainWindow.WindowHeight;
        set => _settings.MainWindow.WindowHeight = value;
    }

    public WindowState WindowState
    {
        get => _settings.MainWindow.WindowState;
        set => _settings.MainWindow.WindowState = value;
    }

    public bool ScrollBelowDocument
    {
        get => _settings.Editor.ScrollBelowDocument;
        set => _settings.Editor.ScrollBelowDocument = value;
    }

    public bool TabToSpaces
    {
        get => _settings.Editor.TabToSpaces;
        set => _settings.Editor.TabToSpaces = value;
    }

    public bool EnableEmailLinks
    {
        get => _settings.Editor.EnableEmailLinks;
        set => _settings.Editor.EnableEmailLinks = value;
    }

    public bool EnableHyperinks
    {
        get => _settings.Editor.EnableHyperinks;
        set => _settings.Editor.EnableHyperinks = value;
    }
    public bool HighlightLine
    {
        get => _settings.Editor.HighlightLine;
        set => _settings.Editor.HighlightLine = value;
    }

    public int IntendationSize
    {
        get => _settings.Editor.IntendationSize;
        set => _settings.Editor.IntendationSize = value;
    }

    public string EditorFontFamily
    {
        get => _settings.Editor.EditorFontFamily;
        set => _settings.Editor.EditorFontFamily = value;
    }

    public int EditorFontSize
    {
        get => _settings.Editor.EditorFontSize;
        set => _settings.Editor.EditorFontSize = value;
    }

    public bool ShowLineNumbers
    {
        get => _settings.Editor.ShowLineNumbers;
        set => _settings.Editor.ShowLineNumbers = value;
    }

    public bool ShowEmptyLanguages
    {
        get => _settings.MainWindow.ShowEmptyLanguages;
        set => _settings.MainWindow.ShowEmptyLanguages = value;
    }

    public bool ShowEmptyCategories
    {
        get => _settings.MainWindow.ShowEmptyCategories;
        set => _settings.MainWindow.ShowEmptyCategories = value;
    }

    public string BaseColor
    {
        get => _settings.Theme.BaseColor;
        set => _settings.Theme.BaseColor = value;
    }

    public string AccentColor
    {
        get => _settings.Theme.Accent;
        set => _settings.Theme.Accent = value;
    }

    public SettingsService()
    {
        _settings = new AppSettings();
        _ = LoadSettingsAsync(); // Auto-load on init
        ApplyAccentColor();
        ApplyTheme();
    }

    private async Task LoadSettingsAsync()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                string json = File.ReadAllText(SettingsFile);
                // Attempt to deserialize. If the file is corrupt, Deserialize will return null or throw an exception.
                // In that case, we use the default settings (?? new AppSettings()).
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                ValidateAndCorrectSettings();
            }
            catch (JsonException ex)
            {
                await MessageBoxService.Instance.OkAsync("Settings service", $"Error deserializing settings: {ex.Message}. Loading default settings.", Icon.Error);
                _settings = new AppSettings();
            }
            catch (Exception ex)
            {
                await MessageBoxService.Instance.OkAsync("Settings service", $"Error reading settings file: {ex.Message}. Loading default settings.", Icon.Error);
                _settings = new AppSettings();
            }
        }
        else
        {
            // If the file does not exist, _settings will be initialized automatically 
            // with default values in the AppSettings class constructor.
            _settings = new AppSettings();
            await SaveSettingsAsync();
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Settings service", $"Error saving settings: {ex.Message}", Icon.Error);
        }
    }

    public async Task ResetToDefaults()
    {
        _settings = new AppSettings();
        ApplyAccentColor();
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        if (Application.Current is Application app)
        {
            app.RequestedThemeVariant = BaseColor switch
            {
                "Dark" => ThemeVariant.Dark,
                "Light" => ThemeVariant.Light,
                _ => ThemeVariant.Light // fallback
            };
        }
    }


    private void ValidateAndCorrectSettings()
    {
        var defaultSettings = new AppSettings();

        // 1. Validate WindowState
        if (!Enum.IsDefined(typeof(WindowState), _settings.MainWindow.WindowState))
        {
            _settings.MainWindow.WindowState = defaultSettings.MainWindow.WindowState;
        }

        // 2. Validate EditorFontFamily
        if (string.IsNullOrWhiteSpace(_settings.Editor.EditorFontFamily))
        {
            _settings.Editor.EditorFontFamily = defaultSettings.Editor.EditorFontFamily;
        }

        // 3. Validate Theme BaseColor
        var validThemes = new[] { "Light", "Dark" };
        if (!validThemes.Contains(_settings.Theme.BaseColor, StringComparer.OrdinalIgnoreCase))
        {
            _settings.Theme.BaseColor = defaultSettings.Theme.BaseColor; // "Dark"
        }
        // 4. Validate AccentColor HEX
        if (!IsValidHexColor(_settings.Theme.Accent))
        {
            _settings.Theme.Accent = defaultSettings.Theme.Accent; // "#FF87794E"
        }
    }

    private static bool IsValidHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        // Standard HEX format: #RRGGBB, #RGB, #RRGGBBAA
        return Regex.IsMatch(hex.Trim(), @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3}|[A-Fa-f0-9]{8})$");
    }

    public void ApplyAccentColor()
    {
        if (Application.Current is not Application app)
            return;

        var baseColor = Color.Parse(AccentColor);

        // -----------------------------
        // RGB
        // -----------------------------
        byte r = baseColor.R;
        byte g = baseColor.G;
        byte b = baseColor.B;

        // -----------------------------
        // Predefined shades
        // -----------------------------
        var c100 = new Color(255, r, g, b); // FF
        var c80 = new Color(204, r, g, b); // CC
        var c60 = new Color(153, r, g, b); // 99
        var c40 = new Color(102, r, g, b); // 66
        var c20 = new Color(51, r, g, b); // 33

        // -----------------------------
        // Accent Colors
        // -----------------------------
        app.Resources["ThemeAccentColor"] = c80;
        app.Resources["ThemeAccentColor2"] = c60;
        app.Resources["ThemeAccentColor3"] = c40;
        app.Resources["ThemeAccentColor4"] = c20;

        app.Resources["ThemeAccentBrush"] = new SolidColorBrush(c80);
        app.Resources["ThemeAccentBrush2"] = new SolidColorBrush(c60);
        app.Resources["ThemeAccentBrush3"] = new SolidColorBrush(c40);
        app.Resources["ThemeAccentBrush4"] = new SolidColorBrush(c20);

        // -----------------------------
        // Highlight Colors
        // -----------------------------
        app.Resources["HighlightColor"] = c100;
        app.Resources["HighlightColor2"] = c80;

        app.Resources["HighlightBrush"] = new SolidColorBrush(c100);
        app.Resources["HighlightBrush2"] = new SolidColorBrush(c80);

        // -----------------------------
        // Control Highlight Colors
        // -----------------------------
        app.Resources["ThemeControlHighlightHighColor"] = c100;
        app.Resources["ThemeControlHighlightMidColor"] = c80;
        app.Resources["ThemeControlHighlightLowColor"] = c40;

        app.Resources["ThemeControlHighlightHighBrush"] = new SolidColorBrush(c100);
        app.Resources["ThemeControlHighlightMidBrush"] = new SolidColorBrush(c80);
        app.Resources["ThemeControlHighlightLowBrush"] = new SolidColorBrush(c40);

    }
}
