using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace CodeSnip.Services
{
    public static class HighlightingService
    {
        /// <summary>
        /// Caches loaded highlighting definitions to improve performance.
        /// The key is a composite string: "{themeFolder}/{langCode}".
        /// </summary>
        private static readonly ConcurrentDictionary<string, IHighlightingDefinition> _highlightCache = new();



        /// <summary>
        /// Applies the appropriate syntax highlighting and folding colors to the editor
        /// based on the given language code and the current application theme.
        /// </summary>
        /// <param name="editor">The TextEditor instance to apply highlighting to.</param>
        /// <param name="langCode">The language code (e.g., "cs", "py") used to find the definition file.</param>
        public static void ApplyHighlighting(TextEditor editor, string? langCode)
        {
            if (editor is null)
                return;

            if (Application.Current is App app)
            {
                var mode = app.ActualThemeVariant;

                string themeFolder = mode == ThemeVariant.Dark
                    ? "Dark"
                    : "Light";

                ApplyHighlightingWithTheme(editor, langCode, themeFolder);
            }
        }


        /// <summary>
        /// Loads and applies a specific highlighting definition from the cache or file system.
        /// </summary>
        private static void ApplyHighlightingWithTheme(TextEditor editor, string? langCode, string themeFolder)
        {
            if (string.IsNullOrWhiteSpace(langCode))
            {
                editor.SyntaxHighlighting = null;
                return;
            }

            try
            {
                //editor.ClearValue(TextEditor.ForegroundProperty);
                if (themeFolder.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                {

                    editor.Foreground = new SolidColorBrush(Color.Parse("#F8F8F2"));
                    //editor.Background = new SolidColorBrush(Color.Parse("#252525")); // iz mahapps metro dark
                }
                else
                {
                    editor.Foreground = new SolidColorBrush(Color.Parse("#000000"));
                    //editor.Background = new SolidColorBrush(Color.Parse("#F5F5F5"));
                }
                string key = $"{themeFolder}/{langCode.ToLower()}";

                // Is the definition in the cache?
                if (_highlightCache.TryGetValue(key, out var cachedHighlighting))
                {
                    editor.SyntaxHighlighting = cachedHighlighting;
                    return;
                }

                // Not in cache → load and store in cache
                string relativePath = Path.Combine("Highlighting", themeFolder, $"{langCode.ToLower()}.xshd");
                IHighlightingDefinition? loadedHighlighting = LoadHighlightingFromPath(relativePath);

                if (loadedHighlighting != null)
                {
                    _highlightCache[key] = loadedHighlighting;
                    editor.SyntaxHighlighting = loadedHighlighting;
                }
                else
                {
                    editor.SyntaxHighlighting = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[HighlightingService] Error loading highlighting for '{langCode}' in {themeFolder}: {ex.Message}");
                editor.SyntaxHighlighting = null;
            }
        }

        /// <summary>
        /// Gets the raw XSHD XML content for a given language and theme, loading from disk first, then from resources.
        /// </summary>
        /// <param name="langCode">The language code (e.g., "cs", "py").</param>
        /// <param name="themeFolder">The theme folder name (e.g., "Dark", "Light").</param>
        /// <returns>The XML content as a string, or null if not found.</returns>
        public static string? GetXshdXml(string langCode, string themeFolder)
        {
            string lowerLangCode = langCode.ToLowerInvariant();
            string relativePath = Path.Combine("Highlighting", themeFolder, $"{lowerLangCode}.xshd");
            string appBase = AppDomain.CurrentDomain.BaseDirectory;
            string diskFilePath = Path.Combine(appBase, relativePath);

            if (File.Exists(diskFilePath))
            {
                return File.ReadAllText(diskFilePath);
            }

            // 2) Učitavanje iz Avalonia resursa
            //    Format: avares://AssemblyName/Resources/Highlighting/...
            string uriString = $"avares://CodeSnip/Resources/{relativePath.Replace('\\', '/')}";

            var uri = new Uri(uriString);

            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            return null;
        }

        /// <summary>
        /// Loads the XSHD definition first from disk, if not found, from resources.
        /// Returns null if not found.
        /// </summary>
        private static IHighlightingDefinition? LoadHighlightingFromPath(string relativeXshdPath)
        {
            try
            {
                string appBase = AppDomain.CurrentDomain.BaseDirectory;
                string diskFilePath = Path.Combine(appBase, relativeXshdPath);

                // Path format is "Highlighting\[Theme]\[lang].xshd"
                var parts = relativeXshdPath.Split(Path.DirectorySeparatorChar);
                string themeFolder = parts.Length > 1 ? parts[1] : string.Empty;
                string langCode = Path.GetFileNameWithoutExtension(relativeXshdPath);

                string? xshdXml = GetXshdXml(langCode, themeFolder);

                if (xshdXml == null)
                    return null;

                using StringReader stringReader = new(xshdXml);
                using XmlReader xmlReader = XmlReader.Create(stringReader);

                return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[HighlightingService] Failed to load highlighting file '{relativeXshdPath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a syntax definition file (.xshd) exists for a given language code,
        /// looking first on disk and then in embedded resources for both themes.
        /// </summary>
        /// <param name="langCode">The language code to check for (e.g., "cs", "py").</param>
        /// <returns><c>true</c> if a definition file is found; otherwise, <c>false</c>.</returns>
        public static bool SyntaxDefinitionExists(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode))
                return false;

            string lowerLangCode = langCode.ToLower();
            string appBase = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Check on disk (both themes)
            string darkDiskPath = Path.Combine(appBase, "Highlighting", "Dark", $"{lowerLangCode}.xshd");
            if (File.Exists(darkDiskPath)) return true;

            string lightDiskPath = Path.Combine(appBase, "Highlighting", "Light", $"{lowerLangCode}.xshd");
            if (File.Exists(lightDiskPath)) return true;

            // 2. Check in resources (both themes)
            try
            {
                Uri darkResourceUri = new($"avares://CodesnipAvalonia/Resources/Highlighting/Dark/{lowerLangCode}.xshd");
                using var stream = AssetLoader.Open(darkResourceUri);
                if (stream.Length > 0) return true;

                Uri lightResourceUri = new($"avares://CodesnipAvalonia/Resources/Highlighting/Light/{lowerLangCode}.xshd");
                using var stream2 = AssetLoader.Open(lightResourceUri);
                if (stream2.Length > 0) return true;
            }
            catch (IOException)
            {
                // This can happen if the resource assembly isn't found, etc.
                // It's safe to assume the definition doesn't exist in this case.
                return false;
            }

            return false;
        }

        private static bool ResourceExists(string relativePath)
        {
            try
            {
                var uri = new Uri($"avares://CodesnipAvalonia/{relativePath.Replace('\\', '/')}");
                return AssetLoader.Exists(uri);
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Removes a specific highlighting definition from the cache. This is useful when a custom
        /// definition file has been updated and needs to be reloaded.
        /// </summary>
        /// <param name="langCode">The language code of the definition to invalidate.</param>
        /// <param name="themeFolder">The theme folder of the definition to invalidate.</param>
        public static void InvalidateCache(string langCode, string themeFolder)
        {
            if (string.IsNullOrWhiteSpace(langCode) || string.IsNullOrWhiteSpace(themeFolder))
                return;

            string key = $"{themeFolder}/{langCode.ToLower()}";
            _highlightCache.TryRemove(key, out _);
        }


        public static bool GenerateBasicXshdFile(string langCode, string langName)
        {
            try
            {
                var darkContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<SyntaxDefinition name=""{langName}"" extensions="".{langCode}"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
    <!-- Monokai theme -->
    <Color name=""Comment""       foreground=""#75715E"" />
    <Color name=""String""        foreground=""#E6DB74"" />
    <Color name=""Character""     foreground=""#FD971F"" />
    <Color name=""Number""        foreground=""#AE81FF"" />
    <Color name=""Keywords""      foreground=""#F92672"" />
    <Color name=""Types""          foreground=""#66D9EF"" />
    <Color name=""MethodName""    foreground=""#A6E22E"" />

    <Property name=""Extension"" value=""{langCode}"" />

<RuleSet ignoreCase=""false"">

     <Keywords color=""Keywords"">
        <!-- Add your keywords here -->
        <Word>SomeKeyword</Word>
    </Keywords>

    <Keywords color=""Types"">
    <!-- Add your types here -->
        <Word>SomeType</Word>
    </Keywords>

  <!-- Add your <RuleSet> and <Span> definitions below -->
</RuleSet>
</SyntaxDefinition>";
                var lightContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<SyntaxDefinition name=""{langName}"" extensions="".{langCode}"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
    <!-- vscode Light Modern-->
    <Color name=""Comment"" foreground=""#FF008000"" />
	<Color name=""String"" foreground=""#FFA31515"" />
	<Color name=""Character"" foreground=""#FFA31515"" />
	<Color name=""Number"" foreground=""#FF098658"" />
	<Color name=""Keywords"" foreground=""#FFAF00DB"" />
	<Color name=""Types"" foreground=""#FF0000FF"" />
	<Color name=""MethodName"" foreground=""#FF795E26"" />

    <Property name=""Extension"" value=""{langCode}"" />

<RuleSet ignoreCase=""false"">

    <Keywords color=""Keywords"">
        <!-- Add your keywords here -->
        <Word>SomeKeyword</Word>
    </Keywords>

    <Keywords color=""Types"">
    <!-- Add your types here -->
        <Word>SomeType</Word>
    </Keywords>

  <!-- Add your <RuleSet> and <Span> definitions below -->
</RuleSet>
</SyntaxDefinition>";
                string appBase = AppDomain.CurrentDomain.BaseDirectory;
                string darkDir = Path.Combine(appBase, "Highlighting", "Dark");
                string lightDir = Path.Combine(appBase, "Highlighting", "Light");
                if (!Directory.Exists(darkDir))
                    Directory.CreateDirectory(darkDir);
                if (!Directory.Exists(lightDir))
                    Directory.CreateDirectory(lightDir);
                string darkFilePath = Path.Combine(darkDir, $"{langCode.ToLower()}.xshd");
                string lightFilePath = Path.Combine(lightDir, $"{langCode.ToLower()}.xshd");
                File.WriteAllText(darkFilePath, darkContent);
                File.WriteAllText(lightFilePath, lightContent);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
