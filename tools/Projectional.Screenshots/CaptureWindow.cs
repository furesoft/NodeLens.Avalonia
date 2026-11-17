using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

using Projectional.Avalonia;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples;

namespace Projectional.Screenshots;

/// <summary>Hosts the editor, drives each shot into its state and writes the image.</summary>
/// <remarks>
/// A real window is used because the control needs a rendering platform: glyph shaping, the completion
/// icons and the tooltips all come from it, and a picture rendered without one would not be the picture
/// a user sees. The window is hidden from the task bar and closes as soon as the set is written, so the
/// tool runs unattended.
/// </remarks>
internal sealed class CaptureWindow : Window
{
    private readonly string _output;

    public CaptureWindow(string output)
    {
        _output = output;

        Title = "Projectional screenshots";
        ShowInTaskbar = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(0, 0);
    }

    /// <inheritdoc />
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var log = new List<string>();

        try
        {
            foreach (var shot in Shots.All)
            {
                var size = await CaptureAsync(shot, log);

                log.Add($"{shot.FileName}.png {size.Width}x{size.Height}");
            }

            // After the editor shots, because each shell shot opens and closes a window of its own.
            Environment.ExitCode = await ShellCapture.RunAsync(_output, log) ? 0 : 1;
        }
        catch (Exception exception)
        {
            log.Add(exception.ToString());
            Environment.ExitCode = 1;
        }
        finally
        {
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "projectional-screenshots.log"), log);
            Close();
        }
    }

    /// <summary>Takes one shot and answers the size of the image it wrote.</summary>
    private async Task<PixelSize> CaptureAsync(Shot shot, List<string> log)
    {
        var theme = shot.Dark ? SyntaxTheme.Dark : SyntaxTheme.Light;

        var editor = new ProjectionalEditorControl();

        // Size before the session: the session lays out with the control's measure, so a font applied
        // afterwards would describe a layout that is already computed.
        editor.FontSize = shot.FontSize;
        ApplyTheme(editor, theme);
        editor.ShowLineNumbers = shot.LineNumbers;

        var model = shot.Create();
        using var session = new EditorSession(
            model.Roots[0],
            Shots.Notations,
            editor.TextMeasure,
            RulesLanguage.Emphasis,
            typing: RulesLanguage.TypingRules);

        editor.Session = session;
        Content = editor;

        await SettleAsync();

        // Layout-changing states first, so the size below is the size of what will be shown: a table is
        // wider than the block it replaces, and a folded rule is shorter than an open one.
        shot.Prepare?.Invoke(editor, session);
        editor.InvalidateMeasure();
        editor.InvalidateVisual();
        await SettleAsync();

        // The window is made large enough for the document plus whatever a list or a tooltip needs; the
        // crop at the end removes the room that stayed page. The slack keeps the measure pass from
        // wrapping a line that grows by a keystroke after this point.
        editor.Width = Math.Ceiling(session.Layout.Size.Width + editor.GutterWidth) + shot.ExtraWidth + 80;
        editor.Height = Math.Ceiling(session.Layout.Size.Height) + shot.ExtraHeight + 40;

        await SettleAsync();

        // Only now, because publishing the wrap width re-lays-out the document and closes the lists. A
        // state opened before that would be thrown away by a measure pass the shot never asked for.
        shot.Arrange?.Invoke(editor, session);

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
        await SettleAsync();

        var bitmap = ImageWriter.Render(editor);

        if (shot.Hover != HoverKind.None)
        {
            var point = shot.Hover == HoverKind.Marker
                ? MarkerPoint(editor, session)
                : TooltipPoint(editor, session, shot.HoverAddress?.Invoke(session));

            if (point is { } hover)
            {
                RaisePointerMove(editor, hover);
                await Task.Delay(shot.HoverDelayMs);
                editor.InvalidateVisual();
                await SettleAsync();

                bitmap.Dispose();
                bitmap = ImageWriter.Render(editor);
            }
            else
            {
                log.Add($"{shot.FileName}: no hover target found");
            }
        }

        using (bitmap)
        {
            var path = Path.Combine(_output, $"{shot.FileName}.png");

            return ImageWriter.Save(
                bitmap,
                path,
                ColourOf(theme.Background),
                ColourOf(theme.Gutter),
                Color.FromRgb(0xC8, 0xC8, 0xC8));
        }
    }

    /// <summary>Applies the theme the way a host does: one member per surface the control paints.</summary>
    private static void ApplyTheme(ProjectionalEditorControl editor, SyntaxTheme theme)
    {
        editor.Palette = theme.Palette;
        editor.Background = theme.Background;
        editor.CaretBrush = theme.Caret;
        editor.HighlightBrush = theme.Highlight;
        editor.GutterBackground = theme.Gutter;
        editor.LineNumberBrush = theme.LineNumber;
        editor.MarkerColors = theme.Markers;
        editor.ListPalette = theme.Lists;
        editor.TipPalette = theme.Tip;
        editor.BulbBrush = theme.Bulb;
    }

    /// <summary>The middle of the bottom strip of the first squiggle, in control coordinates.</summary>
    /// <remarks>
    /// The control opens a marker tooltip only for a pointer inside the band the wave is stroked in, which
    /// is the bottom of the line. Asking the marker geometry where that strip is - instead of looking for
    /// the severity's colour in the rendered pixels - keeps the point on the wave even when the same
    /// colour appears elsewhere in the document.
    /// </remarks>
    private static Point? MarkerPoint(ProjectionalEditorControl editor, EditorSession session)
    {
        var lines = MarkerGeometry.Of(session.Layout, session.Markers, session.Measure);

        if (lines.Count == 0)
        {
            return null;
        }

        var line = lines[0];

        return new Point(
            line.X + (line.Width / 2) + editor.GutterWidth - editor.OffsetX,
            line.Y + line.Height - 1 - editor.OffsetY);
    }

    /// <summary>The middle of the cell a tooltip is asked about, in control coordinates.</summary>
    private static Point? TooltipPoint(ProjectionalEditorControl editor, EditorSession session, CellAddress? address)
    {
        if (address is not { } cell || session.Layout.BoundsOf(cell) is not { } bounds)
        {
            return null;
        }

        return new Point(bounds.X + editor.GutterWidth + 2, bounds.Y + (bounds.Height / 2));
    }

    /// <summary>Moves the pointer onto <paramref name="point"/>, which is how a tooltip is asked for.</summary>
    /// <remarks>
    /// The control is passed as the root visual, so the position the event carries is the position the
    /// control reads back - there is no window offset to get wrong.
    /// </remarks>
    private static void RaisePointerMove(ProjectionalEditorControl editor, Point point)
    {
        var args = new PointerEventArgs(
            InputElement.PointerMovedEvent,
            editor,
            new Pointer(1, PointerType.Mouse, isPrimary: true),
            editor,
            point,
            timestamp: 0,
            new PointerPointProperties(),
            KeyModifiers.None);

        editor.RaiseEvent(args);
    }

    /// <summary>Lets layout, render and any dispatcher work catch up before the next read.</summary>
    private static async Task SettleAsync()
    {
        await Task.Delay(40);

        Dispatcher.UIThread.RunJobs();

        await Task.Delay(40);
    }

    /// <summary>The solid colour of a brush the theme supplied.</summary>
    private static Color ColourOf(IBrush brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
}
