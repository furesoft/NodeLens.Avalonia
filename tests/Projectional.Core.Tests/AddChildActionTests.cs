using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Filling an empty slot with a concept the caller names, which is what a declared intention needs and what
/// <see cref="InsertChildAction"/> cannot do - it takes the slot's default.
/// </summary>
/// <remarks>
/// The fixture is a rule's <c>condition</c>, which is declared over the abstract <c>Expressions.Expression</c>: seven
/// concrete concepts can stand there, so "the named one was created" is a real claim rather than one the default
/// would satisfy by accident.
/// </remarks>
public sealed class AddChildActionTests
{
    [Fact]
    public void TheNamedConceptIsCreatedRatherThanTheDefault()
    {
        var rule = EmptyRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new AddChildAction(rule.Id, "condition", ExpressionsConceptKeys.BoolLiteral));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(ExpressionsConceptKeys.BoolLiteral, rule.GetChild("condition")!.Concept.Key);
        Assert.Equal(CellAddress.OfElement(rule.Id, "condition", 0), result.Selection.Caret.Address);
    }

    [Fact]
    public void AFilledSlotIsRefused()
    {
        var rule = EmptyRule();
        using var session = TestEditor.Open(rule);

        session.Apply(new AddChildAction(rule.Id, "condition", ExpressionsConceptKeys.BoolLiteral));

        var again = session.Apply(new AddChildAction(rule.Id, "condition", ExpressionsConceptKeys.IntLiteral));

        // A second child in a single-valued slot is a replacement, which is a different entry.
        Assert.Equal(EditOutcome.Rejected, again.Outcome);
    }

    [Fact]
    public void AConceptTheSlotDoesNotAcceptIsRefused()
    {
        var rule = EmptyRule();
        using var session = TestEditor.Open(rule);

        var result = session.Apply(new AddChildAction(rule.Id, "condition", RulesConceptKeys.Rule));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Empty(rule.GetChildren("condition"));
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsRefused()
    {
        var rule = EmptyRule();
        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Rejected, session.Apply(new AddChildAction(rule.Id, "nothing", RulesConceptKeys.Rule)).Outcome);
    }

    private static Node EmptyRule()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var rule = model.NewNode(RulesConceptKeys.Rule);

        model.AddRoot(rule);
        rule.Set("name", "Approved");

        return rule;
    }
}
