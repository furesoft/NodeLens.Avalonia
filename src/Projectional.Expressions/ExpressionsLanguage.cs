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
        new ConceptDeclaration(
            ExpressionsConceptKeys.Expression,
            isAbstract: true,
            description: "Something that evaluates to a value."),

        new ConceptDeclaration(
            ExpressionsConceptKeys.IntLiteral,
            [new PropertySlot("value", PropertyType.Integer, description: "A whole number, such as 42.")],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.BoolLiteral,
            [new PropertySlot("value", PropertyType.Boolean, description: "Either true or false.")],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.StringLiteral,
            [new PropertySlot("value", PropertyType.String, description: "Any text, without the quotes.")],
            [ExpressionsConceptKeys.Expression],
            isOfferedInCompletion: false),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Name,
            [new PropertySlot("name", PropertyType.String, description: "The name to read, such as amount.")],
            [ExpressionsConceptKeys.Expression],
            nameSlot: "name",
            description: "A reference to a name that some scope resolves."),

        // The gestures, declared on the concepts they create: an operator typed at an edge of an existing
        // expression, and a parenthesis typed in front of one. Binary's left trigger does not claim "-", because a
        // minus in front of something reads as a negation - the narrower reading belongs to Unary, and a language
        // that leaves the tie to declaration order would have to rank Unary above Binary in the completion menu to
        // get it.
        new ConceptDeclaration(
            ExpressionsConceptKeys.Binary,
            [
                new PropertySlot("operator", ExpressionOperators.BinaryType, description: "The operator, such as + or &&."),
                new ChildSlot("left", ExpressionsConceptKeys.Expression, Cardinality.One),
                new ChildSlot("right", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            [ExpressionsConceptKeys.Expression],
            triggers:
            [
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Right,
                    text => ExpressionOperators.Binary.Contains(text),
                    anchorSlot: "left",
                    propertySlot: "operator",
                    caretSlot: "right"),
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Left,
                    text => ExpressionOperators.Binary.Contains(text) && text != "-",
                    anchorSlot: "right",
                    propertySlot: "operator",
                    caretSlot: "left"),

                // The words, which no single keystroke spells: the presets turn the word into the operator the
                // model stores, and the session's buffer is what waits for the last letter.
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Right,
                    text => text == "and",
                    anchorSlot: "left",
                    caretSlot: "right",
                    presets: new Dictionary<string, string>(StringComparer.Ordinal) { ["operator"] = "&&" }),
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Right,
                    text => text == "or",
                    anchorSlot: "left",
                    caretSlot: "right",
                    presets: new Dictionary<string, string>(StringComparer.Ordinal) { ["operator"] = "||" }),
            ]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Unary,
            [
                new PropertySlot("operator", ExpressionOperators.UnaryType, description: "Either - or !."),
                new ChildSlot("operand", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            [ExpressionsConceptKeys.Expression],
            triggers:
            [
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Left,
                    text => ExpressionOperators.Unary.Contains(text),
                    anchorSlot: "operand",
                    propertySlot: "operator"),
            ]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.Parenthesized,
            [new ChildSlot("inner", ExpressionsConceptKeys.Expression, Cardinality.One)],
            [ExpressionsConceptKeys.Expression],
            triggers:
            [
                // A parenthesis is a gesture rather than a value: nothing is written, and the caret stays inside
                // the expression that was grouped.
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Left,
                    text => text == "(",
                    anchorSlot: "inner"),
            ]),

        // A conditional is the one concept here that is not a rearrangement of its operands, so it is the one a
        // user reaches for while writing an expression rather than from the top of a list. The gesture is a
        // question mark after the expression that becomes its condition, which is also the reading of `a ? b : c`
        // in the languages this vocabulary borrows from.
        new ConceptDeclaration(
            ExpressionsConceptKeys.If,
            [
                new ChildSlot("condition", ExpressionsConceptKeys.Expression, Cardinality.One),
                new ChildSlot("then", ExpressionsConceptKeys.Expression, Cardinality.One),
                new ChildSlot("else", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            [ExpressionsConceptKeys.Expression],
            triggers:
            [
                new ConceptTrigger(
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Right,
                    text => text == "?",
                    anchorSlot: "condition",
                    caretSlot: "then"),
            ]),
    ];
}
