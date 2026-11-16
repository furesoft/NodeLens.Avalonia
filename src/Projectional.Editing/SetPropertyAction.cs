using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Writes a property in one step, whatever its previous value was.</summary>
/// <remarks>
/// <para>
/// Typing replaces a value one character at a time through <see cref="TypeTextAction"/>, which is the
/// right shape for a keystroke and the wrong one for a repair: a fix knows the value it wants and must not
/// depend on the caret being anywhere in particular. This action takes a cell address and a value, so a
/// repair can name what it is fixing instead of first moving the caret there.
/// </para>
/// <para>
/// The value still goes through the cell's own type (<see cref="EditContext.SetProperty"/>), so a fix
/// cannot write something the language would refuse with a keystroke - it is refused here as a rejected
/// edit rather than accepted as a special case.
/// </para>
/// <para>
/// The caret lands on the value that was written. A repair changes text the user was looking at, and
/// leaving the caret where it happened to be would hide the result of the one action that must be
/// verifiable at a glance.
/// </para>
/// </remarks>
public sealed class SetPropertyAction : EditAction
{
    private readonly CellAddress _address;
    private readonly string _text;

    /// <summary>Writes <paramref name="text"/> into the property at <paramref name="address"/>.</summary>
    /// <param name="address">The slot holding the property, which must still be drawn when this runs.</param>
    /// <param name="text">The value to write, in the type's own spelling.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public SetPropertyAction(CellAddress address, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _address = address;
        _text = text;
    }

    /// <summary>The property this action writes.</summary>
    public CellAddress Address => _address;

    /// <summary>The value it writes.</summary>
    public string Text => _text;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Tree.Find(_address) is not PropertyCell cell)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The document draws no property at '{_address}' any more.");
        }

        // Writing the value a property already holds is not an edit: it would leave an undo step that
        // changes nothing, and a repair that turned out to be unnecessary must not look like one that ran.
        // GetOrNull, not Get: a property with neither a value nor a default is a legal state, and asking
        // for its value would throw instead of answering "nothing to compare".
        if (context.NodeOf(_address) is { } owner
            && string.Equals(owner.GetOrNull(cell.Slot), _text, StringComparison.Ordinal))
        {
            return EditResult.Unchanged(context.Selection);
        }

        if (!context.SetProperty(cell, _text))
        {
            return EditResult.Rejected(
                context.Selection,
                $"\"{_text}\" is not a valid value for '{cell.Slot}'.");
        }

        return EditResult.Applied(Selection.At(_address));
    }

    public override string ToString() => $"set {_address} = \"{_text}\"";
}
