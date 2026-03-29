using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using System.Threading.Tasks;

namespace CodeSnip.Helpers
{
    public static class ImageExporter
    {
        public static async Task<RenderTargetBitmap?> ExportToImageAsync(
            string code,
            string title,
            IHighlightingDefinition syntax,
            FontFamily fontFamily,
            double fontSize,
            bool showLineNumbers)
        {
            try
            {
                // TEME COLORS
                var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                var bgColor = isDark ? Color.Parse("#1E1E1E") : Color.Parse("#FFFFFF");
                var fgColor = isDark ? Color.Parse("#D4D4D4") : Color.Parse("#333333");
                var titleBarBackground = isDark ? Color.Parse("#2D2D2D") : Color.Parse("#F3F3F3");
                var bgBrush = new SolidColorBrush(bgColor);
                var fgBrush = new SolidColorBrush(fgColor);

                // HEADER
                var headerGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(0, 0, 0, 15),
                    Background = new SolidColorBrush(titleBarBackground)
                };

                var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0, 0, 0) };
                dots.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Brush.Parse("#FF5F56") });
                dots.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Brush.Parse("#FFBD2E") });
                dots.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Brush.Parse("#27C93F") });
                Grid.SetColumn(dots, 0);

                var titleLabel = new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = fgBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleLabel, 1);

                if (string.IsNullOrEmpty(title)) titleLabel.IsVisible = false;

                var spacer = new Canvas { Width = 52 }; // Balans za točkice
                Grid.SetColumn(spacer, 2);

                headerGrid.Children.Add(dots);
                headerGrid.Children.Add(titleLabel);
                headerGrid.Children.Add(spacer);

                // VIRTUAL EDITOR
                var editor = new TextEditor
                {
                    Text = code,
                    SyntaxHighlighting = syntax,
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    IsReadOnly = true,
                    ShowLineNumbers = showLineNumbers,
                    Background = Brushes.Transparent,
                    Foreground = fgBrush,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                    Margin = new Thickness(5, 0)
                };
                editor.Options.HighlightCurrentLine = false;
                editor.Options.EnableHyperlinks = false;
                editor.Options.EnableEmailHyperlinks = false;

                // FOOTER
                var footer = new TextBlock
                {
                    Text = "created with CodeSnip",
                    FontSize = 11,
                    FontStyle = FontStyle.Italic,
                    Foreground = fgBrush,
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 5, 15, 10)
                };

                // COMPOSITION
                var mainStack = new StackPanel { Background = bgBrush };
                mainStack.Children.Add(headerGrid);
                mainStack.Children.Add(editor);
                mainStack.Children.Add(footer);

                var mainBorder = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Child = new Border
                    {
                        ClipToBounds = true,
                        CornerRadius = new CornerRadius(10),
                        Child = mainStack
                    },
                    BorderBrush = isDark ? Brush.Parse("#3E3E42") : Brush.Parse("#E0E0E0"),
                    BorderThickness = new Thickness(1)
                };


                var wrapper = new Border { Padding = new Thickness(20), Background = Brushes.Transparent, Child = mainBorder };

                // RENDER PASS
                var window = new Window
                {
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    SystemDecorations = SystemDecorations.None,
                    Background = Brushes.Transparent,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Content = wrapper
                };

                window.Show();
                window.Hide();

                wrapper.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var finalSize = wrapper.DesiredSize;
                wrapper.Width = finalSize.Width;
                wrapper.Height = finalSize.Height;
                wrapper.Arrange(new Rect(finalSize));
                wrapper.UpdateLayout();

                await Task.Delay(200); // Syntax highlighting delay

                double scale = 1.5;
                var pixelSize = new PixelSize((int)(finalSize.Width * scale), (int)(finalSize.Height * scale));
                var rtb = new RenderTargetBitmap(pixelSize, new Vector(96 * scale, 96 * scale));

                wrapper.UpdateLayout();
                rtb.Render(wrapper);

                window.Close();
                return rtb;
            }
            catch
            {
                return null;
            }
        }
    }
}