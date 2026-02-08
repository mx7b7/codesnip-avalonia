using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

namespace CodeSnip.Services;

public sealed class MessageBoxService
{
    private static readonly Lazy<MessageBoxService> _instance = new(() => new MessageBoxService());
    public static MessageBoxService Instance => _instance.Value;

    private Window? _owner;

    private MessageBoxService() { }

    public void Register(Window window)
    {
        _owner = window;
    }

    private Window GetOwner()
    {
        if (_owner is not null)
            return _owner;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            return desktop.MainWindow;

        throw new InvalidOperationException("No owner window registered for MessageBoxService.");
    }

    private static MessageBoxStandardParams CreateParams(
        string title,
        string message,
        ButtonEnum buttons,
        Icon icon = Icon.None)
    {
        return new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = buttons,
            Icon = icon,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Topmost = true,
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInCenter = true,
            MaxWidth = 500,
            MaxHeight = 800
        };
    }

    public async Task<ButtonResult> ShowAsync(
        string title,
        string message,
        ButtonEnum buttons = ButtonEnum.Ok,
        Icon icon = Icon.None)
    {
        var owner = GetOwner();
        var p = CreateParams(title, message, buttons, icon);
        var box = MessageBoxManager.GetMessageBoxStandard(p);
        return await box.ShowWindowDialogAsync(owner);
    }

    public Task OkAsync(string title, string message, Icon icon)
        => ShowAsync(title, message, ButtonEnum.Ok, icon);


    public async Task<bool> AskYesNoAsync(string title, string message)
    {
        var result = await ShowAsync(title, message, ButtonEnum.YesNo, Icon.Question);
        return result == ButtonResult.Yes;
    }

    public async Task<ButtonResult> AskYesNoCancelAsync(string title, string message)
    {
        return await ShowAsync(title, message, ButtonEnum.YesNoCancel, Icon.Question);
    }
}
