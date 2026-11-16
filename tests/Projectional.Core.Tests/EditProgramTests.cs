using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A program is a list of edits that are applied as one: the shape a repair takes when one property write
/// is not enough.
/// </summary>
/// <remarks>
/// The construction rules are the interesting part here, because they are what stops a program from being
/// something else: no nesting, no nulls, and no direct application - each of those would produce a program
/// whose steps ran against addresses the step before it invalidated.
/// </remarks>
public sealed class EditProgramTests
{
    [Fact]
    public void AProgramKeepsTheOrderItsStepsWereGiven()
    {
        var rule = TestEditor.BareRule();
        var name = CellAddress.OfSlot(rule.Id, "name");

        var program = EditProgram.Of(
            new SetPropertyAction(name, "first"),
            new SetPropertyAction(name, "second"));

        Assert.Equal(2, program.Steps.Count);
        Assert.Equal("first", Assert.IsType<SetPropertyAction>(program.Steps[0]).Text);
        Assert.Equal("second", Assert.IsType<SetPropertyAction>(program.Steps[1]).Text);
    }

    [Fact]
    public void AProgramCannotContainAProgram()
    {
        // The sequence is flat by construction; flattening silently would hide the mistake.
        var name = CellAddress.OfSlot(TestEditor.BareRule().Id, "name");
        var inner = EditProgram.Of(new SetPropertyAction(name, "x"));

        Assert.Throws<ArgumentException>(() => EditProgram.Of(inner));
    }

    [Fact]
    public void ANullStepIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => EditProgram.Of([null!]));
    }

    [Fact]
    public void AnEmptyProgramIsLegalAndHasNoSteps()
    {
        Assert.Empty(EditProgram.Of().Steps);
    }

    [Fact]
    public void AProgramCannotBeAppliedWithoutASession()
    {
        // Only the session rebuilds the tree between steps, so applying one anywhere else would run the
        // later steps against stale addresses. Refusing is the only honest answer.
        var program = EditProgram.Of(
            new SetPropertyAction(CellAddress.OfSlot(TestEditor.BareRule().Id, "name"), "x"));

        Assert.Throws<EditingException>(() => program.Apply(null!));
    }
}
