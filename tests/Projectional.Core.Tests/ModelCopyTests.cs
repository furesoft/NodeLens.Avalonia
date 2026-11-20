using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Copying: a whole model into another project (which is what a preview sandbox is built from), and one subtree to
/// a place inside its own model (which is what duplicating a node is).
/// </summary>
/// <remarks>
/// The contract under test is identity, in both directions. A model copy carries the same node ids, so an edit that
/// addresses ids and slots means the same thing in the copy as in the original. A subtree copy inside one model
/// must carry <em>new</em> ids, because ids are unique per project - and it must carry everything else: values,
/// references and order, with one undo step for the whole thing.
/// </remarks>
public sealed class ModelCopyTests
{
    [Fact]
    public void ACopyHasTheSameNodesInTheSameOrder()
    {
        var original = PayrollSample.NewSampleModel();
        var copy = original.CopyTo(new Project(RulesLanguage.Concepts));

        Assert.Equal(Ids(original), Ids(copy));
    }

    [Fact]
    public void ACopyKeepsItsNameUnlessAnotherIsGiven()
    {
        var original = PayrollSample.NewSampleModel();
        original.Rename("Payroll");

        Assert.Equal("Payroll", original.CopyTo(new Project(RulesLanguage.Concepts)).Name);
        Assert.Equal("Copy", original.CopyTo(new Project(RulesLanguage.Concepts), "Copy").Name);
    }

    [Fact]
    public void ACopyCarriesPropertiesReferencesAndChildren()
    {
        var source = new Project(TestLanguage.Registry).NewModel("source");
        var tag = source.NewNode(TestLanguage.Tag);
        tag.Set("label", "urgent");
        source.AddRoot(tag);

        var rule = source.NewNode(TestLanguage.Rule);
        rule.Set("name", "Only");
        rule.Set("enabled", "false");
        rule.SetReference("tag", tag);
        rule.SetReference("inherits", rule.Id, "Only");
        source.AddRoot(rule);

        var body = source.NewChild(rule, "body", TestLanguage.Body);
        var step = source.NewChild(body, "steps", TestLanguage.Step);
        step.Set("index", 3);
        step.Set("status", "approved");
        source.NewChild(body, "notes", TestLanguage.Note).Set("text", "note");

        var copy = source.CopyTo(new Project(TestLanguage.Registry));

        AssertSameModel(source, copy);

        var copiedRule = copy.Project.FindNode(rule.Id)!;

        Assert.Equal("Only", copiedRule.GetOrNull("name"));
        Assert.Equal("false", copiedRule.GetOrNull("enabled"));
        Assert.Equal(tag.Id, copiedRule.GetReference("tag")!.Value.Target);
        Assert.Equal("Only", copiedRule.GetReference("inherits")!.Value.ResolveInfo);
        Assert.Equal(3, copiedRule.GetChild("body")!.GetChild("steps")!.GetInt("index"));
    }

    [Fact]
    public void ACopyDrawsTheSamePicture()
    {
        // The strongest thing that can be said about an exact copy without comparing fields one at a time.
        var original = PayrollSample.NewSampleModel();
        var copy = original.CopyTo(new Project(RulesLanguage.Concepts));
        var notations = RulesLanguage.CreateNotations(RulesLanguage.Concepts);

        Assert.Equal(
            LayoutSnapshot.Render(original, notations, width: 60),
            LayoutSnapshot.Render(copy, notations, width: 60));
    }

    [Fact]
    public void ACopyIsIndependentOfTheOriginal()
    {
        var original = PayrollSample.NewSampleModel();
        var copy = original.CopyTo(new Project(RulesLanguage.Concepts));

        var originalRule = original.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0];
        var copyRule = copy.Project.FindNode(originalRule.Id)!;

        copyRule.Set("name", "Changed");

        Assert.NotEqual("Changed", originalRule.GetOrNull("name"));

        originalRule.Set("name", "Other");

