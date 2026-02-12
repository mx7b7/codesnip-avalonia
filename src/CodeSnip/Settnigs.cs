using Avalonia.Controls;

namespace CodeSnip;

public class MainWindowSettings
{
    public bool LoadSnippetsOnStartup { get; set; } = true;
    public string LastSnippet { get; set; } = "9:22:3";
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
    public string EditorFontFamily { get; set; } = "Consolas";
    public int EditorFontSize { get; set; } = 14;
    
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
