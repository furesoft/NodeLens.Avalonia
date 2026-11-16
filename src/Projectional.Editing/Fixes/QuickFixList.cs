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
/// It is never built with fewer than two entries. One repair is applied immediately, because a list of one
/// costs the user a keystroke and tells them nothing, and none is the absence of an answer rather than an
/// empty list - so a <see cref="QuickFixList"/> always has something to choose between.
/// </para>
/// </remarks>
public sealed class QuickFixList
{
    private readonly ReadOnlyCollection<DiagnosticFix> _fixes;

    /// <summary>Offers <paramref name="fixes"/>, which must hold at least two repairs.</summary>
    /// <param name="fixes">The repairs to offer, in the order they should be listed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Fewer than two repairs were given.</exception>
    public QuickFixList(IReadOnlyList<DiagnosticFix> fixes)
    {
        ArgumentNullException.ThrowIfNull(fixes);

        if (fixes.Count < 2)
        {
            throw new ArgumentException(
                "A quick-fix list is only opened for two or more repairs; one is applied directly and none is no answer.",
                nameof(fixes));
        }

        var copy = new List<DiagnosticFix>(fixes.Count);

        foreach (var fix in fixes)
        {
            ArgumentNullException.ThrowIfNull(fix, nameof(fixes));

            copy.Add(fix);
        }

        _fixes = copy.AsReadOnly();
    }

    /// <summary>The repairs, in the order they are offered.</summary>
    public IReadOnlyList<DiagnosticFix> Fixes => _fixes;

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
}
