using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using System;

namespace CodeSnip.Helpers;

/// <summary>
/// Provides a background renderer that highlights the current line in a text editor with a customizable background
/// color and optional border.
/// </summary>
/// <remarks>This class is intended for use with a TextEditor control and implements the IBackgroundRenderer
/// interface. The current line is highlighted only when the editor's HighlightCurrentLine option is enabled. Custom
/// brushes can be provided for the background and border to match the desired appearance. This renderer operates on the
/// background layer and does not affect text content or selection.</remarks>
public class CurrentLineHighlighter : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private IBrush? _backgroundBrush;
    private IPen _borderPen;

    /// <summary>
    /// Initializes a new instance of the CurrentLineHighlighter class, which highlights the current line in the
    /// specified text editor.
    /// </summary>
    /// <remarks>The border color defaults to a semi-transparent color if the borderBrush parameter is not
    /// specified.</remarks>
    /// <param name="editor">The text editor instance in which the current line will be highlighted. This parameter cannot be null.</param>
    /// <param name="backgroundBrush">An optional brush used to fill the background of the highlighted line. If not provided, a default brush will be
    /// used.</param>
    /// <param name="borderBrush">An optional brush used to define the border color of the highlighted line. If not provided, a default border
    /// color will be applied.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="editor"/> parameter is null.</exception>
    public CurrentLineHighlighter(TextEditor editor, IBrush? backgroundBrush = null, IBrush? borderBrush = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));

        _backgroundBrush = backgroundBrush;

        var borderColor = borderBrush != null
            ? ((ISolidColorBrush)borderBrush).Color
            : Color.FromArgb(120, 100, 100, 130);

        _borderPen = new ImmutablePen(new ImmutableSolidColorBrush(borderColor), 1.0);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_editor.Document == null || !_editor.Options.HighlightCurrentLine)
            return;

        textView.EnsureVisualLines();

        var currentLine = _editor.Document.GetLineByNumber(_editor.TextArea.Caret.Line);

        var builder = new BackgroundGeometryBuilder();

        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, currentLine))
        {
            var rectFull = new Rect(new Point(0, rect.Top), new Size(textView.Bounds.Width, rect.Height));
            builder.AddRectangle(textView, rectFull);
        }

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(_backgroundBrush, _borderPen, geometry);
        }
    }
}