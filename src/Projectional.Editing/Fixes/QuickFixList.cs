using System.Collections.ObjectModel;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>The repairs offered at one moment, waiting for the user to pick one.</summary>
/// <remarks>
/// <para>
/// The list a gesture opened, and view state like the completion list: it is derived from the current
/// marker and fix sets, never persisted and never an undo step. It exists only because a list of several
/// repairs needs somewhere to live between the keystroke that asked and the click or the arrow key that
/// answers.
/// </para>
/// <para>
/// It is never empty: none is the absence of an answer rather than an empty list. It may hold exactly one,
/// which happens only when the lone repair is not one the gesture may run by itself - a destructive repair,
/// or one that cannot run at all. The list is then not offering a choice, it is asking whether the user
/// means it, and that is a different sentence with the same shape.
/// </para>
/// </remarks>
public sealed class QuickFixList
{
    private readonly ReadOnlyCollection<DiagnosticFix> _fixes;

    /// <summary>Offers <paramref name="fixes"/>, which must hold at least one repair.</summary>
    /// <param name="fixes">The repairs to offer, in the order they should be listed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No repairs were given.</exception>
    public QuickFixList(IReadOnlyList<DiagnosticFix> fixes)
        : this(Validated(fixes), 0)
    {
    }

    private QuickFixList(ReadOnlyCollection<DiagnosticFix> fixes, int selected)
    {
        _fixes = fixes;
        Selected = selected;
    }

    /// <summary>The repairs, in the order they are offered.</summary>
    public IReadOnlyList<DiagnosticFix> Fixes => _fixes;

    /// <summary>Which repair is highlighted, counted from zero.</summary>
    /// <remarks>
    /// The list opens on the first entry, which is the worst finding's repair by the order
    /// <see cref="QuickFixes.At(EditorSession)"/> answers in - so the entry the user most likely wants is
    /// the one that is already under the highlight.
    /// </remarks>
    public int Selected { get; }

    /// <summary>The highlighted repair.</summary>
    public DiagnosticFix SelectedFix => _fixes[Selected];

    /// <summary>The same repairs with <paramref name="index"/> highlighted, clamped into the list.</summary>
    /// <param name="index">Which repair to highlight.</param>
    /// <remarks>
    /// A new list rather than a mutation, so the one a caller is holding cannot change underneath it.
    /// The index is clamped rather than wrapped: a list of repairs is not a carousel, and pressing Down
    /// once too often should stop at the end where the user can see it stopped.
    /// </remarks>
    public QuickFixList With(int index) => new(_fixes, Math.Clamp(index, 0, _fixes.Count - 1));

    /// <summary>The same repairs with the highlight moved by <paramref name="delta"/> rows.</summary>
    /// <param name="delta">How many rows to move, negative towards the front.</param>
    public QuickFixList Move(int delta) => With(Selected + delta);

    /// <summary>How many repairs are offered.</summary>
    public int Count => _fixes.Count;

    /// <summary>The repair at <paramref name="index"/>.</summary>
    /// <param name="index">Which repair, counted from zero in the order offered.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public DiagnosticFix this[int index] => _fixes[index];

    /// <summary>The finding the repair at <paramref name="index"/> belongs to.</summary>
    /// <param name="index">Which repair, counted from zero in the order offered.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public Diagnostic DiagnosticOf(int index) => _fixes[index].Diagnostic;

    public override string ToString() => $"{_fixes.Count} quick fixes";

    /// <summary>Copies and checks a list of repairs for the real constructor to index into.</summary>
    private static ReadOnlyCollection<DiagnosticFix> Validated(IReadOnlyList<DiagnosticFix> fixes)
    {
        ArgumentNullException.ThrowIfNull(fixes);

        if (fixes.Count == 0)
        {
            throw new ArgumentException(
                "A quick-fix list holds at least one repair; no repairs at all is no answer rather than a list.",
                nameof(fixes));
        }

        var copy = new List<DiagnosticFix>(fixes.Count);

        foreach (var fix in fixes)
        {
            ArgumentNullException.ThrowIfNull(fix, nameof(fixes));

            copy.Add(fix);
        }

        return copy.AsReadOnly();
    }
}
