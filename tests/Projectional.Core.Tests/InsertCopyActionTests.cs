using Projectional.Core;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Putting a copy of one node where another stands, and the declared intention that asks for it.</summary>
/// <remarks>
/// The action is the framework's half - a stated place for a copy, answered as a reason when that place has gone stale -
/// and the body is the language's: it names the reference slot whose target stands for the meaning of the node at the
/// caret. The end-to-end case is what makes the pair worth anything: the use is replaced by a copy of its definition and
/// the definition stays where it is, in one undo step.
/// </remarks>
public sealed class InsertCopyActionTests
{
    [Fact]
    public void ACopyLandsInAnotherParentAndTheSourceStays()
    {
        var (model, root, definition, use) = Model();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new InsertCopyAction(definition.Id, root.Id, InlineLanguage.Uses, 1));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Same(definition, Assert.Single(root.GetChildren(InlineLanguage.Definitions)));
        Assert.Equal(2, root.GetChildren(InlineLanguage.Uses).Count);
        Assert.Same(use, root.GetChildren(InlineLanguage.Uses)[0]);

        var copy = root.GetChildren(InlineLanguage.Uses)[1];

        Assert.NotEqual(definition.Id, copy.Id);
        Assert.Equal("build", copy.GetOrNull("text"));
    }

    [Fact]
    public void TheCopyHasItsOwnIdentitySoTheTwoCanBeEditedApart()
    {
        var (_, root, definition, _) = Model();

        using var session = TestEditor.Open(root);

        session.Apply(new InsertCopyAction(definition.Id, root.Id, InlineLanguage.Uses, 0));

        var copy = root.GetChildren(InlineLanguage.Uses)[0];

        copy.Set("text", "changed");

        Assert.Equal("build", definition.GetOrNull("text"));
    }

    [Fact]
    public void AStalePlaceIsAReasonNotAThrow()
    {
        var (_, root, definition, _) = Model();

        using var session = TestEditor.Open(root);
        var id = definition.Id;

        session.Apply(new DeleteNodeAction(id));

        var result = session.Apply(new InsertCopyAction(id, root.Id, InlineLanguage.Uses, 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer in the document", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotTheParentDoesNotDeclareIsAReason()
    {
        var (_, root, definition, _) = Model();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new InsertCopyAction(definition.Id, root.Id, "nowhere", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no child slot", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeCannotBeCopiedIntoItsOwnSubtree()
    {
        // A copy of an ancestor would contain the place it goes, so the structure would never end - and the model's own
        // InsertCopy recurses until the stack gives out rather than refusing. The action is where that is answered.
        var (_, root, _, _) = Model();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new InsertCopyAction(root.Id, root.Id, InlineLanguage.Uses, 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("its own subtree", result.Reason, StringComparison.Ordinal);
    }
    [Fact]
    public void ADeclaredInlineReplacesTheUseWithACopyOfItsDefinition()
    {
        var (_, root, definition, use) = Model();

        using var session = new EditorSession(root, InlineLanguage.Notations);

        Caret(session, use);

        session.AddIntentions(Provider(), IntentionSource.Language);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Inline this use", offer.Label);
        Assert.Equal(EditSafety.Destructive, offer.Safety);

        session.Apply(offer);

        Assert.Equal("build", Assert.Single(root.GetChildren(InlineLanguage.Uses)).GetOrNull("text"));
        Assert.Same(definition, Assert.Single(root.GetChildren(InlineLanguage.Definitions)));

        session.History.Undo();

        Assert.Same(use, Assert.Single(root.GetChildren(InlineLanguage.Uses)));
        Assert.Same(definition, Assert.Single(root.GetChildren(InlineLanguage.Definitions)));
    }

    [Fact]
    public void AnInlineNamingAReferenceTheConceptDoesNotHaveIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([Declaration("nowhere").Rule], InlineLanguage.Registry));

        Assert.Contains("does not declare", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>An item in <c>defs</c> named "build", and a use of it.</summary>
    private static (Model Model, Node Root, Node Definition, Node Use) Model()
    {
        var model = InlineLanguage.NewModel();
        var root = model.NewNode(InlineLanguage.Root);
        var definition = model.NewChild(root, InlineLanguage.Definitions, InlineLanguage.Item);
        var use = model.NewChild(root, InlineLanguage.Uses, InlineLanguage.Item);

        model.AddRoot(root);
        definition.Set("text", "build");
        use.Set("text", "run build");
        use.SetReference(InlineLanguage.Target, definition);

        return (model, root, definition, use);
    }

    /// <summary>The declared intention: on an item, inline its <c>target</c>.</summary>
    private static (Model Model, Node Rule) Declaration(string reference = InlineLanguage.Target)
    {
        var model = IntentionLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(IntentionLanguage.ConceptKeySlot, InlineLanguage.Item);
        rule.Set(IntentionLanguage.LabelSlot, "Inline this use");
        rule.Set(IntentionLanguage.DescriptionSlot, "Puts what the target holds in this node's place.");

        var action = Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot));
        var inline = model.Replace(action, IntentionConceptKeys.Inline);

        inline.Set(IntentionLanguage.ReferenceSlot, reference);

        return (model, rule);
    }

    private static IIntentionProvider Provider() => IntentionLowering.CreateProvider([Declaration().Rule], InlineLanguage.Registry);

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
