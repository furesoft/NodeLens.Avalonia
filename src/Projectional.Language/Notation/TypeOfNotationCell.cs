namespace Projectional.Language;

/// <summary>A cell that draws the type of the node whose notation it is, as an annotation.</summary>
/// <remarks>
/// <para>
/// An inlay hint, not part of the document: like every constant, it is not editable and holds no value, and the text
/// it shows is derived from the model every time the tree is built - which is why a type that changes is redrawn
/// without the author doing anything.
/// </para>
/// <para>
/// <b>The notation decides whether and where.</b> Nothing about this cell is automatic: a concept that does not
/// mention it shows no type at all, and one that does says on which line and after which word, exactly as it says
/// where a keyword goes. That is the difference between an annotation and a decoration the framework adds behind the
/// author's back.
/// </para>
/// <para>
/// A language that declares no type rules still draws something: <c>unknown</c>. It is the honest answer - no rule
/// said what the node means - and hiding the cell instead would make a language being written look like a language
/// whose notations are wrong.
/// </para>
/// </remarks>
public sealed class TypeOfNotationCell : InlineNotationCell
{
    private TypeOfNotationCell()
    {
    }

    /// <summary>The single instance: the cell names no slot and carries no data.</summary>
    public static TypeOfNotationCell Instance { get; } = new();

    public override string ToString() => "typeof";
}
