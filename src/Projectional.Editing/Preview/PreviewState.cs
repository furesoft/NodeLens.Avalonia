namespace Projectional.Editing;

/// <summary>
/// How far a hovered entry has travelled down the ladder, as a value the host asks instead of a timer it owns.
/// </summary>
/// <remarks>
/// <para>
/// The ladder is a state machine and not three timers, because the interesting cases are the interruptions:
/// the pointer that leaves an entry and returns, the document that changes under a hover, the preview that must
/// not restart because the user moved between the bulb and the list. Those are rules, and rules belong here -
/// headless, driven by a fake clock in tests, and the same arrangement as <see cref="CaretBlink"/>, which is why
/// nothing about the timing needs a window to verify.
/// </para>
/// <para>
/// The host calls <see cref="Hover"/>, <see cref="Leave"/>, <see cref="LeaveList"/>, <see cref="Invalidate"/> and
/// once per tick <see cref="Tick"/>, then draws whatever <see cref="Stage"/> says. It owns the timer and nothing
/// else; this type never asks what time it is.
/// </para>
/// <para>
/// The entry key is a string so that this stays independent of <see cref="Intention"/>; the value a host passes is
/// <see cref="Intention.Id"/>, which is stable, never translated and unique within one list.
/// </para>
/// </remarks>
public sealed class PreviewState
{
    private readonly PreviewDelay _delay;
    private string? _hovered;
    private string? _blocked;
    private TimeSpan _elapsed;
    private TimeSpan? _grace;

    /// <summary>Starts the ladder with <see cref="PreviewDelay.Default"/>.</summary>
    public PreviewState()
        : this(PreviewDelay.Default)
    {
    }

    /// <summary>Starts the ladder with the timings <paramref name="delay"/> names.</summary>
    /// <param name="delay">The four durations the rungs are measured with.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="delay"/> is negative or has its rungs out of order.
    /// </exception>
    public PreviewState(PreviewDelay delay)
    {
        delay.Validate();

        _delay = delay;
    }

    /// <summary>The entry being hovered, or <see langword="null"/> when none is.</summary>
    /// <remarks>
    /// During the grace period this is still the entry the pointer left, because the preview is still showing it.
    /// </remarks>
    public string? Entry => _hovered;

    /// <summary>What the host should be showing right now.</summary>
    public PreviewStage Stage { get; private set; }

    /// <summary>Whether the preview is replacing the page.</summary>
    public bool IsPreviewing => Stage == PreviewStage.Preview;

    /// <summary>The timings in force.</summary>
    public PreviewDelay Delay => _delay;

    /// <summary>The pointer entered <paramref name="entryId"/>, or the keyboard highlight moved onto it.</summary>
    /// <param name="entryId">The entry's stable id.</param>
    /// <exception cref="ArgumentException"><paramref name="entryId"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// Hovering the entry that is already hovered is not a new hover: it is what happens when the pointer crosses
    /// the gap between the bulb and the list and arrives back where it was, so the clock continues instead of
    /// starting again - that is the whole point of the grace period.
    /// </para>
    /// <para>
    /// A different entry starts from nothing. The preview of one entry is never left standing while another is
    /// considered, because a preview is an answer about <em>that</em> edit.
    /// </para>
    /// <para>
    /// An entry the document changed under stays silent: <see cref="Invalidate"/> remembers it, and only leaving
    /// and coming back asks again. A preview of a document that has moved on would be a lie told confidently.
    /// </para>
    /// </remarks>
    public void Hover(string entryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        _grace = null;

        if (entryId == _hovered)
        {
            return;
        }

        _hovered = entryId;
        _elapsed = TimeSpan.Zero;

        if (entryId != _blocked)
        {
            _blocked = null;
        }

        Settle();
    }

    /// <summary>The pointer left the hovered entry, or the list closed.</summary>
    /// <remarks>
    /// Immediate, because the user has moved on to something else in the same list. The one case that is not
    /// immediate is leaving the list entirely - see <see cref="LeaveList"/> - since that is usually a pointer on
    /// its way somewhere and not a decision.
    /// </remarks>
    public void Leave() => Cancel();

    /// <summary>The pointer left the list, which is usually a pointer on its way to or from the bulb.</summary>
    /// <remarks>
    /// The preview survives for <see cref="PreviewDelay.Grace"/> so that crossing the gap does not cancel and
    /// restart it four times. Nothing is added to the clock while the pointer is away: coming back resumes the
    /// rung that was showing instead of arriving at the next one.
    /// </remarks>
    public void LeaveList()
    {
        if (_hovered is null)
        {
            return;
        }

        if (_delay.Grace <= TimeSpan.Zero)
        {
            Cancel();

            return;
        }

        _grace = _delay.Grace;
    }

    /// <summary>The clock moved on by <paramref name="elapsed"/>, so the ladder may have climbed.</summary>
    /// <param name="elapsed">How long since the last tick.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsed"/> is negative.</exception>
    /// <remarks>
    /// Monotonic on purpose: a stage that has been reached is never given up while the entry stays hovered, so a
    /// host that ticks late does not make the tooltip blink back to nothing.
    /// </remarks>
    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                elapsed,
                "The clock does not run backwards.");
        }

        if (_grace is { } remaining)
        {
            remaining -= elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                Cancel();
            }
            else
            {
                _grace = remaining;
            }

            return;
        }

        if (_hovered is null)
        {
            return;
        }

        _elapsed += elapsed;

        Settle();
    }

    /// <summary>The document changed, the list was rebuilt, or something else made the hover stale.</summary>
    /// <remarks>
    /// The entry stays hovered and stays silent until the pointer leaves and returns, which is what "the user moves
    /// the pointer to ask again" means as a rule. Cancelling without remembering would restart the ladder on the
    /// very next tick, and a preview of a document that is being edited would flicker.
    /// </remarks>
    public void Invalidate()
    {
        if (_hovered is { } entry)
        {
            _blocked = entry;
        }

        _elapsed = TimeSpan.Zero;
        _grace = null;

        Settle();
    }

    private void Cancel()
    {
        _hovered = null;
        _blocked = null;
        _elapsed = TimeSpan.Zero;
        _grace = null;

        Settle();
    }

    private void Settle() =>
        Stage = _hovered is null || _hovered == _blocked
            ? PreviewStage.Nothing
            : _elapsed >= _delay.Preview
                ? PreviewStage.Preview
                : _elapsed >= _delay.Description
                    ? PreviewStage.Tooltip
                    : PreviewStage.Nothing;

    public override string ToString() =>
        _hovered is null ? $"none ({Stage})" : $"{_hovered} ({Stage})";
}
