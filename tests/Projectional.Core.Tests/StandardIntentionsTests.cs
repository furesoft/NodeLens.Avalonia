using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Intentions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The entries the framework ships for every language: rename, wrap in something, reset to the default.
/// </summary>
/// <remarks>
/// <para>
/// What these pin is that the catalogue is derived from the <em>shape</em> of the caret's place rather than from
/// knowledge of a language: a node with a name slot, a property holding something other than its declared default,
/// and a node some concept could surround. The wrap search is the one worth the most attention, because it is the
/// entry the catalogue exists for - "surround with <c>if</c>" is not a refactoring here, it is what the language's
/// own declarations answer.
/// </para>
/// <para>
/// The two absence rules are pinned as well: an entry that would change nothing is not offered (a name already
/// selected, a property already holding its default), which is what keeps a bulb from lighting for a no-op.
/// </para>
/// </remarks>
public sealed class StandardIntentionsTests
{
    [Fact]
    public void RenameIsOfferedOnANodeWithAName()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        Assert.Contains(StandardIntentions.RenameId, Ids(session));

        var result = session.Apply(Offer(session, StandardIntentions.RenameId));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);
        Assert.True(session.Selection.HasText);
        Assert.Equal(rule.GetOrNull("name")!.Length, session.Selection.End);
    }

    [Fact]
    public void RenameIsNotOfferedOnANodeWithoutAName()
    {
        var step = TestEditor.BareStep("walk");
        using var session = Session(step);

        Assert.DoesNotContain(StandardIntentions.RenameId, Ids(session));
    }

    [Fact]
    public void RenameIsNotOfferedOnANameThatIsAlreadySelected()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));
        session.Apply(new SelectValueAction());

        Assert.DoesNotContain(StandardIntentions.RenameId, Ids(session));
    }

    [Fact]
    public void ResetWritesTheDeclaredDefaultOverWhatIsThere()
    {
        var rule = TestEditor.BareRule();

        rule.Set("name", "Approved");

        using var session = Session(rule);
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        var offer = Offer(session, StandardIntentions.ResetId);

        Assert.Equal("Reset to \"unnamed\"", offer.Label);

        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void ResetIsNotOfferedOnAPropertyHoldingItsDefault()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        Assert.DoesNotContain(StandardIntentions.ResetId, Ids(session));
    }

    [Fact]
    public void EveryConceptThatCouldSurroundTheNodeOffersAWrap()
    {
        var (session, _, condition) = Document();

        using (session)
        {
            Caret(session, condition);

            var wrap = Wraps(session);

            // The expressions language's own groupings, found from the declarations and not from a list.
            Assert.Contains(StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Parenthesized, wrap);
            Assert.Contains(StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Unary, wrap);

            // One id per concept: a list keyed by id would otherwise hold a single wrap entry.
            Assert.Equal(wrap.Count, wrap.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void AWrapIsNotOfferedForARoot()
    {
        // A wrapper has to take the node's place, and a root is not in one.
        var model = PayrollSample.NewSampleModel();
        using var session = Session(model.Roots[0]);

        Assert.Empty(Wraps(session));
    }

    [Fact]
    public void ApplyingAWrapEntrySurroundsTheNode()
    {
        var (session, rule, condition) = Document();

        using (session)
        {
            Caret(session, condition);

            Assert.Equal(
                EditOutcome.Applied,
                session.Apply(Offer(session, StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Parenthesized))
                    .Outcome);

            var wrapper = Assert.Single(rule.GetChildren("condition"));

            Assert.Equal(ExpressionsConceptKeys.Parenthesized, wrapper.Concept.Key);
            Assert.Equal(condition.Id, wrapper.GetChild("inner")!.Id);

            Assert.True(session.Undo());
            Assert.Equal(condition.Id, Assert.Single(rule.GetChildren("condition")).Id);
        }
    }

    /// <summary>Opens <paramref name="document"/> with the standard catalogue registered.</summary>
    private static EditorSession Session(Node document)
    {
        var session = new EditorSession(document, TestNotations.Registry);

        session.AddIntentions(StandardIntentions.Provider);

        return session;
    }

    /// <summary>The payroll sample's condition, opened with the standard catalogue registered.</summary>
    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(model.Concepts));
        session.AddIntentions(StandardIntentions.Provider);

        return (session, rule, rule.GetChild("condition")!);
    }

    /// <summary>Puts the caret on the first cell <paramref name="node"/> owns.</summary>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    /// <summary>The ids of everything offered at the caret, in list order.</summary>
    private static IReadOnlyList<string> Ids(EditorSession session) =>
        [.. IntentionList.For(session).Select(entry => entry.Id)];

    /// <summary>The wrap ids offered at the caret.</summary>
    private static IReadOnlyList<string> Wraps(EditorSession session) =>
        [.. Ids(session).Where(id => id.StartsWith(StandardIntentions.WrapPrefix, StringComparison.Ordinal))];

    /// <summary>The one entry with <paramref name="id"/>.</summary>
    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
}
