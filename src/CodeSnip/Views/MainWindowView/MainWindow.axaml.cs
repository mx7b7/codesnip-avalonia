using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodeSnip.Helpers;
using CodeSnip.Services;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace CodeSnip.Views.MainWindowView;

public interface IOverlayViewModel
{
    Func<Task>? CloseOverlayAsync { get; set; }
}

public partial class MainWindow : ControlsEx.Window.Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        NotificationService.Instance.Initialize(this);
        MessageBoxService.Instance.Register(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.Editor = textEditor;
        vm.InitializeEditor(textEditor);

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

    private void Window_Opened(object? sender, EventArgs e)
    {
        if (ViewModel != null && ViewModel.Languages.Count == 0)
        {
            NotificationService.Instance.Show("Welcome to CodeSnip!", "It looks like you don't have any languages yet. Start by adding a language and category, then create your first snippet!", NotificationType.Information, 10);
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
                string originalCode = textEditor.Text;
                string filename = $"example.{code}";
                var (isSuccess, formattedClang, errorClang) = await FormattingService.TryFormatCodeWithClangAsync(originalCode, assumeFilename: filename);
                if (isSuccess)
                {
                    textEditor.Document.Text = formattedClang;
                }
                else
                {
                    await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {errorClang}" ?? "", MsBox.Avalonia.Enums.Icon.Error);
                }
            }
        }
    }

    private async void FormatCSharpier_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null)
        {
            var originalCode = textEditor.Text;

            (bool isSuccess, string? formatted, string? error) = code switch
            {
                "cs" => await FormattingService.TryFormatWithCSharpierAsync(originalCode),
                "xml" => await FormattingService.TryFormatWithCSharpierAsync(originalCode),
                _ => (false, null, $"Formatting for '{code}' is not supported with CSharpier")
            };

            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else if (error != null && error.Contains("command available", StringComparison.OrdinalIgnoreCase))
            {
                error += "\n\nMake sure CSharpier is installed. You can install it via the .NET CLI:\n\n" +
                         "Globally:\n" +
                         "  dotnet tool install -g csharpier\n\n" +
                         "Local:\n" +
                         "Open command prompt in Tools directory" +
                         " dotnet new tool-manifest\n" +
                         " dotnet tool install csharpier";
                await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {error}", MsBox.Avalonia.Enums.Icon.Error);
            }
            else
            {
                await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {error}", MsBox.Avalonia.Enums.Icon.Error);
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
                string originalCode = textEditor.Text;
                var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithPrettierAsync(originalCode, assumeFilename: filename);
                if (isSuccess)
                {
                    textEditor.Document.Text = formatted;
                }
                else
                {
                    await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {error}", MsBox.Avalonia.Enums.Icon.Error);
                }
            }
        }
    }

    private async void FormatFantomas_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "fs")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithFantomasAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
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
                await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {error}", MsBox.Avalonia.Enums.Icon.Error);
            }
            else
            {
                await MessageBoxService.Instance.OkAsync("Formatting error", $"Formatting failed:\n {error}", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
            }
        }
    }

    private async void FormatSqlfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "sql")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithSqlFmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
            }
        }
    }

    private async void FormatZigfmt_Click(object? sender, RoutedEventArgs e)
    {
        string? code = ViewModel.SelectedSnippet?.Category?.Language?.Code;
        if (code is not null and "zig")
        {
            string originalCode = textEditor.Text;
            var (isSuccess, formatted, error) = await FormattingService.TryFormatCodeWithZigFmtAsync(originalCode);
            if (isSuccess)
            {
                textEditor.Document.Text = formatted;
            }
            else
            {
                await MessageBoxService.Instance.OkAsync("Formatting error", error ?? "", MsBox.Avalonia.Enums.Icon.Error);
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

                case "sql":
                    FormatSqlfmt_Click(sender, e);
                    break;

                case "zig":
                    FormatZigfmt_Click(sender, e);
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
                string fileNameTitle = ViewModel.SelectedSnippet.Title;
                string invalidChars = new(Path.GetInvalidFileNameChars());

                foreach (char c in invalidChars) fileNameTitle = fileNameTitle.Replace(c.ToString(), "_");

                fileNameTitle = Regex.Replace(fileNameTitle, @"\s+", "_");

                var success = await FileExporter.ExportToFile(StorageProvider, ViewModel.EditorText, fileNameTitle, ViewModel.SelectedSnippet?.Category?.Language?.Code);
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