using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a host needs to build a representation switcher: which concepts of the open document can be
/// drawn more than one way, which one is drawn now, and how to ask for another.
/// </summary>
public sealed class NotationAlternativesTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    [Fact]
    public void OnlyTheConceptsThatDeclareMoreThanOneNotationAreOffered()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var alternative = Assert.Single(session.Alternatives());

        Assert.Equal(RulesConceptKeys.Ruleset, alternative.ConceptKey);
        Assert.Equal(["default", RulesLanguage.TableVariant], alternative.Variants);
    }

    [Fact]
    public void AConceptWithASingleNotationIsNotOffered()
    {
        // A rule declares one notation only, so a menu built from this document must not offer a choice
        // for it - the list of alternatives is what says so.
        Assert.Equal(["default"], Notations.VariantsOf(RulesConceptKeys.Rule));

        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        Assert.DoesNotContain(session.Alternatives(), a => a.ConceptKey == RulesConceptKeys.Rule);
    }

    [Fact]
    public void TheDefaultVariantIsTheOneShownWhileNobodyAskedForAnother()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        Assert.Equal(Notation.DefaultVariant, Assert.Single(session.Alternatives()).Selected);
    }

    [Fact]
    public void ChoosingAVariantIsWhatTheAlternativesThenReportAndWhatIsDrawn()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        session.ChooseVariant(RulesConceptKeys.Ruleset, RulesLanguage.TableVariant);

        Assert.Equal(RulesLanguage.TableVariant, Assert.Single(session.Alternatives()).Selected);
        Assert.Contains("rule     then", LayoutSnapshot.Render(session.Layout, width: 60), StringComparison.Ordinal);

        session.ChooseVariant(RulesConceptKeys.Ruleset, Notation.DefaultVariant);

        Assert.Equal(Notation.DefaultVariant, Assert.Single(session.Alternatives()).Selected);
        Assert.Contains("ruleset Payroll {", LayoutSnapshot.Render(session.Layout, width: 60), StringComparison.Ordinal);
    }

    [Fact]
    public void ChoosingAVariantLeavesTheHistoryAlone()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        session.ChooseVariant(RulesConceptKeys.Ruleset, RulesLanguage.TableVariant);

        Assert.Equal(0, session.History.Revision);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ChoosingAVariantOfAConceptTheDocumentDoesNotHoldFails()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var error = Assert.Throws<ArgumentException>(
            () => session.ChooseVariant("NoSuchConcept", Notation.DefaultVariant));

        Assert.Contains("NoSuchConcept", error.Message, StringComparison.Ordinal);
        Assert.Equal("conceptKey", error.ParamName);
    }

    [Fact]
    public void ChoosingAVariantTheConceptDoesNotDeclareFails()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var error = Assert.Throws<ArgumentException>(
            () => session.ChooseVariant(RulesConceptKeys.Ruleset, "diagram"));

        Assert.Contains("diagram", error.Message, StringComparison.Ordinal);
        Assert.Equal("variant", error.ParamName);
    }
}
