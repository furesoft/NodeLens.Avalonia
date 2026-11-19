namespace Projectional.Core;

/// <summary>
/// What a concept says about the spelling of its name: anything at all, or an identifier.
/// </summary>
/// <remarks>
/// <para>
/// A name slot says <em>where</em> a name is stored (<see cref="Concept.NameSlot"/>); this says what a legal name
/// looks like. Two values exist and both are closed, because a name rule is a statement about the language and not
/// a place to put a predicate:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="AnyText"/> - anything the user can type. This is the fallback when no concept in a chain declares a
/// rule, and it is also a value an author may declare explicitly to switch an inherited rule off.
/// </description></item>
/// <item><description>
/// <see cref="Identifier"/> - a letter or underscore followed by letters, digits and underscores, all ASCII. A name
/// that breaks it is reported by <c>STD004</c>, which offers <see cref="Propose"/> as the fix.
/// </description></item>
/// </list>
/// <para>
/// <strong>A concept declares a rule, it does not always have one.</strong> <see cref="Concept.NameRule"/> is
/// <see langword="null"/> when nothing in the concept's chain declared a rule, which is the ordinary case and the
/// reason this type is not defaulted anywhere: "nobody said" and "somebody said <c>any</c>" are different facts,
/// and only the second one survives inheritance. A concept that declares <c>any</c> hands <c>any</c> to its
/// subtypes and keeps the check silent for them, while a concept that declares nothing takes its first declaring
/// super's rule. An aspect that defaulted to <see cref="AnyText"/> would erase that difference - the framework's
/// own <c>Framework.Named</c> interface would answer "any" for every concept that implements it and shadow a rule
/// declared further down.
/// </para>
/// <para>
/// The rule is inherited exactly like the name slot: the concept's own declaration wins, then the first declaring
/// super in declaration order. It is not a second place to say "this concept is named" - a concept without a name
/// slot cannot be given a name rule that matters, and neither registry nor check refuses that combination, because
/// declaring a rule on a concept that later gains a name slot is a legitimate order of work.
/// </para>
/// </remarks>
public sealed class NameRule
{
    private NameRule(string text) => Text = text;

    /// <summary>Anything the user can type is a legal name. The fallback, and a value an author may declare.</summary>
    public static NameRule AnyText { get; } = new("any");

    /// <summary>A name must be an ASCII identifier: <c>[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    public static NameRule Identifier { get; } = new("identifier");

    /// <summary>The word this rule is written and read as: <c>any</c> or <c>identifier</c>.</summary>
    public string Text { get; }

    /// <summary>
    /// The rule a declaration's text names, or a <see cref="ConceptDefinitionException"/> when the text names no
    /// rule at all.
    /// </summary>
    /// <param name="text">The declared word, one of <c>any</c> or <c>identifier</c>.</param>
    /// <exception cref="ConceptDefinitionException"><paramref name="text"/> is neither word.</exception>
    /// <remarks>
    /// This is the seam the structure language's lowering uses, so a typo in a definition document is a language
    /// error at load rather than a rule that silently does nothing. A declaration that names no rule at all does not
    /// call this - "no rule" is <see langword="null"/>, not an empty string.
    /// </remarks>
    public static NameRule Parse(string text) => text switch
    {
        "any" => AnyText,
        "identifier" => Identifier,
        _ => throw new ConceptDefinitionException(
            $"'{text}' is not a name rule; a name rule is 'any' or 'identifier'."),
    };

    /// <summary>Whether <paramref name="name"/> is a legal name under this rule.</summary>
    /// <param name="name">The name to test. Empty is legal: a name nobody has set yet is not a spelling error.</param>
    /// <returns><see langword="true"/> for <see cref="AnyText"/>, and for <see cref="Identifier"/> when the name is
    /// an ASCII identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An empty name is legal on purpose: "nobody has named this yet" is <c>STD001</c>'s business (a name still
    /// equal to its placeholder), and reporting it here as well would make one fact produce two findings. The check
    /// therefore reports only a name that is set and misspelt.
    /// </remarks>
    public bool IsValid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (ReferenceEquals(this, AnyText))
        {
            return true;
        }

        if (name.Length == 0)
        {
            return true;
        }

        if (!IsStart(name[0]))
        {
            return false;
        }

        foreach (var character in name.AsSpan(1))
        {
            if (!IsIdentifierPart(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The closest legal name to <paramref name="name"/>, which is what the fix offers.</summary>
    /// <param name="name">The name to repair.</param>
    /// <returns>For <see cref="AnyText"/> the name unchanged; for <see cref="Identifier"/> a name with every
    /// illegal character replaced by an underscore, and a leading digit given an underscore in front.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The proposal is mechanical on purpose: it never invents words, so the author can see what changed. Replacing
    /// with an underscore rather than deleting keeps the name recognisable (<c>my name</c> becomes <c>my_name</c>,
    /// not <c>myname</c>), and a name that is empty stays empty rather than becoming an underscore nobody asked for.
    /// </remarks>
    public string Propose(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (ReferenceEquals(this, AnyText) || name.Length == 0)
        {
            return name;
        }

        var builder = new System.Text.StringBuilder(name.Length + 1);

        foreach (var character in name)
        {
            builder.Append(IsIdentifierPart(character) ? character : '_');
        }

        if (builder.Length > 0 && !IsStart(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    public override string ToString() => Text;

    private static bool IsStart(char character) => char.IsAsciiLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}