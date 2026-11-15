using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Whether the caret bar is currently on, and in which cells it blinks at all.
/// </summary>
/// <remarks>
/// <para>
/// The phase is state, not a rendering trick: a host that repaints for other reasons must not reset
/// the rhythm, and a test must be able to say "the caret is off at this moment" without a window.
/// So the decision - toggle or hold still - lives here and the control only asks it once per tick.
/// </para>
/// <para>
/// The caret blinks only where it is <em>inside</em> something the user can type into, which means a
/// property cell. A caret standing on a keyword has nowhere to go, and blinking there would promise an
/// insertion point that does not exist. The phase is held at visible in that case, so the caret stays
/// findable.
/// </para>
/// </remarks>
public sealed class CaretBlink
{
    /// <summary>How long one half of the blink takes.</summary>
    /// <remarks>
    /// The same value most platform text cursors use, within a few milliseconds. It is public because a
    /// host that draws the caret itself should blink at the same speed as the editor it embeds.
    /// </remarks>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(530);

    /// <summary>Whether the caret bar is painted right now.</summary>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Whether the caret blinks at <paramref name="position"/> in <paramref name="tree"/>.</summary>
    /// <param name="tree">The tree the caret was resolved against.</param>
    /// <param name="position">Where the caret stands.</param>
    public static bool Blinks(EditorTree tree, CaretPosition position) =>
        CaretNavigation.PlaceAt(tree, position)?.IsEditable == true;

    /// <summary>
    /// Moves the phase on by one tick and reports whether that changed what is on screen.
    /// </summary>
    /// <param name="blinking">Whether the caret is supposed to blink where it stands.</param>
    /// <returns>
    /// <see langword="true"/> when the caller has to repaint. A caret that must hold still is already
    /// visible, so a steady caret costs no repaints.
    /// </returns>
    public bool Advance(bool blinking)
    {
        if (!blinking)
        {
            if (IsVisible)
            {
                return false;
            }

            IsVisible = true;

            return true;
        }

        IsVisible = !IsVisible;

        return true;
    }

    /// <summary>Shows the caret again, which every edit, move and click does.</summary>
    /// <remarks>
    /// Without this the caret could be invisible at the moment the user types, so the first keystroke
    /// would look as if nothing had happened until the next tick.
    /// </remarks>
    public void Reset() => IsVisible = true;
}
