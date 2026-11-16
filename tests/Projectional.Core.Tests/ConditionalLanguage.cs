using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A language whose concepts are not valid everywhere: an attribute that may stand on a document or a class but
/// not on a local, and a comment that may stand inside a member but not at the top.
/// </summary>
/// <remarks>
/// The shape of the C# problem in miniature. Both concepts are legal children of every <c>annotations</c> slot
/// <em>by type</em>, so nothing about the slots can tell them apart - only the place each concept names.
/// </remarks>
internal static class ConditionalLanguage
{
    public const string Document = "Conditional.Document";
    public const string Member = "Conditional.Member";
    public const string Class = "Conditional.Class";
    public const string Local = "Conditional.Local";
    public const string Annotation = "Conditional.Annotation";
    public const string Comment = "Conditional.Comment";

    public const string Annotations = "annotations";
    public const string Members = "members";

    /// <summary>
    /// <c>Annotation</c> may stand on a document or a class; <c>Comment</c> anywhere inside a member, which
    /// includes a class - so the two overlap in one place and each has a place the other lacks.
    /// </summary>
    public static ConceptRegistry Registry { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Document,
            [
                new ChildSlot(Annotations, Annotation, Cardinality.Many),
                new ChildSlot(Members, Member, Cardinality.Many),
            ]),

        new ConceptDeclaration(
            Member,
            [new ChildSlot(Annotations, Annotation, Cardinality.Many)],
            isAbstract: true),

        new ConceptDeclaration(Class, superConcepts: [Member]),

        new ConceptDeclaration(Local, superConcepts: [Member]),

        new ConceptDeclaration(
            Annotation,
            [new PropertySlot("text", PropertyType.String)],
            applicability: Applicability.Only(
                new ConceptPlace(Document, Annotations),
                new ConceptPlace(Class, Annotations))),

        new ConceptDeclaration(
            Comment,
            [new PropertySlot("text", PropertyType.String)],
            [Annotation],
            applicability: Applicability.Only(new ConceptPlace(Member, Annotations))),
    ]);

    /// <summary>What a user types to start one: both claim the same character, so only the place decides.</summary>
    public static ITypingRules TypingRules { get; } = TypingRuleRegistry.Empty
        .Add(Annotation, text => text == "@", "text")
        .Add(Comment, text => text == "@", "text");

    public static NotationRegistry Notations { get; } = CreateNotations();

    private static NotationRegistry CreateNotations()
    {
        var notations = new NotationRegistry(Registry);

        notations.Add(Document, ed => ed.Sequence(
            ed.Line(ed.Keyword("document")),
            ed.Indent(
                ed.Line(ed.Keyword("annotations")),
                ed.Children(Annotations),
                ed.Line(ed.Keyword("members")),
                ed.Children(Members))));

        notations.Add(Class, ed => ed.Sequence(
            ed.Line(ed.Keyword("class")),
            ed.Indent(
                ed.Line(ed.Keyword("annotations")),
                ed.Children(Annotations))));

        notations.Add(Local, ed => ed.Sequence(
            ed.Line(ed.Keyword("local")),
            ed.Indent(
                ed.Line(ed.Keyword("annotations")),
                ed.Children(Annotations))));

        notations.Add(Annotation, ed => ed.Line(ed.Punct("@"), ed.Property("text")));

        notations.Add(Comment, ed => ed.Line(ed.Punct("//"), ed.Property("text")));

        return notations;
    }
}
