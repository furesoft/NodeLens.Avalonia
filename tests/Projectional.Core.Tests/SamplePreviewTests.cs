using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Samples.Intentions;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The entries the shipped packages offer, seen through the preview: what the language and the sample actually
/// put in front of a user has to be previewable, not only the intentions a test declares.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes are worth pinning, because they are the two the design gives as its reason for existing. A
/// <b>rename</b> changes widths, so the ghost has to draw the new name in place - an overlay would draw it at the
/// old positions and lie about the layout. A <b>conversion</b> is the case where no sentence answers the question:
/// wrapping in parentheses and negating both change the shape of the tree, and only the projection shows what that
/// looks like.
/// </para>
/// <para>
/// The rename here is the samples' own repair (<c>RULES001</c>) rather than a declared intention, which is also
/// what proves the fix path previews: a fix builds its action against the model it is handed, so on a copy it edits
/// the copy's node rather than reaching back into the live document.
/// </para>
/// </remarks>
public sealed class SamplePreviewTests
{
    /// <summary>The room a page has in these tests, in layout units.</summary>
    private static readonly LayoutSize Viewport = new(80, 20);

    [Fact]
    public void ARenameRepairPreviewsTheNameItWouldWrite()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];

        // A second rule wearing the first one's name, which is exactly what the sample check reports.
        var duplicate = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        duplicate.Set("name", "Approved");

        var fix = Assert.Single(DuplicateRuleNames.Of(model));

        using var session = new EditorSession(
            ruleset,
            RulesLanguage.CreateNotations(model.Concepts),
            emphasis: RulesLanguage.Emphasis,
            typing: RulesLanguage.TypingRules);

        session.Markers = new MarkerSet([fix.Diagnostic]);
        session.Fixes = new FixSet(fix);
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(duplicate.Id, "name"))));

        using var preview = new OfferPreview(session);

        preview.Hover(Offer(session, fix.Id));
        preview.Tick(PreviewDelay.Default.Preview);

        var ghost = preview.Ghost(Viewport, 0, 0);

        Assert.NotNull(ghost);
        Assert.Contains("Approved2", Text(ghost));
        Assert.True(ghost.Marks.Changed > 0);

        // The live document still says what the user wrote; only the copy was renamed.
        Assert.Equal("Approved", duplicate.GetOrNull("name"));
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void WrapInParenthesesPreviewsTheWrapper()
    {
        var (session, _, condition) = Document();

        using (session)
        using (var preview = new OfferPreview(session))
        {
            Caret(session, condition);

            preview.Hover(Offer(session, ExpressionsIntentions.WrapInParenthesesId));
            preview.Tick(PreviewDelay.Default.Preview);

            var ghost = preview.Ghost(Viewport, 0, 0);

            Assert.NotNull(ghost);
            Assert.Contains("(", Text(ghost));
            Assert.Contains(")", Text(ghost));

            // The wrapper is a node that does not exist in the live document, so it is an addition.
            Assert.Equal(1, ghost.Marks.Added);
            Assert.Equal(ExpressionsConceptKeys.Binary, condition.Concept.Key);
        }
    }

    [Fact]
    public void NegatePreviewsTheOperator()
    {
        var (session, _, condition) = Document();

        using (session)
        using (var preview = new OfferPreview(session))
        {
            Caret(session, condition);

            preview.Hover(Offer(session, ExpressionsIntentions.NegateId));
            preview.Tick(PreviewDelay.Default.Preview);

            var ghost = preview.Ghost(Viewport, 0, 0);

            Assert.NotNull(ghost);
            Assert.Contains("!", Text(ghost));

            // Nothing about the document on screen changed, including the condition's own concept.
            Assert.Equal(ExpressionsConceptKeys.Binary, condition.Concept.Key);
            Assert.Equal(0, session.History.UndoDepth);
        }
    }

    [Fact]
    public void AcceptThisRulePreviewsTheOutcome()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));
        rule.Set("outcome", RuleOutcomes.Reject);

        using var session = new EditorSession(
            model.Roots[0],
            RulesLanguage.CreateNotations(model.Concepts),
            emphasis: RulesLanguage.Emphasis,
            typing: RulesLanguage.TypingRules);

        session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        using var preview = new OfferPreview(session);

        preview.Hover(Offer(session, RulesIntentions.AcceptId));
        preview.Tick(PreviewDelay.Default.Preview);

        var ghost = preview.Ghost(Viewport, 0, 0);

        Assert.NotNull(ghost);
        Assert.Contains(RuleOutcomes.Accept, Text(ghost));
        Assert.DoesNotContain(RuleOutcomes.Reject, Text(ghost));

        // The live rule still rejects: the outcome the ghost draws is the copy's.
        Assert.Equal(RuleOutcomes.Reject, rule.GetOrNull("outcome"));
    }

    /// <summary>The payroll sample's condition, opened with the expressions' intentions registered.</summary>
    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(model.Concepts));
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        return (session, rule, rule.GetChild("condition")!);
    }

    /// <summary>Puts the caret on the first cell <paramref name="node"/> owns.</summary>
    /// <remarks>
    /// A node address is not a caret place by itself: only some notations draw a cell carrying the node's own
    /// address, so the places are walked and the first one whose owner is the node is taken.
    /// </remarks>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    /// <summary>Opens the offers at the caret and answers the one <paramref name="id"/> names.</summary>
    private static Intention Offer(EditorSession session, string id)
    {
        session.Apply(new OfferAction());

        return session.Offered!.Entries.First(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }

    /// <summary>The ghost's runs as one string, which is what "the copy says this" means.</summary>
    private static string Text(GhostFrame ghost) => string.Concat(ghost.TextRuns.Select(run => run.Text));
}
