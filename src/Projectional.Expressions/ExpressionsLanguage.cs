using Projectional.Core;
using Projectional.Language;

namespace Projectional.Expressions;

/// <summary>
/// The shared expression sub-language: a small set of concepts for naming things and for computing
/// with them, meant to be put next to a host's own concepts rather than used on its own.
/// </summary>
/// <remarks>
/// <para>
/// Nearly every DSL needs "a name" and "a condition", and no two of them need it differently enough to
/// pay for writing it again. This is that part, declared once: literals, names, the two operator
/// forms, and explicit parentheses. A host declares its slots over
/// <see cref="ExpressionsConceptKeys.Expression"/> and gets all of them, and
/// <see cref="ExpressionsConceptKeys.Name"/> is what a host refers to when it means "the name of
/// something".
/// </para>
/// <para>
/// A host composes rather than inherits: the concept set is a list of declarations, so the host's own
/// <see cref="ConceptRegistry"/> is built from <see cref="ConceptDeclarations"/> plus the host's
/// declarations, and its notations are built by handing its <see cref="NotationRegistry"/> to
/// <see cref="AddNotations"/>. That is the whole integration, and it is why a slot may be declared
/// over an expression without the sub-language owning any model.
/// </para>
/// <para>
/// <see cref="Expression"/> is abstract on purpose. A slot declared over it names a set of
/// expressions and never "an expression", so the model cannot hold one, and the editor fills an empty
/// slot with the first concrete concept below it - <see cref="ExpressionsConceptKeys.IntLiteral"/>,
/// which is the one a user is most likely to want.
/// </para>
/// <para>
/// The three literal concepts are declared with <c>isOfferedInCompletion: false</c>. A literal is
/// something a user creates to type into, not something they pick out of a menu of eight, so listing
/// them only pushes the concepts that are worth choosing down. They stay reachable exactly as before:
/// Enter in an empty slot still materializes the integer literal.
/// </para>
/// </remarks>
public static class ExpressionsLanguage
{
    /// <summary>Every concept this language declares, in declaration order.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>This language's concepts on their own, for a host whose whole language is expressions.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>Adds the notations of this language to an existing registry.</summary>
    /// <exception cref="NotationException">
    /// <paramref name="notations"/> was built for a concept registry that does not contain these concepts.
    /// </exception>
    public static void AddNotations(NotationRegistry notations) => ExpressionsNotations.AddTo(notations);

    /// <summary>A registry holding this language's notation and nothing else.</summary>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        AddNotations(notations);

        return notations;
    }

    /// <summary>A project whose language is this one, which is what the expression tests open.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>What each concept claims when the user starts typing into an empty expression slot.</summary>
    /// <remarks>
    /// <para>
    /// Without this, typing into an empty expression slot does nothing: the slot names the abstract
    /// <see cref="ExpressionsConceptKeys.Expression"/>, and the only property in reach - the operator of
    /// a binary - accepts neither a digit nor a letter. With it, the first keystroke picks the concept and
    /// fills it, so <c>1</c> gives an integer literal, <c>"</c> a string literal, a letter a name, and
    /// <c>(</c> the parenthesised form with the caret inside it.
    /// </para>
    /// <para>
    /// The rules only have to be disjoint, because each candidate concept is asked in declaration order: a
    /// digit is an integer literal before it could be anything else, and a word is a boolean only when it
    /// is exactly <c>true</c> or <c>false</c> - a bare <c>t</c> is a name, since claiming it as a boolean
    /// would make it impossible to write a name that starts with one.
    /// </para>
    /// </remarks>
    public static ITypingRules TypingRules { get; } = TypingRuleRegistry.Empty
        .Add(ExpressionsConceptKeys.IntLiteral, text => text.All(char.IsAsciiDigit), "value")
        .Add(ExpressionsConceptKeys.BoolLiteral, text => text is "true" or "false", "value")
        .Add(ExpressionsConceptKeys.StringLiteral, text => text.StartsWith('"'), "value")
        .Add(ExpressionsConceptKeys.Name, ClaimsName, "name")
        .Add(ExpressionsConceptKeys.Parenthesized, text => text.StartsWith('('));

    private static bool ClaimsName(string text) =>
        text.Length > 0
        && (char.IsAsciiLetter(text[0]) || text[0] == '_')
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(ExpressionsConceptKeys.Expression, isAbstract: true),

        new ConceptDeclaration(
            ExpressionsConceptKeys.IntLiteral,
            [new PropertySlot("value", PropertyType.Integer)],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.BoolLiteral,
            [new PropertySlot("value", PropertyType.Boolean)],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.StringLiteral,
            [new PropertySlot("value", PropertyType.String)],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Name,
            [new PropertySlot("name", PropertyType.String)],
            [ExpressionsConceptKeys.Expression],
            nameSlot: "name"),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Binary,
            [
                new PropertySlot("operator", ExpressionOperators.BinaryType),
                new ChildSlot("left", ExpressionsConceptKeys.Expression, Cardinality.One),
                new ChildSlot("right", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            [ExpressionsConceptKeys.Expression]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Unary,
            [
                new PropertySlot("operator", ExpressionOperators.UnaryType),
                new ChildSlot("operand", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            [ExpressionsConceptKeys.Expression]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Parenthesized,
            [new ChildSlot("inner", ExpressionsConceptKeys.Expression, Cardinality.One)],
            [ExpressionsConceptKeys.Expression]),
    ];
}
