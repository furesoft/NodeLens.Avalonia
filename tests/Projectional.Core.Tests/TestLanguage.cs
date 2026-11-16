namespace Projectional.Core.Tests;

/// <summary>
/// The small language the model tests are written in: a named rule that owns a body and can
/// extend another rule, a body holding ordered steps and notes, and a tag that rules point at.
/// </summary>
/// <remarks>
/// Everything the model tests need to provoke an invariant lives here once, so that each test
/// reads as the rule it is checking rather than as language setup.
/// </remarks>
internal static class TestLanguage
{
    public const string Tag = "Test.Tag";
    public const string Rule = "Test.Rule";
    public const string Body = "Test.Body";
    public const string Step = "Test.Step";
    public const string Note = "Test.Note";

    public static ConceptRegistry Registry { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Tag,
            [new PropertySlot("label", PropertyType.String)],
            nameSlot: "label"),

        new ConceptDeclaration(
            Rule,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),
                new PropertySlot("enabled", PropertyType.Boolean, "true"),
                new ChildSlot("body", Body, Cardinality.One),
                new ChildSlot("extends", Rule, Cardinality.One),
                new ReferenceSlot("tag", Tag),
                new ReferenceSlot("inherits", Rule),
                new ReferenceSlot("self", Rule, allowOwnSubtree: true),
            ],
            nameSlot: "name",
            description: "A named rule with a body and an outcome.",
            // `enabled` doubles as the collapse slot: it is a boolean, it defaults to true, and the
            // fixture's job is to exercise the mechanism rather than to be a sensible language.
            collapseSlot: "enabled"),

        new ConceptDeclaration(
            Body,
            [new ChildSlot("steps", Step), new ChildSlot("notes", Note)]),

        new ConceptDeclaration(
            Step,
            [
                new PropertySlot("text", PropertyType.String, description: "What the step does."),
                new PropertySlot("index", PropertyType.Integer),
                new PropertySlot("due", PropertyType.Date),
                new PropertySlot("status", PropertyType.Enum("draft", "approved")),
                new ChildSlot("detail", Step, Cardinality.One),
            ]),

        new ConceptDeclaration(Note, [new PropertySlot("text", PropertyType.String)]),
    ]);

    public static Project NewProject() => new(Registry);

    public static Model NewModel() => NewProject().NewModel("Test");
}
