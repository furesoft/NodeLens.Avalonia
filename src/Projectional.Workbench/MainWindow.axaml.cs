using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Samples;

namespace Projectional.Workbench;

/// <summary>
/// The workbench window: a menu, a document tab strip and a status bar around the editor.
/// </summary>
/// <remarks>
/// <para>
/// The window is wiring and nothing else. It owns the window-level decisions a control cannot make -
/// which documents are open, which one is in front, what the status bar says, when a session is
/// disposed - and every one of them is expressed through the public API a host would use. There is no
/// reach into the projection or editing layers here, which is the point of shipping an app at all:
/// anything this window cannot do is a gap in the public surface rather than a private convenience.
/// </para>
/// <para>
/// A session belongs to a document, so closing a document disposes it and closing the window disposes
/// whatever is still open. That rule is the one hosts get wrong most often, so the shell demonstrates
/// it rather than describing it.
/// </para>
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly ProjectionalEditorControl _editor = new();

    private SyntaxTheme _theme = SyntaxTheme.Light;
    private EditorSession? _session;

    /// <summary>Creates the window and opens the sample document in it.</summary>
    public MainWindow()
    {
        InitializeComponent();

        ApplyTheme(_theme);
        OpenSample();

        Opened += (_, _) => _editor.Focus();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        // The session, not the control, owns the undo history, so the window is what closes it.
        _session?.Dispose();
        _session = null;

        base.OnClosed(e);
    }

    private void OpenSample()
    {
        var model = PayrollSample.NewSampleModel();

        OpenDocument(
            model.Name ?? "Sample",
            model.Roots[0],
            RulesLanguage.CreateNotations(model.Concepts));
    }

    private void OpenDocument(string title, Node root, NotationRegistry notations)
    {
        _session = _editor.OpenSession(root, notations);

        Documents.Items.Add(new TabItem { Header = title, Content = _editor });
        Documents.SelectedIndex = Documents.ItemCount - 1;

        UpdateStatus();
    }

    private void ApplyTheme(SyntaxTheme theme)
    {
        _theme = theme;

        // The window chrome follows the document rather than the operating system, because the shell
        // offers a theme switch: asking for a dark palette and getting a white tab strip would be a
        // bug the user can see.
        RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = theme.Chrome;

        // Four properties rather than one, because the control refuses to guess what surface its
        // palette is meant for: a dark palette is only readable on a dark background.
        _editor.Palette = theme.Palette;
        _editor.Background = theme.Background;
        _editor.CaretBrush = theme.Caret;
        _editor.HighlightBrush = theme.Highlight;
    }

    private void UpdateStatus() =>
        StatusText.Text = _session is null
            ? "No document open"
            : $"type to edit | Ctrl+Z undoes | {(_theme.IsDark ? "dark" : "light")} theme";

    private void OnLightTheme(object? sender, RoutedEventArgs e)
    {
        ApplyTheme(SyntaxTheme.Light);
        UpdateStatus();
    }

    private void OnDarkTheme(object? sender, RoutedEventArgs e)
    {
        ApplyTheme(SyntaxTheme.Dark);
        UpdateStatus();
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();
}
