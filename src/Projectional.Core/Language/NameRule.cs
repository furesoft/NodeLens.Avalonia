namespace Projectional.Core;

/// <summary>
/// What a concept says about the spelling of its name: anything at all, an identifier, or one of the case
/// conventions a language may want.
/// </summary>
/// <remarks>
/// <para>
/// A name slot says <em>where</em> a name is stored (<see cref="Concept.NameSlot"/>); this says what a legal name
/// looks like. The values are closed and fixed, because a name rule is a statement about the language and not a
/// place to put a predicate:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="AnyText"/> - anything the user can type. This is the fallback when no concept in a chain declares a
/// rule, and it is also a value an author may declare explicitly to switch an inherited rule off.
/// </description></item>
/// <item><description>
/// <see cref="Identifier"/> - a letter or underscore followed by letters, digits and underscores, all ASCII, in
/// whatever convention the language likes.
/// </description></item>
/// <item><description>
/// The case conventions - <see cref="Pascal"/>, <see cref="Camel"/>, <see cref="Snake"/>,
/// <see cref="ScreamingSnake"/>, <see cref="Kebab"/>, <see cref="ScreamingKebab"/>, <see cref="Train"/>,
/// <see cref="Dot"/> and <see cref="PascalDot"/>. They name the same words an identifier carries and say how
/// those words are separated and cased; the hyphenated and dotted ones are conventions for a name that is not
/// meant to be a program identifier (a CSS class, a qualified key), which is legal because a name is a property
/// value and never a symbol the compiler sees.
/// </description></item>
/// </list>
/// <para>
/// A name that breaks the rule is reported by <c>STD004</c>, which offers <see cref="Propose"/> as the fix, and by
/// nothing else - a rule is validation, never a refusal at the edit: the model accepts what the user types and a
/// warning names it afterwards.
/// </para>
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
    private readonly bool _anyText;
    private readonly NameCase? _case;

    private NameRule(string text, string requirement, bool anyText = false, NameCase? nameCase = null)
    {
        Text = text;
        Requirement = requirement;
        _anyText = anyText;
        _case = nameCase;
    }

    /// <summary>Anything the user can type is a legal name. The fallback, and a value an author may declare.</summary>
    public static NameRule AnyText { get; } = new("any", "any text", anyText: true);

    /// <summary>A name must be an ASCII identifier: <c>[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    /// <remarks>
    /// The convention-free rule: it says a name is one identifier and says nothing about how its words are cased.
    /// A language that has a convention declares the convention instead, which is stricter and says more.
    /// </remarks>
    public static NameRule Identifier { get; } = new("identifier", "an identifier");

    /// <summary>Capitalized words run together: <c>MyRule</c>.</summary>
    public static NameRule Pascal { get; } = new("pascal", "PascalCase", nameCase: NameCase.Pascal);

    /// <summary>A lower-case first word and capitalized ones after it: <c>myRule</c>.</summary>
    public static NameRule Camel { get; } = new("camel", "camelCase", nameCase: NameCase.Camel);

    /// <summary>Lower-case words joined by underscores: <c>my_rule</c>.</summary>
    public static NameRule Snake { get; } = new("snake", "snake_case", nameCase: NameCase.Snake);

    /// <summary>Upper-case words joined by underscores: <c>MY_RULE</c>.</summary>
    public static NameRule ScreamingSnake { get; } =
        new("screaming-snake", "SCREAMING_SNAKE_CASE", nameCase: NameCase.ScreamingSnake);

    /// <summary>Lower-case words joined by hyphens: <c>my-rule</c>.</summary>
    public static NameRule Kebab { get; } = new("kebab", "kebab-case", nameCase: NameCase.Kebab);

    /// <summary>Upper-case words joined by hyphens: <c>MY-RULE</c>.</summary>
    public static NameRule ScreamingKebab { get; } =
        new("screaming-kebab", "SCREAMING-KEBAB-CASE", nameCase: NameCase.ScreamingKebab);

    /// <summary>Capitalized words joined by hyphens: <c>My-Rule</c>.</summary>
    public static NameRule Train { get; } = new("train", "Train-Case", nameCase: NameCase.Train);

    /// <summary>Lower-case words joined by dots: <c>my.rule</c>.</summary>
    public static NameRule Dot { get; } = new("dot", "dot.case", nameCase: NameCase.Dot);

    /// <summary>Capitalized words joined by dots: <c>My.Rule</c>, which is how a concept key reads.</summary>
    public static NameRule PascalDot { get; } = new("pascal-dot", "dotted PascalCase", nameCase: NameCase.PascalDot);

    /// <summary>Every rule, in the order a declaration's cell offers them.</summary>
    /// <remarks>
    /// The one list a declaration enumerates, so the words the structure language offers and the words
    /// <see cref="Parse"/> accepts cannot drift apart.
    /// </remarks>
    public static IReadOnlyList<NameRule> All { get; } =
    [
        AnyText,
        Identifier,
        Pascal,
        Camel,
        Snake,
        ScreamingSnake,
        Kebab,
        ScreamingKebab,
        Train,
        Dot,
        PascalDot,
    ];

    /// <summary>The word this rule is written and read as, such as <c>any</c> or <c>screaming-snake</c>.</summary>
    public string Text { get; }

    /// <summary>What a name under this rule has to look like, as the phrase a finding quotes.</summary>
    /// <remarks>
    /// <c>an identifier</c> rather than <c>identifier</c>, because a message reads "the name ... is not an
    /// identifier". It is prose for the finding and never an identity - <see cref="Text"/> is what a declaration is
    /// written with.
    /// </remarks>
    public string Requirement { get; }

    /// <summary>
    /// The rule a declaration's text names, or a <see cref="ConceptDefinitionException"/> when the text names no
    /// rule at all.
    /// </summary>
    /// <param name="text">The declared word, one of the values <see cref="All"/> carries.</param>
    /// <exception cref="ConceptDefinitionException"><paramref name="text"/> is not one of them.</exception>
    /// <remarks>
    /// This is the seam the structure language's lowering uses, so a typo in a definition document is a language
    /// error at load rather than a rule that silently does nothing. A declaration that names no rule at all does not
    /// call this - "no rule" is <see langword="null"/>, not an empty string.
    /// </remarks>
    public static NameRule Parse(string text) => text switch
    {
        "any" => AnyText,
        "identifier" => Identifier,
        "pascal" => Pascal,
        "camel" => Camel,
        "snake" => Snake,
        "screaming-snake" => ScreamingSnake,
        "kebab" => Kebab,
        "screaming-kebab" => ScreamingKebab,
        "train" => Train,
        "dot" => Dot,
        "pascal-dot" => PascalDot,
        _ => throw new ConceptDefinitionException(
            $"'{text}' is not a name rule; a name rule is one of {Words()}."),
    };

    /// <summary>Whether <paramref name="name"/> is a legal name under this rule.</summary>
    /// <param name="name">The name to test. Empty is legal: a name nobody has set yet is not a spelling error.</param>
    /// <returns><see langword="true"/> for <see cref="AnyText"/>; for <see cref="Identifier"/> when the name is an
    /// ASCII identifier; for a case rule when the name is already spelled the way that convention says.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An empty name is legal on purpose: "nobody has named this yet" is <c>STD001</c>'s business (a name still
    /// equal to its placeholder), and reporting it here as well would make one fact produce two findings. The check
    /// therefore reports only a name that is set and misspelt - and a name that is still the concept's placeholder
    /// is skipped for the same reason.
    /// </remarks>
    public bool IsValid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_anyText)
        {
            return true;
        }

        if (_case is { } nameCase)
        {
            return nameCase.Matches(name);
        }

        return IsIdentifier(name);
    }

    /// <summary>The closest legal name to <paramref name="name"/>, which is what the fix offers.</summary>
    /// <param name="name">The name to repair.</param>
    /// <returns>For <see cref="AnyText"/> the name unchanged; for <see cref="Identifier"/> a name with every
    /// illegal character replaced by an underscore and a leading digit given one in front; for a case rule the same
    /// words joined the way the convention says.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The proposal is mechanical on purpose: it never invents words, so the author can see what changed. Replacing
    /// with an underscore rather than deleting keeps an identifier recognisable (<c>my name</c> becomes
    /// <c>my_name</c>, not <c>myname</c>), and a name that is empty stays empty rather than becoming an underscore
    /// nobody asked for.
    /// </para>
    /// <para>
    /// A case rule respells the words the name already carries, so it is both a repair and the test of one: a name
    /// the rule accepts is the name <see cref="Propose"/> answers, and the answer always passes
    /// <see cref="IsValid"/> - a repair that left its own finding behind would be broken.
    /// </para>
    /// </remarks>
    public string Propose(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_anyText || name.Length == 0)
        {
            return name;
        }

        if (_case is { } nameCase)
        {
            return nameCase.Format(name);
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

    private static bool IsIdentifier(string name)
    {
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

    private static string Words() => string.Join(", ", All.Select(rule => $"'{rule.Text}'"));

    private static bool IsStart(char character) => char.IsAsciiLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}