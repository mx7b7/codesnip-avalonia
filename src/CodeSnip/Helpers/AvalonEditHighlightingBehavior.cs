using Avalonia;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using CodeSnip.Services;

namespace CodeSnip.Helpers
{
    public class AvalonEditHighlightingBehavior : Behavior<TextEditor>
    {
        public static readonly AvaloniaProperty HighlightingNameProperty =
            AvaloniaProperty.Register<AvalonEditHighlightingBehavior, string>(
                nameof(HighlightingName)
                );

        public string? HighlightingName
        {
            get => (string?)GetValue(HighlightingNameProperty);
            set => SetValue(HighlightingNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            ApplyHighlighting();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject?.SyntaxHighlighting = null; // Clear highlighting on detach
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HighlightingNameProperty)
            {
                ApplyHighlighting();
            }
        }

        private void ApplyHighlighting()
        {
            if (AssociatedObject != null && !string.IsNullOrEmpty(HighlightingName))
            {
                HighlightingService.ApplyHighlighting(AssociatedObject, HighlightingName);
            }
            else
            {
                AssociatedObject?.SyntaxHighlighting = null;
            }
        }
    }
}
