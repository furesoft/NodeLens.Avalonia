using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// The smallest language a refactoring can be pointed at: a container that runs a chain of steps and keeps the steps
/// it reuses, a step, and a call that stands where a step stands and points back at one.
/// </summary>
/// <remarks>
/// It is deliberately not <see cref="TestLanguage"/>. An extract needs a <em>use</em> - something that takes the
/// extracted node's place - and a use has to be a kind of what it replaces, because that is what makes it fit
/// wherever the definition fits. Added to the shared fixture, a call would make every step replaceable by a call and
/// change what the standard intentions offer in documents other tests own, and
/// <see cref="DeclaredTestLanguage"/> - the parity twin - would have to grow with it. A second small language keeps
/// that out of everybody else's fixture.
/// </remarks>
internal static class ExtractLanguage
{
    public const string Container = "Extract.Container";
    public const string Chain = "Extract.Chain";
    public const string Step = "Extract.Step";
    public const string Call = "Extract.Call";

    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Container,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),
                new ChildSlot("chain", Chain, Cardinality.One),
                new ChildSlot("library", Step),
                new ChildSlot("single", Step, Cardinality.One),
            ],
            nameSlot: "name",
            description: "One workflow: what it runs, and the steps it runs more than once."),

        new ConceptDeclaration(
            Chain,
            [new ChildSlot("steps", Step)],
            description: "An ordered run of steps."),

        new ConceptDeclaration(
            Step,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),

                // Not text, and on purpose: the extract's name write has to say what it does with a property that
                // cannot hold the name it is about to be given.
                new PropertySlot("attempts", PropertyType.Integer, "1"),
            ],
            nameSlot: "name",
            description: "One command."),

        new ConceptDeclaration(
            Call,
            [new ReferenceSlot("calls", Step)],
            [Step],
            description: "Runs a step of this workflow here instead of a command of its own."),
    ]);

    public static NotationRegistry Notations { get; } = Create();

    public static Project NewProject() => new(Concepts);

    /// <summary>A container whose chain holds one step per name, in order, and whose library is empty.</summary>
    /// <param name="names">The names of the steps, in the order they run.</param>
    public static (Node Container, IReadOnlyList<Node> Steps) WithSteps(params string[] names)
    {
        var container = NewProject().NewModel("Extract").NewNode(Container);
        var chain = container.Model.NewChild(container, "chain", Chain);

        IReadOnlyList<Node> steps =
        [
            .. names.Select(name =>
            {
                var step = container.Model.NewChild(chain, "steps", Step);
                step.Set("name", name);

                return step;
            }),
        ];

        return (container, steps);
    }

    /// <summary>A step that belongs to nothing, which is what a document root looks like with one node.</summary>
    public static Node LoneStep()
    {
        var step = NewProject().NewModel("Extract").NewNode(Step);
        step.Set("name", "alone");

        return step;
    }

    public static EditorSession Open(Node document) => new(document, Notations);

    /// <summary>
    /// The notation draws the chain and the library, so a caret can stand on a step wherever it is - which is what an
    /// offer, and the caret that follows an extract, are asked about.
    /// </summary>
    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(Concepts);

        notations.Add(Container, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("workflow"),
                ed.Space,
                ed.Property("name")),
            ed.Indent(
                ed.Children("chain"),
                ed.Line(ed.Keyword("library")),
                ed.Indent(ed.Children("library")))));

        notations.Add(Chain, ed => ed.Children("steps"));

        // A call inherits this, and draws its own name: the reference back to the definition is what the extract
        // wrote, not what the notation has to show.
        notations.Add(Step, ed => ed.Line(ed.Property("name")));

        return notations;
    }
}
