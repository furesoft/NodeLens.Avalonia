using Projectional.Core;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Merging two edits of one document by node id, against the version both started from.</summary>
/// <remarks>
/// The fixture is <see cref="TestLanguage"/> because a merge needs what a document has: roots, a slot of many children,
/// a single-child slot to move into, properties and a reference. Every test derives its two sides from one base model,
/// which is what a branch is - the same ids, edited apart.
/// </remarks>
public sealed class ThreeWayMergeTests
{
    [Fact]
    public void TwoSidesThatEditedDifferentSlotsMergeToTheUnion()
    {
        // The gate in one test: two branches, different parts of one document, one result.
        var (origin, ours, theirs) = Triple("one", "two");

        Step(ours, 0).Set("text", "one-edited");
        Step(theirs, 1).Set("text", "two-edited");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal(["one-edited", "two-edited"], Texts(result.Model!));
    }

    [Fact]
    public void TwoAdditionsToOneListBothSurviveOursFirst()
    {
        // What a text merge calls a conflict because the lines are adjacent. Two insertions into one list are two
        // insertions, and the anchor decides where each goes - ours before theirs, each in its own order.
        var (origin, ours, theirs) = Triple("keep");

        Add(ours, "ours");
        Add(theirs, "theirs");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal(["keep", "ours", "theirs"], Texts(result.Model!));
    }

    [Fact]
    public void ANodeKeepsItsIdentityThroughTheMerge()
    {
        var (origin, ours, theirs) = Triple("one");

        Step(theirs, 0).Set("text", "edited");

        var id = Step(origin, 0).Id;
        var merged = ThreeWayMerge.Merge(origin, ours, theirs).Model!;

        // The id is what a second merge, a reference and the caret are keyed by, so the result may not renumber.
        Assert.Equal(id, Step(merged, 0).Id);
    }

    [Fact]
    public void TheSamePropertyWrittenToTwoValuesIsAConflict()
    {
        var (origin, ours, theirs) = Triple("one");

        Step(ours, 0).Set("text", "ours");
        Step(theirs, 0).Set("text", "theirs");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.False(result.Succeeded);
        Assert.Null(result.Model);

        var conflict = Assert.Single(result.Conflicts);

        Assert.Equal(MergeConflictKind.PropertyWrittenDifferently, conflict.Kind);
        Assert.Equal("text", conflict.Slot);
        Assert.Equal("ours", conflict.Ours);
        Assert.Equal("theirs", conflict.Theirs);
        Assert.Equal(Step(origin, 0).Id, conflict.Node);
    }

    [Fact]
    public void TheSamePropertyWrittenToTheSameValueIsNotAConflict()
    {
        // Two branches that fixed the same typo did the same thing, and a merge that refused that would be annoying
        // rather than careful.
        var (origin, ours, theirs) = Triple("one");

        Step(ours, 0).Set("text", "fixed");
        Step(theirs, 0).Set("text", "fixed");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal(["fixed"], Texts(result.Model!));
    }

    [Fact]
    public void DeletingWhatTheOtherSideEditedIsAConflict()
    {
        var (origin, ours, theirs) = Triple("one", "two");

        Remove(ours, 0);
        Step(theirs, 0).Set("text", "edited");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.False(result.Succeeded);
        Assert.Equal(MergeConflictKind.DeletedAndEdited, Assert.Single(result.Conflicts).Kind);
    }

    [Fact]
    public void DeletingWhatBothSidesDeletedIsNoConflict()
    {
        var (origin, ours, theirs) = Triple("one", "two");

        Remove(ours, 0);
        Remove(theirs, 0);

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal(["two"], Texts(result.Model!));
    }

    [Fact]
    public void DeletingWhatTheOtherSideLeftAloneDeletesIt()
    {
        var (origin, ours, theirs) = Triple("one", "two");

        Remove(theirs, 0);

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal(["two"], Texts(result.Model!));
    }