        Assert.Equal("Changed", copyRule.GetOrNull("name"));
    }

    [Fact]
    public void ACopyLivesInTheTargetProjectAndNotInTheOriginalOne()
    {
        var original = PayrollSample.NewSampleModel();
        var target = new Project(RulesLanguage.Concepts);
        var copy = original.CopyTo(target);

        Assert.Same(target, copy.Project);
        Assert.Same(original.Project, original.Roots[0].Model.Project);
        Assert.Single(original.Project.Models);
        Assert.Single(target.Models);

        // The ids are the same, and each project answers with its own node for them.
        var id = original.Roots[0].Id;

        Assert.Same(original.Roots[0], original.Project.FindNode(id));
        Assert.NotSame(original.Roots[0], target.FindNode(id));
    }

    [Fact]
    public void CopyingIntoTheProjectItCameFromFailsAndLeavesNothingBehind()
    {
        // A copy keeps the ids, so it can only live where they are still free. The failure is clean: the
        // half-built model is taken out of the project again rather than left there empty.
        var original = PayrollSample.NewSampleModel();
        var models = original.Project.Models.Count;

        Assert.Throws<ModelInvariantException>(() => original.CopyTo(original.Project));
        Assert.Equal(models, original.Project.Models.Count);

        // ...and the original is untouched by the attempt.
        Assert.NotEmpty(original.Roots);
    }

    [Fact]
    public void CopyingAnEmptyModelCopiesNoNodes()
    {
        var empty = new Project(TestLanguage.Registry).NewModel("empty");
        var copy = empty.CopyTo(new Project(TestLanguage.Registry));

        Assert.Empty(copy.Roots);
        Assert.Equal(0, copy.Project.NodeCount);
    }

    [Fact]
    public void APlaceNamesWhereANodeStands()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var rule = ruleset.GetChildren(RulesLanguage.RulesSlot)[0];

        Assert.Equal($"root[0]", NodePlace.Of(ruleset).ToString());
        Assert.True(NodePlace.Of(ruleset).IsRoot);
        Assert.Equal(ruleset.Id, NodePlace.Of(rule).Parent);
        Assert.Equal(RulesLanguage.RulesSlot, NodePlace.Of(rule).Slot);
        Assert.Equal(0, NodePlace.Of(rule).Index);
        Assert.False(NodePlace.Of(rule).IsRoot);
    }

    [Fact]
    public void AnInsertedCopyStandsRightAfterTheOriginal()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        var copy = body.Model.InsertCopy(steps[0], body, "steps", 1);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal(steps[0].Id, after[0].Id);
        Assert.Equal(copy.Id, after[1].Id);
        Assert.Equal("a", after[1].GetOrNull("text"));
        Assert.NotEqual(steps[0].Id, copy.Id);
    }

    [Fact]
    public void ACopyCannotBeTakenIntoItsOwnSubtree()
    {
        // Measured the hard way: `CopyFresh` walks the source subtree, so a copy of an ancestor contains the place it is
        // going into and the recursion never ends - a hang from a public API, not a wrong answer. `InsertChild` refuses
        // the same shape for a node, and the copy has to refuse it for its source.
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");
        var detail = body.Model.NewChild(steps[0], "detail", TestLanguage.Step);

        var before = body.Model.Nodes.Count();

        var exception = Assert.Throws<ModelInvariantException>(
            () => body.Model.InsertCopy(steps[0], detail, "detail", 0));

        Assert.Contains("its own subtree", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, body.Model.Nodes.Count());

        // A node into itself is the same refusal.
        Assert.Throws<ModelInvariantException>(() => body.Model.InsertCopy(steps[0], steps[0], "detail", 0));
    }
    [Fact]
    public void AnInsertedCopyCarriesTheWholeSubtreeWithNewIdentities()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");
        var detail = body.Model.NewChild(steps[1], "detail", TestLanguage.Step);

        detail.Set("text", "inside");

        var copy = body.Model.InsertCopy(steps[1], body, "steps", 0);
        var copiedDetail = copy.GetChild("detail");

        Assert.NotNull(copiedDetail);
        Assert.Equal("inside", copiedDetail.GetOrNull("text"));
        Assert.NotEqual(detail.Id, copiedDetail.Id);
        Assert.NotEqual(steps[1].Id, copy.Id);

        // Nothing below the copy is shared, so editing the copy leaves the original alone.
        copiedDetail.Set("text", "changed");

        Assert.Equal("inside", detail.GetOrNull("text"));
    }

    [Fact]
    public void AnInsertedCopyIsOneUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");
        var model = body.Model;
        using var history = new UndoHistory(model);

        model.InsertCopy(steps[0], body, "steps", 1);

        Assert.Equal(3, body.GetChildren("steps").Count);
        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());
        Assert.Equal(2, body.GetChildren("steps").Count);
        Assert.Equal(["a", "b"], [.. body.GetChildren("steps").Select(each => each.GetOrNull("text"))]);

        Assert.True(history.Redo());
        Assert.Equal(3, body.GetChildren("steps").Count);
    }

    [Fact]
    public void AnInsertedCopyKeepsTheReferencesOfTheOriginal()
    {
        var model = TestLanguage.NewModel();
        var tag = model.NewNode(TestLanguage.Tag);

        model.AddRoot(tag);

        var first = model.NewNode(TestLanguage.Rule);
        first.Set("name", "First");
        first.SetReference("tag", tag);

        var second = model.NewNode(TestLanguage.Rule);

        model.AddRoot(first);
        model.AddRoot(second);

        var copy = model.InsertCopy(first, second, "extends", 0);

        Assert.Same(tag, copy.GetReferenceTarget("tag"));
        Assert.Equal("First", copy.GetOrNull("name"));
    }

    [Fact]
    public void ASingleValuedSlotThatIsFullRefusesACopy()
    {
        var (outer, _) = RuleExtendingRule();

        // `extends` holds one rule already, so a copy has nowhere to go.
        Assert.Throws<ModelInvariantException>(
            () => outer.Model.InsertCopy(outer, outer, "extends", 1));
    }

    /// <summary>A rule sitting in another rule's <c>extends</c> slot, which is the smallest nested rule.</summary>
    private static (Node Outer, Node Inner) RuleExtendingRule()
    {
        var outer = TestEditor.BareRule();

        return (outer, outer.Model.NewChild(outer, "extends", TestLanguage.Rule));
    }

    /// <summary>Every node id of one model, in document order, depth first.</summary>
    private static List<NodeId> Ids(Model model)
    {
        var ids = new List<NodeId>();

        foreach (var root in model.Roots)
        {
            Collect(root, ids);
        }

        return ids;

        static void Collect(Node node, List<NodeId> into)
        {
            into.Add(node.Id);

            foreach (var child in node.ChildrenInDocumentOrder())
            {
                Collect(child, into);
            }
        }
    }

    /// <summary>Asserts that two models are the same document, node by node, in the same order.</summary>
    private static void AssertSameModel(Model original, Model copy)
    {
        Assert.Equal(Ids(original), Ids(copy));

        foreach (var id in Ids(original))
        {
            var was = original.Project.FindNode(id)!;
            var now = copy.Project.FindNode(id)!;

            Assert.Equal(was.Concept.Key, now.Concept.Key);
            Assert.Equal(NodePlace.Of(was), NodePlace.Of(now));

            foreach (var declaration in was.Concept.Slots)
            {
                switch (declaration)
                {
                    case PropertySlot property:
                        Assert.Equal(was.GetOrNull(property.Name), now.GetOrNull(property.Name));

                        break;

                    case ReferenceSlot reference:
                        Assert.Equal(was.GetReference(reference.Name), now.GetReference(reference.Name));

                        break;

                    case ChildSlot child:
                        Assert.Equal(
                            was.GetChildren(child.Name).Select(each => each.Id),
                            now.GetChildren(child.Name).Select(each => each.Id));

                        break;
                }
            }
        }
    }
}
