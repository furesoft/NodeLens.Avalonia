namespace Projectional.Editing;

/// <summary>
/// A menu entry the user can pick: something to run, or a submenu holding more of them.
/// </summary>
/// <remarks>
/// <para>
/// An entry knows three things a menu needs and nothing about the widget that shows them: what it
/// says, whether it can be picked right now, and what picking it does. The gesture is part of the
/// model rather than of the host because the menu has to agree with the keyboard: an entry that says
/// Ctrl+Z and a key map that says something else are two different promises, and keeping both strings
/// in one layer is what makes that visible.
/// </para>
/// <para>
/// <see cref="IsEnabled"/> is decided when the menu is built, not when an item is picked. That is the
/// honest order for a projectional editor: what can be done is a fact about the caret, and a menu that
/// offered Undo with nothing to undo would have to explain itself only after the click.
/// </para>
/// </remarks>
public sealed class EditorMenuItem : EditorMenuEntry
{
    private static readonly IReadOnlyList<EditorMenuEntry> NoChildren = [];

    private readonly Action? _command;

    private EditorMenuItem(
        string label,
        Action? command,
        string? gesture,
        bool isEnabled,
        bool isOption,
        bool isChecked,
        IReadOnlyList<EditorMenuEntry> children)
        : base(label)
    {
        _command = command;
        Gesture = gesture;
        IsEnabled = isEnabled;
        IsOption = isOption;
        IsChecked = isChecked;
        Children = children;
    }

    /// <summary>The keys that do the same thing, shown beside the label, or <see langword="null"/> when there are none.</summary>
    public string? Gesture { get; }

    /// <summary>Whether picking the entry would do anything in the state the menu was built in.</summary>
    public bool IsEnabled { get; }

    /// <summary>Whether the entry is one of a set of alternatives, so a menu shows it as a radio button.</summary>
    public bool IsOption { get; }

    /// <summary>Whether an option is the one currently in force; always <see langword="false"/> for the others.</summary>
    public bool IsChecked { get; }

    /// <summary>The entries of a submenu, empty for an entry that runs something.</summary>
    public IReadOnlyList<EditorMenuEntry> Children { get; }

    /// <summary>Whether the entry opens a submenu.</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>An entry that runs <paramref name="command"/>.</summary>
    /// <param name="label">What the entry says.</param>
    /// <param name="command">What picking it does.</param>
    /// <param name="gesture">The keys that do the same thing, when there are any.</param>
    /// <param name="isEnabled">Whether it can be picked in the state the menu describes.</param>
    public static EditorMenuItem Run(
        string label,
        Action command,
        string? gesture = null,
        bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new EditorMenuItem(label, command, gesture, isEnabled, false, false, NoChildren);
    }

    /// <summary>An entry that opens <paramref name="children"/>.</summary>
    /// <param name="label">What the entry says.</param>
    /// <param name="children">The entries of the submenu.</param>
    public static EditorMenuItem Submenu(string label, IReadOnlyList<EditorMenuEntry> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        return new EditorMenuItem(label, null, null, children.Count > 0, false, false, children);
    }

    /// <summary>One of a set of alternatives, shown with the one in force marked.</summary>
    /// <param name="label">What the entry says.</param>
    /// <param name="isChecked">Whether this is the alternative currently in force.</param>
    /// <param name="command">What picking it does.</param>
    public static EditorMenuItem Option(string label, bool isChecked, Action command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new EditorMenuItem(label, command, null, !isChecked, true, isChecked, NoChildren);
    }

    /// <summary>Does what the entry says.</summary>
    /// <remarks>
    /// A disabled entry does nothing rather than throwing, because a host may well run the command
    /// behind a menu item that its toolkit failed to grey out. Picking an option that is already in
    /// force does nothing for the same reason: choosing what is already chosen would rebuild the tree
    /// to reach the same projection and move the caret for no reason.
    /// </remarks>
    public void Execute()
    {
        if (IsEnabled)
        {
            _command?.Invoke();
        }
    }
}
