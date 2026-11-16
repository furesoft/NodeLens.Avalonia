using System.Collections.ObjectModel;
using Projectional.Core;
using Projectional.Editing;

namespace Projectional.Checks;

/// <summary>The checks one language offers, in the order their findings should be shown.</summary>
/// <remarks>
/// <para>
/// This is the composition point a language author writes once: the standard catalogue plus whatever only
/// that language knows, and nothing else. A host then asks the set, not the individual checks, so adding a
/// check to a language is one line in its declaration rather than one line in every consumer.
/// </para>
/// <para>
/// Immutable and ordered, like <see cref="FixSet"/> and <see cref="MarkerSet"/>: <see cref="With"/> answers
/// a new set rather than appending to this one, so a set handed to a document cannot grow underneath it.
/// Running a set is a plain concatenation - each check reports in document order, and the set keeps the
/// checks' order - which means two checks that report the same node produce two findings and nothing is
/// silently merged.
/// </para>
/// </remarks>
public sealed class CheckSet
{
    private readonly ReadOnlyCollection<ICheck> _checks;

    /// <summary>Offers <paramref name="checks"/>, in that order; an empty sequence is allowed.</summary>
    /// <param name="checks">The checks to run, in the order their findings are shown.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="checks"/> or one of its entries is <see langword="null"/>.
    /// </exception>
    public CheckSet(IEnumerable<ICheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        var all = new List<ICheck>();

        foreach (var check in checks)
        {
            ArgumentNullException.ThrowIfNull(check, nameof(checks));

            all.Add(check);
        }

        _checks = all.AsReadOnly();
    }

    /// <summary>A set with nothing in it, which is what a document with no checks publishes.</summary>
    public static CheckSet Empty { get; } = new([]);

    /// <summary>The checks, in the order they run.</summary>
    public IReadOnlyList<ICheck> Checks => _checks;

    /// <summary>How many checks the set holds.</summary>
    public int Count => _checks.Count;

    /// <summary>This set with <paramref name="check"/> appended.</summary>
    /// <param name="check">The check to add, which is not re-run or deduplicated.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="check"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Appending is what a language does with the standard catalogue:
    /// <c>StandardChecks.All.With(new ExactlyOneAcceptRule())</c>. Nothing is deduplicated, because two
    /// checks that answer the same finding are a language's decision to make, not one this type can infer.
    /// </remarks>
    public CheckSet With(ICheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new CheckSet([.. _checks, check]);
    }

    /// <summary>What every check in the set finds in <paramref name="model"/>, in order.</summary>
    /// <param name="model">The document to inspect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var check in _checks)
        {
            fixes.AddRange(check.Find(model));
        }

        return fixes;
    }

    public override string ToString() => $"{_checks.Count} checks";
}
