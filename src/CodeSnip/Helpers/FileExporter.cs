using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CodeSnip.Helpers;

public static class FileExporter
{
    public static async Task<bool> ExportToFile(IStorageProvider storageProvider, string text, string defaultFileName, string? defaultExtension = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(defaultFileName))
            throw new ArgumentException("File name cannot be empty.", nameof(defaultFileName));

        var fileTypes = new List<FilePickerFileType>();

        if (!string.IsNullOrWhiteSpace(defaultExtension))
        {
            fileTypes.Add(new FilePickerFileType($"{defaultExtension.ToUpper()} Files")
            {
                Patterns = [$"*.{defaultExtension}"]
            });
        }

        fileTypes.Add(FilePickerFileTypes.All);

        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
            FileTypeChoices = fileTypes,
            ShowOverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(defaultExtension))
        {
            options.SuggestedFileType = fileTypes[0];
            options.DefaultExtension = defaultExtension;
        }

        var file = await storageProvider.SaveFilePickerAsync(options);

        if (file is null)
            return false;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

}