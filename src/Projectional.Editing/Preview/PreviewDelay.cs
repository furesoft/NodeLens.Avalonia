namespace Projectional.Editing;

/// <summary>
/// The four numbers the hover ladder is built from, in one value a host can replace.
/// </summary>
/// <remarks>
/// <para>
/// The numbers are tuning and the shape is not: an entry that is hovered shows its description first and its
/// preview only after the user has clearly stopped on it, because a preview that appears while the pointer is
/// still travelling turns a list into a strobe. Keeping the numbers in one immutable value means a host can slow
/// the ladder down for a projector or speed it up for an experienced user without any of the machinery changing.
/// </para>
/// <para>
/// <see cref="Highlight"/> is zero and exists anyway: it is the rung the list draws itself, and a host that wants
/// a delay before even that has somewhere to say so.
/// </para>
/// </remarks>
/// <param name="Highlight">How long an entry is hovered before it is drawn as highlighted.</param>
/// <param name="Description">How long after that its description appears as a tooltip.</param>
/// <param name="Preview">How long after that the preview replaces the page.</param>
/// <param name="Grace">
/// How long the preview survives the pointer leaving the list, so that moving between the bulb and an entry does
/// not cancel and restart it.
/// </param>
/// <exception cref="ArgumentException">
/// A duration is negative, or the description is due at or after the preview - a ladder whose rungs are out of
/// order would show the answer before the question.
/// </exception>
public readonly record struct PreviewDelay(
    TimeSpan Highlight,
    TimeSpan Description,
    TimeSpan Preview,
    TimeSpan Grace)
{
    /// <summary>The timing the editor uses: a third of a second to the tooltip, seven tenths to the preview.</summary>
    /// <remarks>
    /// The description is a tooltip and gets the platform's usual hover delay. The preview replaces the whole page,
    /// which is a much bigger interruption, so it waits more than twice as long - long enough that reading the
    /// list does not flash documents, short enough that a user who has decided is not kept waiting.
    /// </remarks>
    public static PreviewDelay Default { get; } = new(
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(700),
        TimeSpan.FromMilliseconds(150));

    /// <summary>Whether every duration is a real duration and the rungs are in order.</summary>
    /// <remarks>
    /// Public because a host that builds the value from settings needs to check it before handing it over: the
    /// record's own constructor throws, and a preferences dialog should not.
    /// </remarks>
    public bool IsValid =>
        Highlight >= TimeSpan.Zero
        && Description >= TimeSpan.Zero
        && Preview >= TimeSpan.Zero
        && Grace >= TimeSpan.Zero
        && Highlight <= Description
        && Description < Preview;

    /// <summary>Throws unless every duration is a real duration and the rungs are in order.</summary>
    /// <exception cref="ArgumentException">A duration is negative, or the rungs are out of order.</exception>
    public void Validate()
    {
        if (Highlight < TimeSpan.Zero)
        {
            throw new ArgumentException($"The highlight delay '{Highlight}' is negative.", nameof(Highlight));
        }

        if (Description < TimeSpan.Zero)
        {
            throw new ArgumentException($"The description delay '{Description}' is negative.", nameof(Description));
        }

        if (Preview < TimeSpan.Zero)
        {
            throw new ArgumentException($"The preview delay '{Preview}' is negative.", nameof(Preview));
        }

        if (Grace < TimeSpan.Zero)
        {
            throw new ArgumentException($"The grace period '{Grace}' is negative.", nameof(Grace));
        }

        if (Highlight > Description)
        {
            throw new ArgumentException(
                $"The highlight delay '{Highlight}' is longer than the description delay '{Description}'.",
                nameof(Highlight));
        }

        if (Description >= Preview)
        {
            throw new ArgumentException(
                $"The description delay '{Description}' is not shorter than the preview delay '{Preview}'.",
                nameof(Description));
        }
    }
}
