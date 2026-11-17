using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The hover ladder driven all the way to a ghost: one offer, the edit it would make and a copy that edit was
/// applied to.
/// </summary>
/// <remarks>
/// <para>
/// The ladder itself is <see cref="PreviewStateTests"/>' business; what these pin is the join. Hovering must not
/// copy a document, the copy must be taken when the ladder asks for one, and the answer must be thrown away when
/// the hover ends - otherwise the preview leaks a document per pointer move.
/// </para>
/// <para>
/// The isolation test is the one that matters most: a preview is not undoable because it never happened, so the
/// live model, its version and its history have to be exactly as they were while a ghost is on screen.
/// </para>
/// </remarks>
public sealed class OfferPreviewTests
{
    /// <summary>The room a page has in these tests, in layout units.</summary>
    private static readonly LayoutSize Viewport = new(80, 20);

    [Fact]
    public void AnOfferBecomesAPreviewOnlyWhenTheLadderReachesIt()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));

            preview.Tick(PreviewDelay.Default.Description);

            Assert.Equal(PreviewStage.Tooltip, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));

            preview.Tick(PreviewDelay.Default.Preview);

            Assert.Equal(PreviewStage.Preview, preview.Stage);
            Assert.NotNull(preview.Ghost(Viewport, 0, 0));
        }
    }

    [Fact]
    public void TheGhostShowsTheEditAndLeavesTheLiveDocumentAlone()
    {
        var rule = TestEditor.BareRule();
        var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        using (session)
        using (var preview = new OfferPreview(session))
        {
            var version = session.Document.Model.Version;
            var drawing = LayoutSnapshot.Render(session.Layout, width: 60);

            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            var ghost = preview.Ghost(Viewport, 0, 0);

            Assert.NotNull(ghost);
            Assert.Contains("renamed", Text(ghost));
            Assert.True(ghost.Marks.Changed > 0);

            // The live document is untouched: same value, same version, no undo step, same drawing.
            Assert.Equal("unnamed", rule.GetOrNull("name"));
            Assert.Equal(version, session.Document.Model.Version);
            Assert.Equal(0, session.History.UndoDepth);
            Assert.Equal(drawing, LayoutSnapshot.Render(session.Layout, width: 60));
        }
    }

    [Fact]
    public void LeavingTheOfferDropsTheGhost()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            Assert.NotNull(preview.Ghost(Viewport, 0, 0));

            preview.Leave();

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Entry);
            Assert.False(preview.IsRunning);
            Assert.Null(preview.Ghost(Viewport, 0, 0));
        }
    }

    [Fact]
    public void LeavingTheListKeepsThePreviewOnlyForTheGracePeriod()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            preview.LeaveList();
            preview.Tick(PreviewDelay.Default.Grace / 2);

            // A pointer on its way somewhere keeps the answer on screen, and its copy with it.
            Assert.Equal(PreviewStage.Preview, preview.Stage);
            Assert.NotNull(preview.Ghost(Viewport, 0, 0));

            // The clock was paused while the pointer was away, so coming back resumes instead of climbing.
            preview.Hover(Offer(session, TestIntentions.RenameId));

            Assert.Equal(PreviewStage.Preview, preview.Stage);

            preview.LeaveList();
            preview.Tick(PreviewDelay.Default.Grace);

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));
            Assert.False(preview.IsRunning);
        }
    }

    [Fact]
    public void AnIntentionThatOffersNothingIsNotPreviewed()
    {
        var session = TestEditor.Open(TestEditor.BareRule());
        session.AddIntentions(new StubIntentions(new Intention(
            "test.nothing",
            "Nothing at all",
            "Has no edit behind it.",
            _ => null)), IntentionSource.Language);

        using (session)
        using (var preview = new OfferPreview(session))
        {
            preview.Hover(Offer(session, "test.nothing"));
            preview.Tick(PreviewDelay.Default.Preview);

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));
        }
    }

    [Fact]
    public void AnEditThatCannotRunOnACopyIsNotPreviewed()
    {
        // The documented failure: an action that throws on the copy. It costs the preview and nothing else.
        var session = TestEditor.Open(TestEditor.BareRule());
        session.AddIntentions(new StubIntentions(new Intention(
            "test.throws",
            "Throw on a copy",
            "Cannot be previewed.",
            _ => new DelegateAction(_ => throw new InvalidOperationException("boom")))), IntentionSource.Language);

        using (session)
        using (var preview = new OfferPreview(session))
        {
            preview.Hover(Offer(session, "test.throws"));
            preview.Tick(PreviewDelay.Default.Preview);

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));
        }
    }

    [Fact]
    public void AChangedDocumentSilencesTheHoverUntilThePointerReturns()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            preview.Invalidate();

            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));

            // The same hover does not climb again: the user moves the pointer to ask again.
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            Assert.Equal(PreviewStage.Nothing, preview.Stage);

            preview.Leave();
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            Assert.Equal(PreviewStage.Preview, preview.Stage);
        }
    }

    [Fact]
    public void TheFrameIsKeptUntilTheViewportOrTheOffsetChanges()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            // A repaint per caret blink must not re-project a copy, so the same question answers the same frame.
            Assert.Same(preview.Ghost(Viewport, 0, 0), preview.Ghost(Viewport, 0, 0));

            Assert.NotSame(preview.Ghost(Viewport, 0, 0), preview.Ghost(Viewport, 0, 5));
            Assert.NotSame(preview.Ghost(Viewport, 0, 0), preview.Ghost(new LayoutSize(40, 20), 0, 0));
        }
    }

    [Fact]
    public void AnotherOfferStartsFromNothing()
    {
        var (session, preview) = Open();

        using (session)
        using (preview)
        {
            preview.Hover(Offer(session, TestIntentions.RenameId));
            preview.Tick(PreviewDelay.Default.Preview);

            preview.Hover(Offer(session, TestIntentions.DeleteId));

            Assert.Equal(TestIntentions.DeleteId, preview.Entry!.Id);
            Assert.Equal(PreviewStage.Nothing, preview.Stage);
            Assert.Null(preview.Ghost(Viewport, 0, 0));
        }
    }

    /// <summary>A document with the fixture's offers, and the preview that can show them.</summary>
    private static (EditorSession Session, OfferPreview Preview) Open()
    {
        var session = TestEditor.Open(TestEditor.BareRule());
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        return (session, new OfferPreview(session));
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
