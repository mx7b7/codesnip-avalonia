using System;
using System.IO;

namespace CodeSnip;

public static class AppPaths
{
    // We use ProcessPath because it is 100% safe for Single-File publish,
    // and if for some reason it returns null, the fallback is BaseDirectory.
    public static readonly string AppRoot = Path.GetDirectoryName(Environment.ProcessPath)
                                            ?? AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// AppRoot/Tools
    /// </summary>
    public static readonly string Tools = Path.Combine(AppRoot, "Tools");

    /// <summary>
    /// AppRoot/Tools/Interpreters
    /// </summary>
    public static readonly string Interpreters = Path.Combine(Tools, "Interpreters");

    /// <summary>
    /// AppRoot/Tools/Interpreters/Temp
    /// </summary>
    public static readonly string CodeRunnerTemp = Path.Combine(Interpreters, "Temp");

    /// <summary>
    /// AppRoot/Highlighting
    /// </summary>
    public static readonly string Highlighting = Path.Combine(AppRoot, "Highlighting");

    /// <summary>
    /// AppRoot/Highlighting/Dark 
    /// </summary>
    public static readonly string HighlightingDark = Path.Combine(Highlighting, "Dark");

    /// <summary>
    /// AppRoot/Highlighting/Light
    /// </summary>
    public static readonly string HighlightingLight = Path.Combine(Highlighting, "Light");

    /// <summary>
    /// AppRoot/Highlighting/Grammars
    /// </summary>
    public static readonly string TextMateGrammars = Path.Combine(Highlighting, "Grammars");

    /// <summary>
    /// AppRoot/Backups
    /// </summary>
    public static readonly string Backups = Path.Combine(AppRoot, "Backups");

    /// <summary>
    /// AppRoot/Exports
    /// </summary>
    public static readonly string Exports = Path.Combine(AppRoot, "Exports");

    /// <summary>
    /// AppRoot/codesnip.json
    /// </summary>
    public static readonly string AppSettingsFile = Path.Combine(AppRoot, "codesnip.json");

    /// <summary>
    /// AppRoot/compilers.json
    /// </summary>
    public static readonly string CompilersSettingsFile = Path.Combine(AppRoot, "compilers.json");

    public static void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(Tools);
            Directory.CreateDirectory(Interpreters);
            Directory.CreateDirectory(Highlighting);
            Directory.CreateDirectory(HighlightingDark);
            Directory.CreateDirectory(HighlightingLight);
            Directory.CreateDirectory(TextMateGrammars);
            Directory.CreateDirectory(Exports);
            Directory.CreateDirectory(Backups);
            Directory.CreateDirectory(CodeRunnerTemp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppPaths] Greška pri inicijalizaciji mapa: {ex.Message}");
        }
    }
}