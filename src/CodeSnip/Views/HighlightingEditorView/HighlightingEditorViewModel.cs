using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using CodeSnip.Services;
using CodeSnip.Views.MainWindowView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CodeSnip.Views.HighlightingEditorView
{

    public partial class HighlightingEditorViewModel : ObservableObject, IOverlayViewModel
    {
        public Action? CloseOverlay { get; set; }
        private XshdValidationService validationService = new();
        #region Fields
        private readonly IHighlightingDefinition? _originalDefinition;
        private readonly TextEditor _mainEditor = null!;
        private readonly string _themeName = null!;
        private readonly string _languageCode = null!;
        private readonly string _customXshdPath = null!;
        #endregion

        #region Properties
        [ObservableProperty] private string? _header;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResetDefinitionCommand))]
        private bool customDefinitionExists;

        public ObservableCollection<HighlightingColorInfo> HighlightingColors { get; } = [];

        [ObservableProperty] public HighlightingColorInfo? selectedColor;

        [ObservableProperty] public string? xshdText;

        public IHighlightingDefinition? XmlHighlighting { get; private set; }

        public Brush? XshdEditorForeground { get; private set; }

        #endregion

        public HighlightingEditorViewModel(IHighlightingDefinition? definition, TextEditor editor, string langCode)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(langCode);

            _originalDefinition = definition;
            _mainEditor = editor;
            _languageCode = langCode;

            if (definition == null)
            {
                return;
            }

            if (Application.Current is App app)
            {
                var mode = app.ActualThemeVariant;

                _themeName = mode == ThemeVariant.Dark
                    ? "Dark"
                    : "Light";

                _customXshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Highlighting", _themeName, $"{_languageCode}.xshd");
            }

            LoadHighlightingColors();
            UpdateCustomDefinitionExists();

            if (!TryGetOriginalXshd(out string? xshdXml))
                return;

            XshdText = xshdXml;
            InitializeXshdEditor();
            Header = $"Customize syntax highlighting for the {definition.Name} language";
        }

        #region Commands
        [RelayCommand(CanExecute = nameof(CanResetDefinition))]
        private async Task ResetDefinition()
        {
            var confirm = await MessageBoxManager.GetMessageBoxStandard("Reset Definition", "Are you sure you want to delete your custom highlighting and revert to the default?", ButtonEnum.YesNo).ShowAsync();
            if (confirm == ButtonResult.No) return;

            try
            {
                if (File.Exists(_customXshdPath))
                    File.Delete(_customXshdPath);

                HighlightingService.InvalidateCache(_languageCode, _themeName);
                HighlightingService.ApplyHighlighting(_mainEditor, _languageCode);

                if (TryGetOriginalXshd(out string? xshdXml))
                    XshdText = xshdXml;

                LoadHighlightingColors();
                UpdateCustomDefinitionExists();
                await SyncFromRawCommand.ExecuteAsync(null);
                _ = await MessageBoxManager.GetMessageBoxStandard("Success", "Highlighting definition has been reset to default.", ButtonEnum.Ok).ShowAsync();
            }
            catch (Exception ex)
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to reset definition: {ex.Message}", ButtonEnum.Ok).ShowAsync();

            }
        }

        [RelayCommand]
        private async Task ApplyLivePreview()
        {
            var (isValid, definition) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (isValid && definition != null)
            {
                _mainEditor.SyntaxHighlighting = definition;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrEmpty(_customXshdPath) || string.IsNullOrWhiteSpace(XshdText))
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Error", "Cannot save - invalid path or empty content.", ButtonEnum.Ok).ShowAsync();
                return;
            }

            var (isValid, _) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (!isValid) return;

            try
            {
                var doc = XDocument.Parse(XshdText);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var extensionProperty = doc.Root?.Descendants(ns + "Property")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == "Extension");

                if (extensionProperty != null)
                    extensionProperty.SetAttributeValue("value", _languageCode);
                else
                    doc.Root?.Add(new XElement(ns + "Property", new XAttribute("name", "Extension"), new XAttribute("value", _languageCode)));

                string textToSave = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc.ToString();
                if (textToSave != XshdText) XshdText = textToSave;

                Directory.CreateDirectory(Path.GetDirectoryName(_customXshdPath)!);
                await File.WriteAllTextAsync(_customXshdPath, XshdText, Encoding.UTF8);

                HighlightingService.InvalidateCache(_languageCode, _themeName);
                UpdateCustomDefinitionExists();
                HighlightingService.ApplyHighlighting(_mainEditor, _languageCode);
                _ = await MessageBoxManager.GetMessageBoxStandard("Success", $"Saved to:\n{_customXshdPath}", ButtonEnum.Ok).ShowAsync();
            }
            catch (Exception ex)
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to save: {ex.Message}", ButtonEnum.Ok).ShowAsync();
            }
        }

        [RelayCommand]
        private async Task SyncFromRaw()
        {
            var (isValid, tempDefinition) = await ValidateAndLoadDefinitionAsync(XshdText);
            if (!isValid || tempDefinition == null) return;

            try
            {
                var newColors = HighlightingParser.ExtractColors(tempDefinition);
                ApplyColors(newColors);
            }
            catch (Exception ex)
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Error", $"An unexpected error occurred while synchronizing the highlighting definition.\n\n{ex.Message}", ButtonEnum.Ok).ShowAsync();
            }
        }

        [RelayCommand]
        private async Task FormatXshd()
        {
            if (string.IsNullOrWhiteSpace(XshdText))
                return;

            try
            {
                string formatted = FormatXml(XshdText);
                if (formatted == XshdText)
                    return;

                XshdText = formatted; // Behavior će ovo poslati u editor
            }
            catch (XmlException ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("Format Error", $"Invalid XML: {ex.Message}", ButtonEnum.Ok)
                    .ShowAsync();
            }
        }

        #endregion

        #region Private Methods
        private bool CanResetDefinition() => CustomDefinitionExists;

        private void UpdateCustomDefinitionExists() => CustomDefinitionExists = File.Exists(_customXshdPath);

        private void OnHighlightingColorChanged(object? sender, EventArgs e) => UpdateXshdTextFromColors();

        private static string FormatXml(string xml)
        {
            var doc = XDocument.Parse(xml);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc.ToString();
        }

        private void LoadHighlightingColors()
        {
            foreach (var color in HighlightingColors)
                color.PropertyChangedWithValue -= OnHighlightingColorChanged;

            HighlightingColors.Clear();
            if (_originalDefinition == null) return;

            var colors = HighlightingParser.ExtractColors(_originalDefinition);
            foreach (var color in colors)
            {
                color.PropertyChangedWithValue += OnHighlightingColorChanged;
                HighlightingColors.Add(color);
            }
            SelectedColor = HighlightingColors.FirstOrDefault();
        }

        private void InitializeXshdEditor()
        {
            string? xmlXshd = HighlightingService.GetXshdXml("xml", _themeName);
            if (xmlXshd != null)
            {
                using var reader = new StringReader(xmlXshd);
                using var xmlReader = XmlReader.Create(reader);
                XmlHighlighting = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            }
            else
            {
                XmlHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".xml");
            }

            XshdEditorForeground = new SolidColorBrush(Color.Parse("#000000"));
            if (_themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                XshdEditorForeground = new SolidColorBrush(Color.Parse("#F8F8F8"));
        }

        private bool TryGetOriginalXshd(out string? xshdXml)
        {
            xshdXml = HighlightingService.GetXshdXml(_languageCode, _themeName);
            return xshdXml != null;
        }

        private void UpdateXshdTextFromColors()
        {
            if (string.IsNullOrWhiteSpace(XshdText)) return;
            try
            {
                XshdText = HighlightingSerializer.GenerateUpdatedXshd(XshdText, [.. HighlightingColors]);
            }
            catch { }
        }

        private void ApplyColors(IEnumerable<HighlightingColorInfo> newColors)
        {
            string? selectedColorName = SelectedColor?.Name;
            foreach (var c in HighlightingColors)
                c.PropertyChangedWithValue -= OnHighlightingColorChanged;

            HighlightingColors.Clear();
            foreach (var c in newColors)
            {
                c.PropertyChangedWithValue += OnHighlightingColorChanged;
                HighlightingColors.Add(c);
            }

            if (selectedColorName != null)
            {
                //SelectedColor = HighlightingColors.FirstOrDefault(c => c.Name == selectedColorName);
                var newSelected = HighlightingColors.FirstOrDefault(c => c.Name == selectedColorName);
                // FORCE UI REFRESH
                SelectedColor = null;
                SelectedColor = newSelected;
            }

        }

        private async Task<(bool IsValid, IHighlightingDefinition? Definition)> ValidateAndLoadDefinitionAsync(string? xshdText)
        {
            if (string.IsNullOrWhiteSpace(xshdText))
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Error", "XSHD source is empty.", ButtonEnum.Ok).ShowAsync();
                return (false, null);
            }

            var validationResult = validationService.Validate(xshdText);
            if (!validationResult.IsValid)
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Validation Errors",
                    $"XSHD definition issues:\n\n{string.Join("\n", validationResult.Errors)}", ButtonEnum.Ok).ShowAsync();
                return (false, null);
            }

            try
            {
                using var reader = new StringReader(xshdText);
                using var xmlReader = XmlReader.Create(reader);
                var definition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
                return (true, definition);
            }
            catch { return (false, null); }
        }

        #endregion

        [RelayCommand]
        private void Cancel()
        {
            CloseOverlay?.Invoke();
        }
    }

    #region Helper classes

    /// <summary>
    /// Represents a single named color from a highlighting definition, allowing its properties to be edited.
    /// </summary>
    public partial class HighlightingColorInfo : ObservableObject
    {
        public event EventHandler? PropertyChangedWithValue;
        /// <summary>Gets or sets the name of the color definition (e.g., "Comment", "String").</summary>
        public string Name { get; set; } = string.Empty;

        [ObservableProperty]
        private Color? foreground;

        [ObservableProperty]
        private Color? background;

        [ObservableProperty]
        private FontWeight fontWeight = FontWeight.Normal;

        [ObservableProperty]
        private FontStyle fontStyle = FontStyle.Normal;

        [ObservableProperty]
        private bool underline;

        [ObservableProperty]
        private bool strikethrough;

        [ObservableProperty]
        private int? fontSize;


        /// <summary>
        /// Gets the collection of available font weights for the UI.
        /// </summary>
        public ObservableCollection<FontWeight> AvailableFontWeights { get; } =
    [
        FontWeight.Normal,
        FontWeight.Bold
    ];
        /// <summary>
        /// Gets the collection of available font styles for the UI.
        /// </summary>
        public ObservableCollection<FontStyle> AvailableFontStyles { get; } =
    [
        FontStyle.Normal,
        FontStyle.Italic,
        FontStyle.Oblique
    ];

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            PropertyChangedWithValue?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// A helper class to parse color information from an <see cref="IHighlightingDefinition"/>.
    /// </summary>
    public static class HighlightingParser
    {
        public static List<HighlightingColorInfo> ExtractColors(IHighlightingDefinition definition)
        {
            var result = new List<HighlightingColorInfo>();

            foreach (var named in definition.NamedHighlightingColors.OrderBy(c => c.Name))
            {
                var fg = named.Foreground?.GetColor(null);
                var bg = named.Background?.GetColor(null);

                var colorInfo = new HighlightingColorInfo
                {
                    Name = named.Name,
                    Foreground = fg,
                    Background = bg,
                    FontWeight = named.FontWeight ?? FontWeight.Normal,
                    FontStyle = named.FontStyle ?? FontStyle.Normal,
                    FontSize = named.FontSize,
                    Underline = named.Underline ?? false,
                    Strikethrough = named.Strikethrough ?? false
                };

                result.Add(colorInfo);
            }
            return result;
        }

    }

    /// <summary>
    /// A helper class to serialize highlighting color changes back into an XSHD file format.
    /// </summary>
    public static class HighlightingSerializer
    {
        public static string GenerateUpdatedXshd(string xshdXml, List<HighlightingColorInfo> overrides)
        {
            var doc = XDocument.Parse(xshdXml);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var overridesDict = overrides.ToDictionary(o => o.Name);

            var colorElements = doc.Root?.Elements(ns + "Color").ToList();

            if (colorElements != null)
            {
                foreach (var colorElem in colorElements)
                {
                    var name = colorElem.Attribute("name")?.Value;
                    if (name != null && overridesDict.TryGetValue(name, out var colorOverride))
                    {
                        // Preserve and re-apply 'exampleText' to ensure it's last.
                        var exampleTextAttr = colorElem.Attribute("exampleText");
                        string? exampleTextValue = exampleTextAttr?.Value;
                        exampleTextAttr?.Remove();
                        // Helper to set or remove attribute
                        void SetOrRemove(string attrName, string? value)
                        {
                            if (!string.IsNullOrEmpty(value))
                                colorElem.SetAttributeValue(attrName, value);
                            else
                                colorElem.Attribute(attrName)?.Remove();
                        }

                        SetOrRemove("foreground", colorOverride.Foreground?.ToString());
                        SetOrRemove("background", colorOverride.Background?.ToString());
                        SetOrRemove("fontWeight", colorOverride.FontWeight == FontWeight.Normal ? null : colorOverride.FontWeight.ToString().ToLowerInvariant());
                        SetOrRemove("fontStyle", colorOverride.FontStyle == FontStyle.Normal ? null : colorOverride.FontStyle.ToString().ToLowerInvariant());
                        SetOrRemove("fontSize", colorOverride.FontSize?.ToString());
                        SetOrRemove("underline", colorOverride.Underline ? "true" : null);
                        SetOrRemove("strikethrough", colorOverride.Strikethrough ? "true" : null);
                        if (exampleTextValue != null)
                        {
                            colorElem.SetAttributeValue("exampleText", exampleTextValue);// Re-add 'exampleText' at the end if it existed
                        }
                    }
                }
            }

            using var sw = new StringWriterUtf8();
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
            {
                doc.WriteTo(xw);
            }
            return sw.ToString();
        }

    }

    public class StringWriterUtf8 : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }


    #endregion
}