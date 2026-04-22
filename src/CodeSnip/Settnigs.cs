using Avalonia.Controls;
using System;
using TextMateSharp.Grammars;

namespace CodeSnip;

public enum SyntaxEngine
{
    XSHD,
    TextMate

}

public class MainWindowSettings
{
    public bool LoadSnippetsOnStartup { get; set; } = true;
    public string LastSnippet { get; set; } = "9:22:6";
    public double SplitViewPanelLength { get; set; } = 300;
    public bool ShowEmptyLanguages { get; set; } = false;
    public bool ShowEmptyCategories { get; set; } = false;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 720;
    public WindowState WindowState { get; set; } = WindowState.Normal;
}

public class EditorSettings
{
    public bool ScrollBelowDocument { get; set; } = false;
    public bool TabToSpaces { get; set; } = true;
    public bool EnableEmailLinks { get; set; } = false;
    public bool EnableHyperinks { get; set; } = false;
    public bool HighlightLine { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public int IntendationSize { get; set; } = 4;
    public string EditorFontFamily { get; set; }
    public int EditorFontSize { get; set; } = 14;
    public SyntaxEngine SyntaxEngine { get; set; } = SyntaxEngine.XSHD;
    public ThemeName DefaultLightTheme { get; set; } = ThemeName.LightPlus;
    public ThemeName DefaultDarkTheme { get; set; } = ThemeName.DarkPlus;

    public EditorSettings()
    {
        EditorFontFamily = GetDefaultFontFamilyForOS();
    }

    private static string GetDefaultFontFamilyForOS()
    {
        if (OperatingSystem.IsWindows())
            return "Consolas";
        else if (OperatingSystem.IsLinux())
            return "DejaVu Sans Mono";
        else if (OperatingSystem.IsMacOS())
            return "Menlo";
        else
            return "Inter";  // fallback to bundled font
    }

}
public class ThemeSettings
{
    public string BaseColor { get; set; } = "Dark";
    public string Accent { get; set; } = "#FF87794E";
}

public class AppSettings
{
    public MainWindowSettings MainWindow { get; set; } = new MainWindowSettings();
    public ThemeSettings Theme { get; set; } = new ThemeSettings();
    public EditorSettings Editor { get; set; } = new EditorSettings();
}
