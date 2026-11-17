using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a copied subtree remembers: the concept, the values that were written, the references, and the children
/// in the slot they stood in.
/// </summary>
/// <remarks>
/// The fragment is the payload the clipboard carries instead of text, so what it keeps is what a paste can put
/// back. Three rules are pinned here: a value the user never wrote stays defaulted rather than being copied as
/// the default it materializes, a reference keeps both of its halves, and children keep the slot they were in -
/// not merely their order.
/// </remarks>
public sealed class ModelFragmentTests
{
    [Fact]
    public void AFragmentKeepsTheValuesThatWereWrittenAndNotTheOnesThatWereDefaulted()
    {
        var step = TestEditor.BareStep("walk");

        var node = Assert.Single(ModelFragment.Of([step]).Roots);

        Assert.Equal(TestLanguage.Step, node.ConceptKey);
        Assert.Equal(step.Id, node.Origin);
        Assert.Equal("walk", node.Properties["text"]);

        // `index`, `due` and `status` declare no default and were never written, so there is nothing to copy.
        Assert.False(node.Properties.ContainsKey("index"));
        Assert.False(node.Properties.ContainsKey("status"));
    }

    [Fact]
    public void AFragmentKeepsChildrenInTheSlotTheyStoodIn()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        var note = body.Model.NewChild(body, "notes", TestLanguage.Note);
        note.Set("text", "remember");

        var node = Assert.Single(ModelFragment.Of([body]).Roots);

        Assert.Equal(3, node.Children.Count);
        Assert.Equal(["steps", "steps", "notes"], node.Children.Select(child => child.Slot));
        Assert.Equal(
            ["first", "second", "remember"],
            node.Children.Select(child => child.Node.Properties["text"]));
        Assert.Equal(steps[0].Id, node.Children[0].Node.Origin);
    }

    [Fact]
    public void AReferenceKeepsItsTargetAndTheNameItWasStoredWith()
    {
        var rule = TestEditor.BareRule();
        var tag = rule.Model.NewNode(TestLanguage.Tag);
        tag.Set("label", "finance");
        rule.Model.AddRoot(tag);
        rule.SetReference("tag", tag);

        var node = Assert.Single(ModelFragment.Of([rule]).Roots);
        var reference = Assert.Single(node.References);

        Assert.Equal("tag", reference.Slot);
        Assert.Equal(tag.Id, reference.Target);
        Assert.Equal("finance", reference.ResolveInfo);
    }

    [Fact]
    public void AFragmentOfSeveralNodesKeepsThemInTheOrderTheyWereGiven()
    {
        var (_, steps) = TestEditor.BodyWithSteps("first", "second", "third");

        var fragment = ModelFragment.Of([steps[2], steps[0]]);

        Assert.Equal([steps[2].Id, steps[0].Id], fragment.Roots.Select(root => root.Origin));
    }

    [Fact]
    public void AFragmentNamesTheFirstConceptItsTargetLanguageDoesNotDeclare()
    {
        var item = GroupEditor.GroupWithItems("first").Items[0];
        var fragment = ModelFragment.Of([item]);

        Assert.Null(fragment.MissingConcept(GroupLanguage.Registry));
        Assert.Equal(GroupLanguage.Item, fragment.MissingConcept(TestLanguage.Registry));
    }

    [Fact]
    public void AFragmentOfNothingIsEmpty()
    {
        Assert.True(new ModelFragment([]).IsEmpty);
        Assert.False(ModelFragment.Of([TestEditor.BareStep()]).IsEmpty);
    }
}
