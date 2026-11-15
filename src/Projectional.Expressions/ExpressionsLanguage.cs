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

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(ExpressionsConceptKeys.Expression, isAbstract: true),

        new ConceptDeclaration(
            ExpressionsConceptKeys.IntLiteral,
            [new PropertySlot("value", PropertyType.Integer)],
            [ExpressionsConceptKeys.Expression]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.BoolLiteral,
            [new PropertySlot("value", PropertyType.Boolean)],
            [ExpressionsConceptKeys.Expression]),

        new ConceptDeclaration(
            ExpressionsConceptKeys.StringLiteral,
            [new PropertySlot("value", PropertyType.String)],
            [ExpressionsConceptKeys.Expression]),

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
