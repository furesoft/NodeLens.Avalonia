using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Writes a property of a node the caller names, whether or not the notation draws it.</summary>
/// <remarks>
/// <para>
/// <see cref="SetPropertyAction"/> addresses a <em>cell</em>, which is right for a repair of text the user can see
/// and wrong for a value the drawing hides. A property can be invisible three ways: the notation puts it behind a
/// conditional, the node is drawn collapsed, or the concept's notation simply leaves the slot out. In all three the
/// value still exists and a language still has an opinion about it, so this action addresses the node and the slot
/// instead of a place on screen.
/// </para>
/// <para>
/// The value goes through <see cref="EditContext.SetProperty(Node, string, string)"/>, so it is refused by the same
/// check a keystroke would hit. A language whose flag is a truth value therefore cannot be handed <c>"yes"</c> by an
/// intention and get a broken model out of it.
/// </para>
/// <para>
/// The caret stays where it was. Nothing was typed and no text the user was looking at changed meaning - the
/// drawing may even gain a word at the start of the line - so an action that also moved the caret would be moving
/// it away from the thing the user just changed.
/// </para>
/// </remarks>
public sealed class SetNodePropertyAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _slot;
    private readonly string _text;

    /// <summary>Writes <paramref name="text"/> into <paramref name="slot"/> of node <paramref name="node"/>.</summary>
    /// <param name="node">The node that owns the property.</param>
    /// <param name="slot">The property slot to write, which must belong to the node's concept.</param>
    /// <param name="text">The value to write, in the type's own spelling.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public SetNodePropertyAction(NodeId node, string slot, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(text);

        _node = node;
        _slot = slot;
        _text = text;
    }

    /// <summary>The node whose property is written.</summary>
    public NodeId Node => _node;

    /// <summary>The property slot that is written.</summary>
    public string Slot => _slot;

    /// <summary>The value it writes.</summary>
    public string Text => _text;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The document no longer holds the node '{_node}' this writes to.");
        }

        // The declaration comes first, and not only for the message: asking a node for the value of a slot that is
        // not a property throws, so "has it already got this value" is a question that can only be asked once the
        // slot is known to be a property.
        if (node.Concept.FindProperty(_slot) is null)
        {
            return EditResult.Rejected(
                context.Selection,
                $"'{node.Concept.Key}' declares no property named '{_slot}'.");
        }

        // Writing the value a property already holds is not an edit: an undo step that changes nothing would make
        // the history claim a change the model never had.
        if (string.Equals(node.GetOrNull(_slot), _text, StringComparison.Ordinal))
        {
            return EditResult.Unchanged(context.Selection);
        }

        if (!context.SetProperty(node, _slot, _text))
        {
            return EditResult.Rejected(
                context.Selection,
                $"\"{_text}\" is not a valid value for '{_slot}'.");
        }

        return EditResult.Applied(context.Selection);
    }

    public override string ToString() => $"set {_node}.{_slot} = \"{_text}\"";
}
