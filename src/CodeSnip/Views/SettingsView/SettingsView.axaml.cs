using Avalonia.Controls;

namespace CodeSnip.Views.SettingsView;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void NumericUpDown_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        e.Handled = true;
    }
}