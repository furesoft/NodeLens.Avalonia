using Avalonia.Controls;
using Avalonia.Input;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// Maps the editing layer's menu description onto Avalonia's menu items.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is the whole class: a label becomes a header, a gesture becomes an input gesture, an
/// option becomes a radio check, a command becomes the click handler. Nothing here decides what an
/// entry means, which is why the menu a host shows and the entries <see cref="EditorMenu"/> builds
/// cannot disagree.
/// </para>
/// <para>
/// The callback runs after every command so a host has one place to repaint: an entry can change the
/// document, the caret or the representation, and the control knows about none of them because the
/// command ran outside it. It may be <see langword="null"/> for a host that builds a menu it never
/// opens.
/// </para>
/// </remarks>
public static class EditorMenuPresenter
{
    /// <summary>A context menu that shows <paramref name="entries"/> where the pointer is.</summary>
    /// <param name="entries">The entries to show, usually <see cref="EditorMenu.For"/>'s result.</param>
    /// <param name="afterInvoke">Runs after any entry's command, typically a repaint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The placement is the pointer rather than the caret: the user right-clicked somewhere, and a menu
    /// that jumped to the caret would move away from the hand that opened it.
    /// </remarks>
    public static ContextMenu Menu(IReadOnlyList<EditorMenuEntry> entries, Action? afterInvoke = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ContextMenu
        {
            Placement = PlacementMode.Pointer,
            ItemsSource = Items(entries, afterInvoke),
        };
    }

    /// <summary>The menu items for <paramref name="entries"/>, submenus and separators included.</summary>
    /// <param name="entries">The entries to map.</param>
    /// <param name="afterInvoke">Runs after any entry's command, typically a repaint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A separator is a menu item with an empty label in the editing layer and a <see cref="Separator"/>
    /// here, because a separator is the one entry whose meaning is exactly its toolkit representation.
    /// </remarks>
    public static IReadOnlyList<MenuItem> Items(IEnumerable<EditorMenuEntry> entries, Action? afterInvoke = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var items = new List<MenuItem>();

        foreach (var entry in entries)
        {
            if (entry is EditorMenuItem item)
            {
                items.Add(Item(item, afterInvoke));
            }
        }

        return items;
    }

    private static MenuItem Item(EditorMenuItem entry, Action? afterInvoke)
    {
        var item = new MenuItem
        {
            Header = entry.Label,
            IsEnabled = entry.IsEnabled,
            InputGesture = Gesture(entry.Gesture),
        };

        if (entry.IsOption)
        {
            // A radio check rather than a tick, because the variants of a concept exclude each other:
            // the mark says "one of these" instead of "any number of these".
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = entry.IsChecked;
        }

        if (entry.HasChildren)
        {
            item.ItemsSource = Items(entry.Children, afterInvoke);
        }

        item.Click += (_, _) =>
        {
            entry.Execute();
            afterInvoke?.Invoke();
        };

        return item;
    }

    private static KeyGesture? Gesture(string? gesture) =>
        gesture is null ? null : KeyGesture.Parse(gesture);
}
