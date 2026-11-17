using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The hover ladder tied to what it is a ladder for: one offer, the edit it would make, and the ghost of the copy
/// that edit was applied to.
/// </summary>
/// <remarks>
/// <para>
/// The three ingredients exist on their own - <see cref="PreviewState"/> times a hover, <see cref="PreviewSandbox"/>
/// is a discarded copy, <see cref="GhostFrame"/> is a drawn frame - and this is the one place that knows the order
/// they happen in. It is the answer to "a preview is a property of the list, not of a single affordance"
/// (<c>docs/architecture/context-actions.md</c>): the bulb, the popup and a context-menu submenu all show the same
/// list, so all three get their previews from one implementation instead of three.
/// </para>
/// <para>
/// It owns no timer and no pixels. A host calls <see cref="Tick"/> once per timer tick and <see cref="Ghost"/> once
/// per paint, passing its own viewport and scroll offsets - which is what anchors the preview to the line the user is
/// looking at, since only the page knows where that is. That keeps this headless: everything below can be driven by
/// a fake clock in a test, exactly like the caret blink.
/// </para>
/// <para>
/// The copy is opened lazily, when the ladder first reaches <see cref="PreviewStage.Preview"/>, and thrown away as
/// soon as the hover ends. A document that is too large, an offer whose edit cannot run on a copy and an offer that
/// produces no action all answer the same way: no preview, and the ladder goes quiet until the pointer leaves and
/// comes back. That is the documented best effort (<see cref="PreviewSandbox"/>,
/// <a href="../../../docs/adr/0017-preview-is-a-discarded-edit.md">ADR 0017</a>) - the live document is never
/// touched, and a preview that cannot be built is simply not shown.
/// </para>
/// </remarks>
public sealed class OfferPreview : IDisposable
{
    private readonly EditorSession _session;
    private readonly PreviewState _state;

    private Intention? _entry;
    private PreviewSandbox? _sandbox;
    private PreviewMarks? _marks;

    private GhostFrame? _frame;
    private LayoutSize _frameViewport;
    private double _frameX;
    private double _frameY;
    private ICellStyler? _frameStyler;

    private bool _disposed;

    /// <summary>Shows what a hovered offer would do, at the default timings.</summary>
    /// <param name="session">The live document whose offers are previewed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public OfferPreview(EditorSession session)
        : this(session, new PreviewState())
    {
    }

    /// <summary>Shows what a hovered offer would do, at the timings <paramref name="state"/> runs on.</summary>
    /// <param name="session">The live document whose offers are previewed.</param>
    /// <param name="state">The ladder to climb, which a host builds from its own settings.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="state"/> is <see langword="null"/>.
    /// </exception>
    public OfferPreview(EditorSession session, PreviewState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        _session = session;
        _state = state;
    }

    /// <summary>How far down the ladder the hovered entry has travelled.</summary>
    public PreviewStage Stage => _state.Stage;

    /// <summary>The entry being hovered, or <see langword="null"/> when none is.</summary>
    /// <remarks>
    /// Its id is the ladder's key and its label is what the badge says, so this is the entry the ghost describes
    /// rather than merely the one the pointer is over.
    /// </remarks>
    public Intention? Entry => _entry;

    /// <summary>Whether a hover is in progress, counting the grace period the pointer gets to come back.</summary>
    /// <remarks>
    /// A host starts its ticker on this and stops it when it turns false: nothing about the ladder needs a clock
    /// while no entry is hovered, and a document nobody is pointing at should cost no timer.
    /// </remarks>
    public bool IsRunning => _state.Entry is not null;

    /// <summary>The pointer entered <paramref name="intention"/>, or the keyboard highlight moved onto it.</summary>
    /// <param name="intention">The offer being hovered, as the list handed it over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="intention"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This preview has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Hovering the entry that is already hovered continues its clock, and anything else starts from nothing -
    /// the interruption rules are <see cref="PreviewState"/>'s and are deliberately not restated here.
    /// </para>
    /// <para>
    /// The copy is not taken yet. A pointer that crosses an entry is not a request to see an edit, and a document
    /// of twenty thousand nodes must not be copied because the mouse passed over a menu row.
    /// </para>
    /// </remarks>
    public void Hover(Intention intention)
    {
        ArgumentNullException.ThrowIfNull(intention);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_entry is { } current && string.Equals(current.Id, intention.Id, StringComparison.Ordinal))
        {
            _state.Hover(intention.Id);

            return;
        }

        Drop();

        _entry = intention;

