using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A property slot whose values come from the host rather than from its declared type.</summary>
/// <remarks>
/// <para>
/// The seam exists for one real slot and is tested on a small language of its own: a behaviour written in C# names an
/// enumeration of the language around it, so the values live in another layer and no <see cref="PropertyType"/> could
/// hold them. What the layer has to guarantee is narrow - an installed provider is asked, and one that says nothing
/// changes nothing - and that is what these tests hold.
/// </para>
/// <para>
/// The language is declared here rather than borrowed from <see cref="TestLanguage"/> because the two slots this needs
/// side by side - free text and a declared enumeration - are not both drawn there, and a fixture that grew them would
/// change what every other test sees.
/// </para>
/// </remarks>
public sealed class ValueCompletionTests
{
    [Fact]
    public void APropertySlotIsOfferedTheValuesTheHostInstalls()
    {
        var thing = NewThing();

        using var session = new EditorSession(thing, Notations)
        {
            Values = new Answering(Note, "one", "two"),
        };

        session.Select(Selection.At(CaretPosition.At(In(session, Note), 0)));
        session.Apply(new OpenCompletionAction());

        Assert.NotNull(session.Completion);
        Assert.Equal(["one", "two"], session.Completion.Items.Select(item => item.ConceptKey));

        // A value is a value, not a reference and not a node: accepting one writes the text, which is what makes the
        // list safe on a slot the language declared as free text.
        Assert.All(session.Completion.Items, item => Assert.Equal(CompletionKind.Value, item.Kind));
    }

    [Fact]
    public void AProviderThatSaysNothingLeavesTheDeclaredTypeToAnswer()
    {
        // Null is "ask the type", not "nothing fits": a declared enumeration still offers its symbols, which is what
        // makes installing a provider harmless for every slot it was never written for.
        var thing = NewThing();

        using var session = new EditorSession(thing, Notations)
        {
            Values = new Answering(Note, "one"),
        };

        session.Select(Selection.At(CaretPosition.At(In(session, Status), 0)));
        session.Apply(new OpenCompletionAction());

        Assert.NotNull(session.Completion);
        Assert.Equal(["draft", "approved"], session.Completion.Items.Select(item => item.ConceptKey));
    }

    [Fact]
    public void AProviderIsAskedBeforeTheDeclaredType()
    {
        // A provider that *does* answer for an enumeration slot wins over the declared symbols: it is the more specific
        // statement, and the host installed it for that slot.
        var thing = NewThing();

        using var session = new EditorSession(thing, Notations)
        {
            Values = new Answering(Status, "blocked"),
        };

        session.Select(Selection.At(CaretPosition.At(In(session, Status), 0)));
        session.Apply(new OpenCompletionAction());

        Assert.NotNull(session.Completion);
        Assert.Equal(["blocked"], session.Completion.Items.Select(item => item.ConceptKey));
    }

    [Fact]
    public void ASessionWithoutAProviderAnswersEverySlotFromTheDocument()
    {
        var thing = NewThing();

        using var session = new EditorSession(thing, Notations);

        session.Select(Selection.At(CaretPosition.At(In(session, Note), 0)));
        session.Apply(new OpenCompletionAction());

        // A plain string property has no closed set and no provider, so there is no list - the behaviour the layer had
        // before the seam existed, which a host that installs nothing keeps.
        Assert.Null(session.Completion);
    }

    private const string Note = "note";

    private const string Status = "status";

    private const string Thing = "Value.Thing";

    private static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(Thing, [
            new PropertySlot(Note, PropertyType.String),
            new PropertySlot(Status, PropertyType.Enum("draft", "approved")),
        ]),
    ]);

    private static NotationRegistry Notations { get; } = NotationsFor();

    private static Node NewThing()
    {
        var model = new Project(Concepts).NewModel("values");
        var thing = model.NewNode(Thing);

        model.AddRoot(thing);

        return thing;
    }

    private static NotationRegistry NotationsFor()
    {
        var ed = NotationEditor.Create();
        var notations = new NotationRegistry(Concepts);

        notations.Add(Thing, ed => ed.Line(
            ed.Property(Note),
            ed.Space,
            ed.Property(Status)));

        return notations;
    }

    /// <summary>The address of the property cell drawn for <paramref name="slot"/>.</summary>
    /// <remarks>
    /// Found rather than spelled: <c>CellAddress.OfSlot</c> names the node and the slot, and the caret at column zero of
    /// that address lands in whichever cell was laid out there. The address of the cell itself is what the test means.
    /// </remarks>
    private static CellAddress In(EditorSession session, string slot) =>
        session.Tree.Cells()
            .OfType<PropertyCell>()
            .First(cell => string.Equals(cell.Slot, slot, StringComparison.Ordinal))
            .Address;

    /// <summary>A provider that answers for one slot and says nothing about the rest.</summary>
    private sealed class Answering(string slot, params string[] values) : IValueProvider
    {
        public IReadOnlyList<string>? ValuesOf(Node owner, string name) =>
            string.Equals(name, slot, StringComparison.Ordinal) ? values : null;
    }
}
