using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using CodeSnip.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Linq;

namespace CodeSnip.Views.MainWindowView;

public interface IOverlayViewModel
{
    Action? CloseOverlay { get; set; }
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
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.Editor = _textEditor;

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.LeftOverlayContent) &&
                 vm.LeftOverlayContent is IOverlayViewModel leftOverlay)
            {
                leftOverlay.CloseOverlay = vm.CloseLeftOverlay;
            }
            if (args.PropertyName == nameof(MainWindowViewModel.RightOverlayContent) &&
                 vm.RightOverlayContent is IOverlayViewModel rightOverlay)
            {
                rightOverlay.CloseOverlay = vm.CloseRightOverlay;
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

    private async void FormatPrettier_Click(object sender, RoutedEventArgs e)
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



}