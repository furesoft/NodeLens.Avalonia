namespace Projectional.Editing;

/// <summary>
/// Starts a new line inside the text of the property the caret is in.
/// </summary>
/// <remarks>
/// <para>
/// Enter is the one key whose meaning depends on where the caret stands. In a value of text it opens a
/// line, the way it does in any editor, and the model stores the line as part of the value. Anywhere
/// else - a keyword, a space, an empty slot - there is no text to open a line in, and the key keeps its
/// structural meaning of inserting a node.
/// </para>
/// <para>
/// The action is only ever chosen for a place whose property is a text, so the break lands in a value
/// that can hold it. It is still a delegate to <see cref="TypeTextAction"/>, which means the model keeps
/// the final word: a property that refuses the new value reports a rejection rather than throwing.
/// </para>
/// </remarks>
public sealed class InsertLineBreakAction : EditAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new TypeTextAction("\n").Apply(context);
    }

    public override string ToString() => "line break";
}
