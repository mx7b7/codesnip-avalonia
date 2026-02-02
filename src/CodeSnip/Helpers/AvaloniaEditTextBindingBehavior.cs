using Avalonia;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using System;

namespace CodeSnip.Helpers;

public class AvaloniaEditTextBindingBehavior : Behavior<TextEditor>
{
    public static readonly AvaloniaProperty<string?> BoundTextProperty =
        AvaloniaProperty.Register<AvaloniaEditTextBindingBehavior, string?>(
            nameof(BoundText),
            default(string?),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? BoundText
    {
        get => (string?)GetValue(BoundTextProperty);
        set => SetValue(BoundTextProperty, value);
    }

    private bool _isUpdating;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.Document.TextChanged += Document_TextChanged;

            if (BoundText != null)
                AssociatedObject.Document.Text = BoundText;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Document.TextChanged -= Document_TextChanged;
        }

        base.OnDetaching();
    }

    private void Document_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || AssociatedObject == null)
            return;

        _isUpdating = true;
        BoundText = AssociatedObject.Document.Text;
        _isUpdating = false;
    }

    static AvaloniaEditTextBindingBehavior()
    {
        BoundTextProperty.Changed.AddClassHandler<AvaloniaEditTextBindingBehavior>((x, e) => x.OnBoundTextChanged(e));
    }

    private void OnBoundTextChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_isUpdating || AssociatedObject == null)
            return;

        _isUpdating = true;
        AssociatedObject.Document.Text = e.NewValue as string ?? string.Empty;
        _isUpdating = false;
    }
}
