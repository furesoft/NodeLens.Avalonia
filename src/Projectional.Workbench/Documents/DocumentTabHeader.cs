using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>
/// One tab's header: what the document is called, whether it has unsaved changes, and a button that closes it.
/// </summary>
/// <remarks>
/// <para>
/// The close button lives in the header because the gesture alone (Ctrl+W) is not a control: a tab a user
/// cannot click shut is a tab they have to guess about. It is a real button with a target a pointer can hit
/// rather than a bare glyph, and it says which document it closes, so a screen reader can offer it as one.
/// </para>
/// <para>
/// Built in code rather than from a template because the tab strip is built in code: one header per open
/// document, each closing over the document it belongs to.
/// </para>
/// </remarks>
internal sealed class DocumentTabHeader : StackPanel
{
    /// <summary>The cross the window's styles name for every other close button.</summary>
    /// <remarks>
    /// A header is created before it has a window, so it cannot look the resource up. The strings are short
    /// enough to repeat here, and reaching for <c>Application.Current</c> instead would tie a host's shell to
    /// the application object the app happens to run in.
    /// </remarks>
    private const string CloseCross = "M3,3 L11,11 M11,3 L3,11";

    /// <summary>The page glyph, for the same reason and with the same limitation as the cross.</summary>
    private const string DocumentPage = "M3,1.5 H9.5 L12.5,4.5 V14.5 H3 Z M9.5,1.5 V4.5 H12.5";

    private readonly TextBlock _title;

    /// <summary>Creates the header of the document called <paramref name="title"/>.</summary>
    /// <param name="title">What the tab shows.</param>
    /// <param name="close">What pressing the close button does.</param>
    /// <exception cref="ArgumentNullException"><paramref name="close"/> is <see langword="null"/>.</exception>
    internal DocumentTabHeader(string title, Action close)
    {
        ArgumentNullException.ThrowIfNull(close);

        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;

        _title = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        Children.Add(Glyph(DocumentPage, 11));
        Children.Add(_title);
        Children.Add(CloseButton(title, close));

        SetTitle(title, isDirty: false);
    }

    /// <summary>Shows <paramref name="title"/>, marked with a star while the document has unsaved changes.</summary>
    /// <param name="title">What the document is called.</param>
    /// <param name="isDirty">Whether the model has changed since it was last written.</param>
    internal void SetTitle(string title, bool isDirty)
    {
        ArgumentNullException.ThrowIfNull(title);

        _title.Text = isDirty ? $"{title} *" : title;

        // The star says "modified" to a reader who can see it; a screen reader needs it said.
        AutomationProperties.SetName(this, isDirty ? $"{title}, modified" : title);
    }

    private static Button CloseButton(string title, Action close)
    {
        var cross = Glyph(CloseCross, 8);
        cross.StrokeThickness = 1.4;

        var button = new Button
        {
            Content = cross,
            Classes = { "tabClose" },
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };

        AutomationProperties.SetName(button, $"Close {title}");
        ToolTip.SetTip(button, $"Close {title} (Ctrl+W)");

        button.Click += (_, _) => close();

        return button;
    }

    /// <summary>One stroked glyph at <paramref name="size"/> pixels, in the grey the tab strip reads on both themes.</summary>
    private static global::Avalonia.Controls.Shapes.Path Glyph(string data, double size) =>
        new()
        {
            Data = Geometry.Parse(data),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.3,
            StrokeLineCap = PenLineCap.Round,
            Stroke = new SolidColorBrush(Color.FromRgb(0x7A, 0x7A, 0x7A)),
            VerticalAlignment = VerticalAlignment.Center,
        };
}
