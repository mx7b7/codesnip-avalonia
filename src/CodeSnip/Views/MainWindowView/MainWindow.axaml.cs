using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using CodeSnip.Helpers;
using CodeSnip.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeSnip.Views.MainWindowView;

public interface IOverlayViewModel
{
    Func<Task>? CloseOverlayAsync { get; set; }
}

public partial class MainWindow : ControlsEx.Window.Window
{
    private MainWindowViewModel? _viewModel;
    private readonly TextEditor? _textEditor;
    private int _lastSnippetId = -1;

    private MainWindowViewModel ViewModel =>
    _viewModel ??= (MainWindowViewModel?)DataContext
    ?? throw new InvalidOperationException("ViewModel not initialized");

    public MainWindow()
    {
        InitializeComponent();

        _textEditor = this.FindControl<TextEditor>("textEditor");
        if (_textEditor == null)
            return;

        DataContextChanged += OnDataContextChanged;
        NotificationService.Instance.Initialize(this);
        MessageBoxService.Instance.Register(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.Editor = _textEditor;
        vm.InitializeEditor(_textEditor!);

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.LeftOverlayContent) &&
                 vm.LeftOverlayContent is IOverlayViewModel leftOverlay)
            {
                leftOverlay.CloseOverlayAsync = vm.CloseLeftOverlayAsync;
            }
            if (args.PropertyName == nameof(MainWindowViewModel.RightOverlayContent) &&
                 vm.RightOverlayContent is IOverlayViewModel rightOverlay)
            {
                rightOverlay.CloseOverlayAsync = vm.CloseRightOverlayAsync;
            }
        };
    }

    private async void TreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
            return;

        var selected = e.AddedItems[0];

        switch (selected)
        {
            case SnippetView.Snippet snippet:
                if (snippet.Id == _lastSnippetId)
                    return;

                await ViewModel.ChangeSelectedSnippetAsync(snippet);

                _lastSnippetId = snippet.Id;
                var langCode = snippet.Category?.Language?.Code ?? string.Empty;
                HighlightingService.ApplyHighlighting(_textEditor!, langCode);
                _textEditor?.Document.UndoStack.ClearAll();
                break;

            case LanguageCategoryView.Category category:
                ViewModel.SelectedCategory = category;
                break;

            case LanguageCategoryView.Language lang:
                ViewModel.SelectedCategory = lang.Categories.FirstOrDefault();
                break;

        }

    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnWindowClosing(e);
        }
    }

    private async void FormatClang_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {

            var supported = new[]
            {
                "c", "cpp", "h", "cs", "d", "js", "java", "mjs", "ts",
                "json", "m", "mm", "proto", "protodevel", "td", "txtpb",
                "textpb", "textproto", "asciipb", "sv", "svh", "v", "vh"
            };

            if (supported.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                string originalCode = _textEditor!.Text;
                string filename = $"example.{code}";
                var (isSuccess, formattedClang, errorClang) = await FormattingService.TryFormatCodeWithClangAsync(originalCode, assumeFilename: filename);
                if (isSuccess)
                {
                    _textEditor.Document.Text = formattedClang;
                }
                else
                {
                    _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", $"Formatting failed:\n {errorClang}" ?? "", ButtonEnum.Ok).ShowAsync();
                }
            }
        }
    }

    private async void FormatCSharpier_Click(object? sender, RoutedEventArgs e)
    {

        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            var originalCode = _textEditor!.Text;

            (bool isSuccess, string? formatted, string? error) = code switch
            {
                "cs" => await FormattingService.TryFormatCodeWithCSharpierAsync(originalCode),
                "xml" => await FormattingService.TryFormatXmlWithCSharpierAsync(originalCode),
                _ => (false, null, $"Formatting for '{code}' is not supported with CSharpier")
            };

            if (isSuccess)
            {
                _textEditor.Document.Text = formatted!;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", $"Formatting failed:\n {error}", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatPrettier_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            var supported = new[]
                    {
                    "js","jsx","ts","tsx","css","scss","html","json","md","mdx","vue","yaml","yml"
                 };
            if (supported.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                string filename = $"example.{code}";
                string originalCode = _textEditor!.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPrettierAsync(originalCode, assumeFilename: filename);
                if (isSuccess)
                {
                    _textEditor.Document.Text = formatted;
                }
                else
                {
                    _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", $"Formatting failed:\n {error}", ButtonEnum.Ok).ShowAsync();
                }
            }
        }
    }

    private async void FormatFantomas_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "fs")
        {
            string originalCode = _textEditor!.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithFantomasAsync(originalCode);
            if (isSuccess)
            {
                _textEditor.Document.Text = formatted;
            }
            else if (error != null && error.Contains("Could not execute", StringComparison.OrdinalIgnoreCase))
            {
                error += "\n\nMake sure Fantomas is installed. You can install it via the .NET CLI:\n\n" +
                         "Globally:\n" +
                         "  dotnet tool install -g fantomas-tool\n\n" +
                         "Local:\n" +
                         "Open command prompt in Tools directory" +
                         "  dotnet new tool-manifest\n" +
                         "  dotnet tool install fantomas";
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", $"Formatting failed:\n {error}", ButtonEnum.Ok).ShowAsync();
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", $"Formatting failed:\n {error}", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatDfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "d")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithDfmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatBlack_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "py")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPythonFormatterAsync(originalCode, "black");
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }

        }
    }

    private async void FormatAutopep8_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "py")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPythonFormatterAsync(originalCode, "autopep8");
            if (isSuccess)
            {
                textEditor.Document.Text = formatted!;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }

        }
    }

    private async void FormatRuff_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "py")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithRuffAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }

        }
    }

    private async void FormatRustfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "rs")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithRustFmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatGofmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "go")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithGofmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatStylua_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "lua")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithStyluaAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatPasfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "pas")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPasFmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatShfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "sh")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithShFmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                _ = await MessageBoxManager.GetMessageBoxStandard("Formatting error", error ?? "", ButtonEnum.Ok).ShowAsync();
            }
        }
    }

    private async void FormatAll_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            switch (code.ToLowerInvariant())
            {
                case "cs":
                    FormatCSharpier_Click(sender, e);
                    break;

                case "d":
                    FormatDfmt_Click(sender, e);
                    break;

                case "fs":
                    FormatFantomas_Click(sender, e);
                    break;

                case "go":
                    FormatGofmt_Click(sender, e);
                    break;

                case "lua":
                    FormatStylua_Click(sender, e);
                    break;

                case "pas":
                    FormatPasfmt_Click(sender, e);
                    break;

                case "py":
                    FormatBlack_Click(sender, e);
                    break;

                case "rs":
                    FormatRustfmt_Click(sender, e);
                    break;

                case "xml":
                    FormatCSharpier_Click(sender, e);
                    break;

                case "html":
                case "css":
                case "md":
                    FormatPrettier_Click(sender, e);
                    break;

                case "sh":
                    FormatShfmt_Click(sender, e);
                    break;
                default:
                    // DEFAULT: Use clang-format for other supported languages
                    FormatClang_Click(sender, e);
                    break;
            }
        }
    }

    private async void CopyAsMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(textEditor.SelectedText))
            return;

        string langCode = ViewModel.SelectedSnippet?.Category?.Language?.Code ?? "";
        langCode = MapLangCodeToMarkdown(langCode);
        string markdownCode = $"```{langCode}\n{textEditor.SelectedText}\n```";


        try
        {
            if (GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(markdownCode ?? string.Empty);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard Error", "Failed to copy to clipboard");
        }
    }

    private async void CopyAsHtml_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(textEditor.SelectedText))
            return;

        string encodedCode = WebUtility.HtmlEncode(textEditor.SelectedText);
        string htmlCode = $"<pre><code>{encodedCode}</code></pre>";

        try
        {
            if (GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(htmlCode ?? string.Empty);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard Error", "Failed to copy to clipboard");
        }
    }

    private async void CopyAsBBCode_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(textEditor.SelectedText))
            return;
        string langCode = ViewModel.SelectedSnippet?.Category?.Language?.Code ?? "";
        string bbCode = $"[code={langCode}]{textEditor.SelectedText}[/code]";
        try
        {
            if (GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(bbCode ?? string.Empty);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard Error", "Failed to copy to clipboard");
        }
    }

    private async void CopyAsJsonString_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(textEditor.SelectedText))
            return;

        string jsonString = JsonSerializer.Serialize(textEditor.SelectedText);

        try
        {
            if (GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(jsonString ?? string.Empty);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard Error", "Failed to copy to clipboard");
        }
    }

    private async void CopyAsBase64String_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = textEditor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        try
        {
            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(selectedText);
            string base64String = Convert.ToBase64String(textBytes);

            if (GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(base64String ?? string.Empty);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard Error", "Failed to copy to clipboard");
        }
    }

    private async void ExportToFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel != null && ViewModel.SelectedSnippet != null)
            {

                var success = await FileExporter.ExportToFile(StorageProvider, ViewModel.EditorText, ViewModel.SelectedSnippet.Title, ViewModel.SelectedSnippet?.Category?.Language?.Code);
                if (success)
                {
                    NotificationService.Instance.Show("Export Success", $"File '{ViewModel.SelectedSnippet?.Title}' exported successfully!");
                }
                else
                {
                    NotificationService.Instance.Show("Export Cancelled", "Export was cancelled or failed.");
                }
            }
        }
        catch
        {
            NotificationService.Instance.Show("Export Error", $"Failed to export file");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutView.AboutWindow();
        aboutWindow.ShowDialog(this);
    }

    private static string MapLangCodeToMarkdown(string code)
    {
        return code.ToLower() switch
        {
            "cs" => "csharp",
            "cpp" => "cpp",
            "js" => "javascript",
            "ts" => "typescript",
            "py" => "python",
            "java" => "java",
            "html" => "html",
            "xml" => "xml",
            "json" => "json",
            "rb" => "ruby",
            "php" => "php",
            "go" => "go",
            "rs" => "rust",
            "swift" => "swift",
            "kt" or "kts" => "kotlin",
            "sh" or "bash" => "bash",
            "ps1" => "powershell",
            "sql" => "sql",
            "d" => "d",
            "vb" => "vbnet",
            "lua" => "lua",
            "md" => "markdown",
            "yml" or "yaml" => "yaml",
            "jsonc" => "jsonc",
            "dockerfile" => "dockerfile",
            "makefile" => "makefile",
            "ini" => "ini",
            "toml" => "toml",
            "h" => "c", // Header files as C
            "m" => "objective-c",
            "mm" => "objective-c++",
            "hs" => "haskell",
            "erl" => "erlang",
            "ex" or "exs" => "elixir",
            "r" => "r",
            "jl" => "julia",
            "scala" => "scala",
            "f" or "for" or "f90" => "fortran",
            "ada" or "adb" => "ada",
            "asm" or "s" => "assembly",
            "v" or "vh" or "sv" or "svh" => "systemverilog",
            "vhdl" => "vhdl",
            "ml" => "ocaml",
            "nim" => "nim",
            "zig" => "zig",
            _ => "", // Default to no language if not recognized
        };
    }


}