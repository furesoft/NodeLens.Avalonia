namespace Projectional.Editing;

/// <summary>
/// A clipboard that keeps its text in the session rather than in the operating system.
/// </summary>
/// <remarks>
/// <para>
/// This is the default an <see cref="EditorSession"/> gets, so a headless run - a test, a rendering probe,
/// a host that embeds the editor in a process with no window - copies and pastes exactly the way a
/// desktop application does. Nothing about the editing layer needs a platform clipboard to work; the
/// platform is what a host adds.
/// </para>
/// <para>
/// Two sessions that both fall back to this share nothing, and that is intended: the clipboard of one
/// document is not the clipboard of another until a host says so by handing both sessions the same
/// adapter.
/// </para>
/// </remarks>
public sealed class InMemoryClipboard : IEditorClipboard
{
    private string? _text;
    private ModelFragment? _fragment;

    /// <inheritdoc />
    /// <remarks>Writing text clears the fragment: a clipboard holds one payload at a time.</remarks>
    public string? Text
    {
        get => _text;

        set
        {
            _text = value;
            _fragment = null;
        }
    }

    /// <inheritdoc />
    /// <remarks>Writing a fragment clears the text, for the same reason.</remarks>
    public ModelFragment? Fragment
    {
        get => _fragment;

        set
        {
            _fragment = value;
            _text = null;
        }
    }
}