        _state.Hover(intention.Id);
    }

    /// <summary>The pointer left the hovered entry, or the list closed.</summary>
    /// <remarks>
    /// Immediate, and it takes the copy with it: a preview of a row the user has moved off is an answer about a
    /// question they are no longer asking.
    /// </remarks>
    public void Leave()
    {
        _state.Leave();
        Drop();
    }

    /// <summary>The pointer left the list, which is usually a pointer on its way somewhere else.</summary>
    /// <remarks>
    /// The preview survives <see cref="PreviewDelay.Grace"/> with the clock paused, which is why this does
    /// <em>not</em> throw the copy away: coming back to the same entry has to find the sandbox it had.
    /// </remarks>
    public void LeaveList() => _state.LeaveList();

    /// <summary>The document changed, so the hover describes a state that no longer exists.</summary>
    /// <remarks>
    /// The entry stays silent until the pointer leaves and returns, which is "the user moves the pointer to ask
    /// again" as a rule. The copy goes with it, because it is a copy of the document as it was.
    /// </remarks>
    public void Invalidate()
    {
        _state.Invalidate();
        Drop();
    }

    /// <summary>The clock moved on by <paramref name="elapsed"/>.</summary>
    /// <param name="elapsed">How long since the last tick.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsed"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This preview has been disposed.</exception>
    /// <remarks>
    /// Reaching the preview rung is what opens the sandbox: the ladder decides <em>when</em> the answer is worth
    /// paying for, and this is the only place that pays.
    /// </remarks>
    public void Tick(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _state.Tick(elapsed);

        if (_state.IsPreviewing)
        {
            Ensure();

            return;
        }

        // The grace period ran out, which is the one way a hover ends without anyone calling Leave: the copy goes
        // with it, so a pointer that wandered off does not leave a document copied and invisible.
        if (_state.Entry is null)
        {
            Drop();
        }
    }

    /// <summary>The frame to draw instead of the page, or <see langword="null"/> when the page is the answer.</summary>
    /// <param name="viewport">How much room the page has on screen.</param>
    /// <param name="offsetX">The live page's horizontal scroll offset.</param>
    /// <param name="offsetY">The live page's vertical scroll offset.</param>
    /// <param name="styler">
    /// What each cell's text means, defaulting to <see cref="CellStyler.Default"/>. Pass the styler the page uses,
    /// or the ghost recolours a document nobody edited.
    /// </param>
    /// <returns>The ghost, or <see langword="null"/> while the ladder has not reached it.</returns>
    /// <exception cref="ObjectDisposedException">This preview has been disposed.</exception>
    /// <remarks>
    /// The viewport and the offsets are the caller's because the ghost is anchored to the page: a preview that
    /// chose its own scroll position would move the region the user was reading. A frame is kept until one of those
    /// four answers changes, so a repaint per caret blink does not re-project a document.
    /// </remarks>
    public GhostFrame? Ghost(
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        ICellStyler? styler = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_state.IsPreviewing || _sandbox is null || _marks is null || _entry is null)
        {
            return null;
        }

        if (_frame is { } cached
            && _frameViewport.Equals(viewport)
            && _frameX.Equals(offsetX)
            && _frameY.Equals(offsetY)
            && ReferenceEquals(_frameStyler, styler))
        {
            return cached;
        }

        _frame = GhostFrame.Build(_sandbox, _marks, _entry.Label, viewport, offsetX, offsetY, styler);
        _frameViewport = viewport;
        _frameX = offsetX;
        _frameY = offsetY;
        _frameStyler = styler;

        return _frame;
    }

    /// <summary>Ends the hover and throws the copy away.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _state.Leave();
        Drop();
    }

    /// <summary>Applies the hovered entry's edit to a copy, the first time the ladder asks for a ghost.</summary>
    /// <remarks>
    /// Every failure answers the same way - no preview - because a caller that wants to show an answer treats
    /// "cannot be previewed" and "nothing to show" identically. Reporting the difference is the badge's job when a
    /// preview was already running, and that is the host's surface, not this one's.
    /// </remarks>
    private void Ensure()
    {
        if (_marks is not null || _entry is not { } entry)
        {
            return;
        }

        if (PreviewSandbox.Open(_session) is not { } sandbox)
        {
            Unavailable();

            return;
        }

        if (_session.ActionFor(entry) is not { } action)
        {
            sandbox.Dispose();
            Unavailable();

            return;
        }

        if (sandbox.Apply(action) is not { } differences)
        {
            sandbox.Dispose();
            Unavailable();

            return;
        }

        _sandbox = sandbox;
        _marks = PreviewMarks.Of(differences, sandbox.Tree);
    }

    /// <summary>Stops the ladder on an entry that cannot be previewed, without restarting it on the next tick.</summary>
    private void Unavailable()
    {
        Drop();

        _state.Invalidate();
    }

    /// <summary>Throws the copy, the marks and the frame away; the ladder is left alone.</summary>
    /// <remarks>
    /// The layers are dropped together because a frame is only meaningful against the marks and the copy it was
    /// built from: keeping one of them alive would let a later hover draw an edit that was made to a document that
    /// has since been replaced.
    /// </remarks>
    private void Drop()
    {
        _sandbox?.Dispose();

        _sandbox = null;
        _marks = null;
        _frame = null;
        _frameStyler = null;
        _entry = null;
    }
}
