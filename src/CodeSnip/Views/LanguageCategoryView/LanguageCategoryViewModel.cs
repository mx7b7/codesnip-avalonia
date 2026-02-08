using Avalonia.Controls.Notifications;
using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodeSnip.Views.LanguageCategoryView;

public partial class LanguageCategoryViewModel : ObservableObject, IDisposable
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<Language> languages = [];

    [ObservableProperty]
    private ObservableCollection<Category> filteredCategories = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
    private Language? selectedLanguage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
    private Language? selectedLanguageForCategory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
    private Category? selectedCategory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
    private string newLanguageCode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
    private string newLanguageName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
    private string newCategoryName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveLanguageCommand))]
    private bool isAddingLanguage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCategoryCommand))]
    private bool isAddingCategory;

    public event Action? RequestClose;

    public LanguageCategoryViewModel(DatabaseService dbService)
    {
        _databaseService = dbService;
        LoadLanguages();
    }

    private static bool IsValidLanguageCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return Regex.IsMatch(code, @"^[a-zA-Z0-9]+$");
    }

    private bool CanSaveLanguage()
    {
        if (string.IsNullOrWhiteSpace(NewLanguageCode) || string.IsNullOrWhiteSpace(NewLanguageName))
            return false;

        if (!IsValidLanguageCode(NewLanguageCode))
            return false;

        bool isDuplicate = IsAddingLanguage
            ? Languages.Any(l => l.Code != null && l.Code.Equals(NewLanguageCode, StringComparison.OrdinalIgnoreCase))
            : SelectedLanguage != null && Languages.Any(l => l.Id != SelectedLanguage.Id && l.Code != null && l.Code.Equals(NewLanguageCode, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate) return false;

        if (IsAddingLanguage) return true;
        if (SelectedLanguage != null)
            return NewLanguageCode != SelectedLanguage.Code || NewLanguageName != SelectedLanguage.Name;

        return false;
    }

    private bool CanSaveCategory()
    {
        if (SelectedLanguageForCategory == null || string.IsNullOrWhiteSpace(NewCategoryName))
            return false;

        bool isDuplicate = IsAddingCategory
            ? SelectedLanguageForCategory.Categories.Any(c => c.Name.Equals(NewCategoryName, StringComparison.OrdinalIgnoreCase))
            : SelectedCategory != null && SelectedLanguageForCategory.Categories.Any(c => c.Id != SelectedCategory.Id && c.Name.Equals(NewCategoryName, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate) return false;

        if (IsAddingCategory) return true;
        if (SelectedCategory != null)
            return NewCategoryName != SelectedCategory.Name;

        return false;
    }

    private void ResortLanguages()
    {
        var currentSelection = SelectedLanguage;
        var sorted = Languages.OrderBy(l => l.Name).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];
            var oldIndex = Languages.IndexOf(item);
            if (oldIndex != i)
            {
                Languages.Move(oldIndex, i);
            }
        }
        SelectedLanguage = currentSelection;
    }

    private void LoadLanguages()
    {
        var langs = _databaseService.GetLanguagesWithCategories().OrderBy(l => l.Name).ToList();
        Languages = new ObservableCollection<Language>(langs);

        if (Languages.Any())
        {
            SelectedLanguage = Languages.First();
            SelectedLanguageForCategory = Languages.First();
        }
    }

    partial void OnSelectedLanguageForCategoryChanged(Language? value)
    {
        if (value != null)
        {
            FilteredCategories = value.Categories;
            if (FilteredCategories.Any())
            {
                SelectedCategory = FilteredCategories.First();
            }
            else
            {
                SelectedCategory = null;
                NewCategoryName = string.Empty;
            }
        }
        else
        {
            FilteredCategories = [];
            SelectedCategory = null;
        }
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value != null)
        {
            NewCategoryName = value.Name;
            IsAddingCategory = false;
        }
    }

    partial void OnSelectedLanguageChanged(Language? value)
    {
        if (value != null)
        {
            NewLanguageCode = value.Code ?? "";
            NewLanguageName = value.Name ?? "";
            SelectedLanguageForCategory = value;
        }
    }

    [RelayCommand]
    private void ToggleAddLanguage()
    {
        if (IsAddingLanguage)
        {
            // Cancel
            IsAddingLanguage = false;
            SelectedLanguage = Languages.FirstOrDefault();
        }
        else
        {
            // Enter Add Mode
            IsAddingLanguage = true;
            SelectedLanguage = null;
            NewLanguageCode = string.Empty;
            NewLanguageName = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveLanguage))]
    private async Task SaveLanguageAsync()
    {
        try
        {
            if (IsAddingLanguage)
            {
                // Check for syntax highlighting file
                if (!HighlightingService.SyntaxDefinitionExists(NewLanguageCode))
                    if (!await HandleMissingHighlightingAsync()) return;

                // INSERT
                var newLang = new Language
                {
                    Code = NewLanguageCode,
                    Name = NewLanguageName
                };

                newLang = _databaseService.SaveLanguage(newLang);
                Languages.Add(newLang);
                IsAddingLanguage = false;
                SelectedLanguage = newLang;
            }
            else if (SelectedLanguage != null)
            {
                // UPDATE
                SelectedLanguage.Code = NewLanguageCode;
                SelectedLanguage.Name = NewLanguageName;
                _databaseService.SaveLanguage(SelectedLanguage);
            }
            ResortLanguages();
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", ex.Message, Icon.Error);
        }
    }

    private void ResortCategories()
    {
        if (SelectedLanguageForCategory == null) return;

        var currentSelection = SelectedCategory;
        var sorted = SelectedLanguageForCategory.Categories.OrderBy(c => c.Name).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];
            var oldIndex = SelectedLanguageForCategory.Categories.IndexOf(item);
            if (oldIndex != i)
            {
                SelectedLanguageForCategory.Categories.Move(oldIndex, i);
            }
        }
        SelectedCategory = currentSelection;
    }

    [RelayCommand]
    private async Task ToggleAddCategoryAsync()
    {
        if (IsAddingCategory)
        {
            // Cancel
            IsAddingCategory = false;
            // Reselect the first category for the current language, if any
            SelectedCategory = FilteredCategories.FirstOrDefault();
        }
        else
        {
            // Enter Add Mode
            if (SelectedLanguageForCategory == null)
            {
                NotificationService.Instance.Show("No Language Selected", "Select a language first before adding a category.");
                return;
            }
            IsAddingCategory = true;
            SelectedCategory = null; // Deselect any category
            NewCategoryName = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveCategory))]
    private async Task SaveCategoryAsync()
    {
        try
        {
            if (IsAddingCategory)
            {
                // INSERT
                if (SelectedLanguageForCategory != null)
                {
                    var newCat = new Category
                    {
                        Name = NewCategoryName,
                        LanguageId = SelectedLanguageForCategory.Id
                    };

                    newCat = _databaseService.SaveCategory(newCat); // Save and get back with ID
                    newCat.Language = SelectedLanguageForCategory; // Set back-reference
                    SelectedLanguageForCategory.Categories.Add(newCat);
                    IsAddingCategory = false;
                    SelectedCategory = newCat; // Select the new category
                }
            }
            else if (SelectedCategory != null)
            {

                SelectedCategory.Name = NewCategoryName;
                _databaseService.SaveCategory(SelectedCategory);
            }
            ResortCategories();
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", ex.Message, Icon.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteLanguageAsync()
    {
        try
        {
            if (SelectedLanguage == null)
            {
                NotificationService.Instance.Show("Action required", "Select a language to delete.");
                return;
            }
            if (SelectedLanguage.Categories.Any())
            {
                NotificationService.Instance.Show("Error", "Cannot delete language that has categories.\nDelete them first.", NotificationType.Error);
                return;
            }

            var confirm = await MessageBoxService.Instance.AskYesNoAsync("Confirm", $"Delete language '{SelectedLanguage.Name}'?");
            if (!confirm)
                return;

            _databaseService.DeleteLanguage(SelectedLanguage.Id);
            HandleLanguageDeletion(SelectedLanguage);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", $"Failed to delete language '{SelectedLanguage?.Name}'.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        try
        {
            if (SelectedCategory == null || SelectedLanguageForCategory == null)
            {
                NotificationService.Instance.Show("Action required", "Select a category to delete.");
                return;
            }
            var confirm = await MessageBoxService.Instance.AskYesNoAsync("Confirm", $"Delete category '{SelectedCategory.Name}'?");
            if (!confirm)
                return;

            _databaseService.DeleteCategory(SelectedCategory.Id);
            HandleCategoryDeletion(SelectedCategory);

        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("FOREIGN KEY constraint failed"))
            {
                NotificationService.Instance.Show("Error", "Cannot delete category that has snippets\nDelete them first.", NotificationType.Error);
            }
            else
            {
                await MessageBoxService.Instance.OkAsync("Error", $"Failed to delete category '{SelectedCategory?.Name}'.\n\nDetails: {ex.Message}", Icon.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ForceDeleteLanguage()
    {
        if (SelectedLanguage == null)
        {
            NotificationService.Instance.Show("Action Skipped", "Select a language to delete.");
            return;
        }

        int snippetCount = _databaseService.CountSnippetsInLanguage(SelectedLanguage.Id);
        string message = $"Are you sure you want to permanently delete the language '{SelectedLanguage.Name}'?";

        if (snippetCount > 0)
        {
            message += $"\n\nThis will also delete all of its categories and {snippetCount} associated snippets. This action cannot be undone.";
        }
        else
        {
            message += "\n\nThis will also delete all of its (empty) categories. This action cannot be undone.";
        }

        var confirm = await MessageBoxService.Instance.AskYesNoAsync("Force Delete Confirmation", message);

        if (!confirm)
            return;

        try
        {
            _databaseService.ForceDeleteLanguage(SelectedLanguage.Id);
            HandleLanguageDeletion(SelectedLanguage);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", $"Failed to delete language '{SelectedLanguage?.Name}'.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    [RelayCommand]
    private async Task ForceDeleteCategory()
    {
        if (SelectedCategory == null || SelectedLanguageForCategory == null)
        {
            NotificationService.Instance.Show("Action Skipped", "Select a category to delete.");
            return;
        }

        int snippetCount = _databaseService.CountSnippetsInCategory(SelectedCategory.Id);
        string message = $"Are you sure you want to permanently delete the category '{SelectedCategory.Name}'?";

        if (snippetCount > 0)
        {
            message += $"\n\nThis will also delete {snippetCount} associated snippets. This action cannot be undone.";
        }
        else
        {
            message += "\n\nThis action cannot be undone.";
        }

        var confirm = await MessageBoxService.Instance.AskYesNoAsync("Force Delete Confirmation", message);

        if (!confirm)
            return;

        try
        {
            _databaseService.ForceDeleteCategory(SelectedCategory.Id);
            HandleCategoryDeletion(SelectedCategory);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error", $"Failed to delete category '{SelectedCategory?.Name}'.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    private void HandleLanguageDeletion(Language languageToDelete)
    {
        if (languageToDelete == null) return;

        Languages.Remove(languageToDelete);
        SelectedLanguage = Languages.FirstOrDefault();

        if (SelectedLanguage == null)
        {
            NewLanguageCode = string.Empty;
            NewLanguageName = string.Empty;
            SelectedLanguageForCategory = null;
        }
    }

    private void HandleCategoryDeletion(Category categoryToDelete)
    {
        SelectedLanguageForCategory?.Categories.Remove(categoryToDelete);
        SelectedCategory = FilteredCategories.FirstOrDefault();
        if (SelectedCategory == null)
        {
            NewCategoryName = string.Empty;
        }
    }

    private async Task GenerateXSHD(string langCode, string langName)
    {
        try
        {
            bool success = HighlightingService.GenerateBasicXshdFile(langCode, langName);
            if (!success)
            {
                NotificationService.Instance.Show("XSHD Creation Failed", $"Failed to create syntax highlighting definition for '{langCode}'.", NotificationType.Error);
                return;
            }

            NotificationService.Instance.Show("XSHD Created", $"A basic syntax highlighting definition for '{langName}' ({langCode}.xshd) has been created.\nYou can customize it later.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            await MessageBoxService.Instance.OkAsync("Error Creating XSHD", $"Failed to create syntax highlighting definition for '{langName}'.\n\nDetails: {ex.Message}", Icon.Error);
        }
    }

    /// <summary>
    /// Handles the workflow when a syntax highlighting file is missing for a new language.
    /// It shows a dialog to the user and optionally generates a template file.
    /// </summary>
    /// <returns>A task that resolves to <c>true</c> if the process should continue, or <c>false</c> if it was cancelled.</returns>
    private async Task<bool> HandleMissingHighlightingAsync()
    {
        var message = $"No syntax highlighting definition (.xshd file) was found for '{NewLanguageCode}'.\n\n" +
               $"Do you want to **generate XSHD template** for language '{NewLanguageName}'?\n\n" +
               "(Choose 'No' to add language without highlighting)";

        var confirm = await MessageBoxService.Instance.AskYesNoCancelAsync("Missing Syntax Highlighting", message);

        if (confirm == ButtonResult.Cancel)
        {
            ToggleAddLanguage();
            return false;
        }

        if (confirm == ButtonResult.Yes)
        {
            await GenerateXSHD(NewLanguageCode, NewLanguageName);
        }

        return true;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    public void Dispose()
    {
        RequestClose = null;
    }
}
