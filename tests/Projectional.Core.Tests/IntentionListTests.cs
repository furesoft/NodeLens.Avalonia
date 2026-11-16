using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The offers at one caret: host before language before quick fixes, absent entries dropped, ids unique.
/// </summary>
/// <remarks>
/// The order and the de-duplication are the two things a surface depends on, and both are invisible in a single
/// source - which is why every test here publishes at least two of them.
/// </remarks>
public sealed class IntentionListTests
{
    [Fact]
    public void NothingRegisteredAndNothingFoundOffersNothing()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void HostEntriesComeBeforeLanguageEntriesEvenWhenRegisteredLast()
    {
        // The registration order must not be the visible order: a language package is typically wired first,
        // and the host's own commands still belong at the top.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(Named("language.one")), IntentionSource.Language);
        session.AddIntentions(new StubIntentions(Named("host.one")));

        Assert.Equal(["host.one", "language.one"], IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AnAbsentIntentionIsNotOffered()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(
            Named("here"),
            Named("elsewhere").With(IntentionAvailability.Absent)));

        Assert.Equal("here", Assert.Single(IntentionList.For(session)).Id);
    }

    [Fact]
    public void ADisabledIntentionIsOfferedWithItsReason()
    {
        // "This language knows about this caret and here is why it cannot act" is the useful half of the
        // message, so a disabled entry stays in the list rather than disappearing.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(
            Named("blocked").With(IntentionAvailability.Disabled("nothing to wrap here"))));

        var offer = Assert.Single(IntentionList.For(session));

        Assert.True(offer.IsDisabled);
        Assert.Equal("nothing to wrap here", offer.Availability.Reason);
    }

    [Fact]
    public void TwoSourcesOfferingOneIdAreListedOnceAndTheFirstWins()
    {
        // The catalogues give a standard repair and its intention the same id on purpose, so the user never
        // reads one sentence twice.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(Named("shared", label: "from the host")));
        session.AddIntentions(new StubIntentions(Named("shared", label: "from the language")), IntentionSource.Language);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("from the host", offer.Label);
    }

    [Fact]
    public void QuickFixesAreListedAfterTheIntentions()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule, "the name says nothing");

        session.AddIntentions(new StubIntentions(Named("intent")));
        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(RenameFix(finding, "walked"));

        Assert.Equal(["intent", "test.rename"], IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AnIntentionHidesAFixWithTheSameId()
    {
        // One id per operation: the intention comes first, so the fix that names the same operation is not
        // shown a second time.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule, "the name says nothing");

        session.AddIntentions(new StubIntentions(Named("test.rename", label: "Rename (the intention)")));
        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(RenameFix(finding, "walked"));

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Rename (the intention)", offer.Label);
        Assert.Null(offer.Fix);
    }

    [Fact]
    public void TheListIsDerivedAfreshOnEveryCall()
    {
        // Nothing is cached, because an offer describes a caret that may already have moved.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var provider = new StubIntentions(Named("one"));

        session.AddIntentions(provider);

        Assert.Single(IntentionList.For(session));
        Assert.Single(IntentionList.For(session));

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public void EveryProviderSeesTheCellTheCaretIsOn()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var provider = new StubIntentions(Named("one"));

        session.AddIntentions(provider);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "body")));
        IntentionList.For(session);

        var context = provider.LastContext!;

        Assert.Equal(CellAddress.OfSlot(rule.Id, "body"), context.Address);
        Assert.Equal(rule.Id, context.Node?.Id);
        Assert.True(context.IsEmptySlot);
    }

    [Fact]
    public void AnEmptySlotAnswersWithTheNodeThatOwnsIt()
    {
        // An empty slot has no concept of its own, so "insert a rule here" is an intention about the parent.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "body")));
        session.AddIntentions(TestIntentions.ForRules);

        Assert.Equal(
            [TestIntentions.RenameId, TestIntentions.DeleteId],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void ACaretOffTheRulesOffersNothingFromTheLanguage()
    {
        // The fixture declares its entries once and answers "absent" everywhere else, so a whole catalogue can
        // be handed back and filtered by the list.
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.Empty(IntentionList.For(session));

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        Assert.Equal(2, IntentionList.For(session).Count);
    }

    [Fact]
    public void AddIntentionsRefusesWhatItCannotUse()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Throws<ArgumentNullException>(() => session.AddIntentions(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.AddIntentions(new StubIntentions(), (IntentionSource)99));
    }

    [Fact]
    public void ANullSessionIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => IntentionList.For(null!));
    }

    private static Intention Named(string id, string label = "Do it") =>
        new(id, label, "Does something useful.", _ => null);

    private static DiagnosticFix RenameFix(Diagnostic finding, string text) =>
        new(finding, "test.rename", "Rename", _ => null);

    private static Diagnostic Finding(Node node, string message) =>
        new(DiagnosticSeverity.Warning, node.Id, message, role: "name", range: CharacterRange.Of(0, 4));

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