    [Fact]
    public void AMoveOnOneSideSurvivesAndTheOtherSidesEditComesWithIt()
    {
        var (origin, ours, theirs) = Triple("one", "two");

        // Ours moves the second step below the first, theirs edits it: the two intents do not overlap.
        var moved = Step(ours, 1);

        Step(ours, 0).InsertChild("detail", 0, moved);

        Step(theirs, 1).Set("text", "edited");

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.True(result.Succeeded, Describe(result));

        var merged = result.Model!;
        var first = Step(merged, 0);

        Assert.Equal("edited", first.GetChild("detail")!.GetOrNull("text"));
        Assert.Single(merged.Roots[0].GetChild("body")!.GetChildren("steps"));
    }

    [Fact]
    public void TheMergedModelSavesAndLoadsBackByteIdentically()
    {
        // A merge that produced a model the writer cannot write twice would not be a document at all.
        var (origin, ours, theirs) = Triple("one", "two");

        Step(ours, 0).Set("text", "one-edited");
        Add(theirs, "three");

        var merged = ThreeWayMerge.Merge(origin, ours, theirs).Model!;

        var folder = Directory.CreateTempSubdirectory("merge");

        try
        {
            var first = Path.Combine(folder.FullName, "merged.mps");
            var second = Path.Combine(folder.FullName, "again.mps");

            ModelFile.Save(merged, first);
            ModelFile.Save(ModelFile.Load(new Project(TestLanguage.Registry), first), second);

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void AReferenceThatTheMergeWouldLeaveDanglingIsAConflict()
    {
        // The failure a text merge cannot see: our side adds a reference to a rule, their side deletes that rule. Both
        // edits are innocent, and only the merge makes the pair broken - so the merge is not allowed to stay silent.
        var origin = TestLanguage.NewModel();
        var rule = origin.NewNode(TestLanguage.Rule);

        var inherited = origin.NewChild(rule, "extends", TestLanguage.Rule);

        var ours = Copy(origin);
        var theirs = Copy(origin);

        Rule(ours).SetReference("self", Rule(ours).GetChild("extends")!);
        Rule(theirs).RemoveChild("extends", Rule(theirs).GetChild("extends")!);

        var result = ThreeWayMerge.Merge(origin, ours, theirs);

        Assert.False(result.Succeeded);

        var conflict = Assert.Single(result.Conflicts);

        Assert.Equal(MergeConflictKind.DanglingReference, conflict.Kind);
        Assert.Equal("self", conflict.Slot);
        Assert.Equal(inherited.Id, Rule(origin).GetChild("extends")!.Id);
    }

    /// <summary>A base document with one step per text, and a copy for each side.</summary>
    private static (Model Origin, Model Ours, Model Theirs) Triple(params string[] texts)
    {
        var origin = TestLanguage.NewModel();
        var rule = origin.NewNode(TestLanguage.Rule);
        var body = origin.NewChild(rule, "body", TestLanguage.Body);

        foreach (var text in texts)
        {
            Add(body, text);
        }

        return (origin, Copy(origin), Copy(origin));
    }

    /// <summary>A copy of one model in a project of its own, which is what a branch is.</summary>
    private static Model Copy(Model model) =>
        model.CopyTo(new Project(TestLanguage.Registry), model.Name);

    private static Node Rule(Model model) => model.Roots[0];

    private static Node BodyNode(Model model) => Rule(model).GetChild("body")!;

    private static Node Step(Model model, int index) => BodyNode(model).GetChildren("steps")[index];

    private static IReadOnlyList<string?> Texts(Model model) =>
        [.. BodyNode(model).GetChildren("steps").Select(step => step.GetOrNull("text"))];

    private static void Add(Model model, string text) => Add(BodyNode(model), text);

    private static Node Add(Node body, string text)
    {
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", text);

        return step;
    }

    private static void Remove(Model model, int index) =>
        BodyNode(model).RemoveChild("steps", Step(model, index));

    private static string Describe(MergeResult result) =>
        result.Succeeded ? "merged" : string.Join("; ", result.Conflicts.Select(conflict => conflict.Describe()));
}
