using CodeSnip.Services;
using CodeSnip.Views.MainWindowView;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Avalonia.Controls.Notifications;
using MsBox.Avalonia.Enums;

namespace CodeSnip.Views.CompilerSettingsView;

public partial class CompilerSettingsViewModel : ObservableValidator, IOverlayViewModel
{
    public Func<Task>? CloseOverlayAsync { get; set; }
    private readonly CompilerSettingsService _manager;

    [ObservableProperty]
    private ObservableCollection<LanguageInfo> _languages = [];

    [ObservableProperty]
    private ObservableCollection<CompilerInfo> _compilers = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private LanguageInfo? _selectedLanguage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private CompilerInfo? _selectedCompiler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private bool _isAddingLanguage;

    [ObservableProperty]
    private bool _canSetDefaultCompiler;

    [ObservableProperty]
    private string _compilersLink = string.Empty;

    [ObservableProperty]
    private string _linkText = string.Empty;

    [ObservableProperty]
    private string _compilerLocalId = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Compiler ID is required")]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private string _compilerId = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Compiler Name is required")]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private string _compilerName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCompilerCommand))]
    private string _compilerFlags = string.Empty;

    [ObservableProperty]
    private string _helpText = "";

    public string? HeaderText { get; private set; }

    private bool CanSave()
    {
        if (SelectedLanguage == null) return false;
        if (IsAddingLanguage)
        {
            return !HasErrors &&
                   !string.IsNullOrWhiteSpace(CompilerId) &&
                   !string.IsNullOrWhiteSpace(CompilerName) &&
                   !Compilers.Any(c => string.Equals(c.Id, CompilerId, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return !HasErrors &&
                   !string.IsNullOrWhiteSpace(CompilerId) &&
                   !string.IsNullOrWhiteSpace(CompilerName) &&
                   SelectedCompiler != null &&
                   (!string.Equals(CompilerId, SelectedCompiler.Id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(CompilerName, SelectedCompiler.Name, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(CompilerFlags, SelectedCompiler.Flags, StringComparison.OrdinalIgnoreCase));
        }
    }

    public CompilerSettingsViewModel()
    {
        _manager = new CompilerSettingsService();
        LoadLanguages();
        HeaderText = "Compiler Settings";
        ValidateAllProperties();
    }

    private void LoadLanguages()
    {
        Languages = new ObservableCollection<LanguageInfo>(_manager.Settings!.Languages!);
        if (Languages.Any())
        {
            SelectedLanguage = Languages.FirstOrDefault();
        }
    }

    partial void OnSelectedLanguageChanged(LanguageInfo? value)
    {
        if (value != null)
        {
            Compilers = new ObservableCollection<CompilerInfo>(
                _manager.GetCompilersForLanguage(value.LanguageId ?? "")
            );
            if (Compilers.Count > 0)
            {
                SelectedCompiler = _manager.GetDefaultCompiler(value); // Compilers.First();
            }

            CompilersLink = $"https://godbolt.org/api/compilers/{value.LanguageId}";
            LinkText = $"Get more compilers for {value.LanguageName}";
            HelpText = $"Enter a CompilerId that matches a 'Compiler Name' from the list of Compiler Explorer compilers for {value.LanguageName}";
        }
        else
        {
            Compilers.Clear();
            SelectedCompiler = null;
        }
    }

    partial void OnSelectedCompilerChanged(CompilerInfo? value)
    {
        if (value != null)
        {
            CompilerLocalId = value.LocalId ?? "";
            CompilerId = value.Id ?? "";
            CompilerName = value.Name ?? "";
            CompilerFlags = value.Flags ?? "";
            CanSetDefaultCompiler = SelectedLanguage != null &&
                           !string.Equals(value.Id, SelectedLanguage.DefaultCompilerId, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            ClearCompilerFields();
            CanSetDefaultCompiler = false;
        }
    }

    private void ClearCompilerFields()
    {
        CompilerLocalId = string.Empty;
        CompilerId = string.Empty;
        CompilerName = string.Empty;
        CompilerFlags = string.Empty;
    }

    partial void OnCompilerIdChanged(string value) => ValidateProperty(value, nameof(CompilerId));

    partial void OnCompilerNameChanged(string value) => ValidateProperty(value, nameof(CompilerName));

    [RelayCommand]
    private void ToggleAddCompiler()
    {
        if (IsAddingLanguage)
        {
            // Cancel
            IsAddingLanguage = false;
            ClearErrors();
            if (Compilers.Count > 0)
            {
                SelectedCompiler = null; // otherwise it will not trigger OnSelectedCompilerChanged if there is one compiler
                SelectedCompiler = Compilers.FirstOrDefault();
            }
            else
            {
                SelectedCompiler = null;
                ClearCompilerFields();
            }
        }
        else
        {
            // Enter Add Mode
            IsAddingLanguage = true;
            ClearCompilerFields();
        }
        ValidateAllProperties();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void SaveCompiler()
    {
        if (SelectedLanguage == null) return;

        var compiler = new CompilerInfo
        {
            LocalId = CompilerLocalId,
            Id = CompilerId,
            Name = CompilerName,
            Flags = CompilerFlags
        };

        _ = _manager.UpsertCompiler(SelectedLanguage.LanguageId!, compiler);

        if (IsAddingLanguage)
        {
            if (!Compilers.Any(c => c.Id == compiler.Id))
                Compilers.Add(compiler);

            if (Compilers.Count == 1)
                _manager.SetDefaultCompiler(SelectedLanguage, compiler.Id);

            SelectedCompiler = compiler;
            IsAddingLanguage = false;
        }
        else
        {
            var idx = Compilers.IndexOf(SelectedCompiler!);
            if (idx >= 0)
            {
                Compilers[idx] = compiler;
                SelectedCompiler = compiler;
            }
        }

        ClearErrors();
    }

    [RelayCommand]
    private async Task DeleteCompiler()
    {
        if (SelectedLanguage == null || SelectedCompiler == null)
        {
            NotificationService.Instance.Show("Error", "Please select a language and compiler to delete.");
            return;
        }

        string deletedId = SelectedCompiler.Id ?? "";
        bool wasDefault = string.Equals(deletedId, SelectedLanguage.DefaultCompilerId, StringComparison.OrdinalIgnoreCase);

        try
        {
            if (_manager.RemoveCompiler(SelectedLanguage.LanguageId ?? "", deletedId))
            {
                Compilers.Remove(SelectedCompiler);

                if (Compilers.Count > 0)
                {
                    var newSelected = Compilers.First();

                    if (wasDefault)
                    {
                        _manager.SetDefaultCompiler(SelectedLanguage, newSelected.Id ?? "");
                    }

                    SelectedCompiler = newSelected;
                }
                else
                {
                    SelectedCompiler = null;
                    ClearCompilerFields();
                }
                NotificationService.Instance.Show("Success", "Compiler deleted successfully.");
            }
            else
            {
                NotificationService.Instance.Show("Error", "Failed to delete compiler.", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {

            await MessageBoxService.Instance.OkAsync("Error", $"An error occurred while deleting the compiler: {ex.Message}", Icon.Error);
        }
    }

    [RelayCommand]
    private void SetDefault()
    {
        if (SelectedLanguage != null && SelectedCompiler != null)
        {
            _manager.SetDefaultCompiler(SelectedLanguage, SelectedCompiler.Id);
            CanSetDefaultCompiler = false;
        }
    }

    [RelayCommand]
    private void OpenCompilersLink()
    {
        if (!string.IsNullOrEmpty(CompilersLink))
            Process.Start(new ProcessStartInfo(CompilersLink) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (CloseOverlayAsync != null)
            await CloseOverlayAsync();
    }
}
