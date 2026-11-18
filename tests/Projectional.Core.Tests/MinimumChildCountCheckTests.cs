using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD011</c>: a list slot that holds nothing while its declaration asks for at least one child.
/// </summary>
/// <remarks>
/// The list half of <c>STD010</c>: a slot written <c>[1..n]</c> promises an element, one written <c>[0..n]</c>
/// promises nothing, and the same repair fills both - <see cref="InsertChildAction"/>, which is what <c>Enter</c>
/// runs on an empty slot.
/// </remarks>
public sealed class MinimumChildCountCheckTests
{
    private static MinimumChildCountCheck Check => new();

    [Fact]
    public void AMandatoryEmptyListIsReported()
    {
        var model = CardinalityLanguage.NewModel(out var holder);

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(MinimumChildCountCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal(CardinalityLanguage.RequiredSlot, finding.Role);
        Assert.Equal(holder.Id, finding.Target);

        // The concept the language offers in the list, which is the one Enter would create.
        Assert.Equal("Add \"Item\"", fix.Label);
    }

    [Fact]
    public void AListThatMayBeEmptyIsNotReported()
    {
        // A list with no minimum is a document in progress, not a hole.
        var body = TestLanguage.NewModel().NewNode(TestLanguage.Body);

        Assert.Empty(Check.Find(body.Model));
    }

    [Fact]
    public void AFilledListIsNotReported()
    {
        var model = CardinalityLanguage.NewModel(out var holder);

        model.NewChild(holder, CardinalityLanguage.RequiredSlot, CardinalityLanguage.Item).Set("name", "first");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ASlotThatMayStayEmptyIsNotReported()
    {
        // `0..1` is a single child that nobody promised, so the structure check is silent about it.
        var model = CardinalityLanguage.NewModel();

        Assert.Empty(new EmptyChildSlotCheck().Find(model));
    }

    [Fact]
    public void TheRepairAddsTheFirstItemAsEnterWould()
    {
        var model = CardinalityLanguage.NewModel(out var holder);
        var fix = Assert.Single(Check.Find(model));

        using var session = new EditorSession(model.Roots[0], CardinalityLanguage.Notations);

        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);

        var item = Assert.Single(holder.GetChildren(CardinalityLanguage.RequiredSlot));

        Assert.Equal(CardinalityLanguage.Item, item.Concept.Key);
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Empty(holder.GetChildren(CardinalityLanguage.RequiredSlot));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }
}
