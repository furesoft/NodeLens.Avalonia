namespace Projectional.Editing;

/// <summary>
/// One line of the editor's context menu, which is either something to run or a rule between two such
/// things.
/// </summary>
/// <remarks>
/// The menu is described rather than drawn. A host builds a widget from these entries and never asks
/// the editing layer what belongs in a menu, so what the menu offers, in which order, and why an item
/// is greyed out is decided once - in <see cref="EditorMenu"/> - and can be tested without a window.
/// It is also what keeps the layer free of a UI toolkit: an Avalonia menu, a console prompt and a
/// headless test all read the same list.
/// </remarks>
public abstract class EditorMenuEntry
{
    /// <summary>Creates an entry with the text a menu shows for it.</summary>
    /// <param name="label">What the entry says; a separator's label is empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    protected EditorMenuEntry(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        Label = label;
    }

    /// <summary>What the entry says.</summary>
    public string Label { get; }

    public override string ToString() => Label;
}
