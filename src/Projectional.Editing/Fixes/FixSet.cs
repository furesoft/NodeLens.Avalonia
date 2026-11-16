using System.Collections.ObjectModel;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>The repairs offered for a batch of findings, kept beside the set that reports them.</summary>
/// <remarks>
/// <para>
/// Fixes deliberately do not live on <see cref="Diagnostic"/> or on <see cref="MarkerSet"/>: the
/// vocabulary of "what is wrong" would otherwise also be the vocabulary of "what to do about it", and a
/// renderer or a hit test - the two readers a marker set has - has no use for a repair. A host that runs
/// checks publishes its findings and its fixes as two sets, and one that runs none assigns neither and
/// pays nothing.
/// </para>
/// <para>
/// The key is the <see cref="Diagnostic"/> record itself, by value. Keying by node would answer "what is
/// wrong here" when the question is "what does this particular finding offer", and two findings on one
/// node with different cures would collide. Value equality is the honest key for the other reason too: a
/// check that reworded its message has reworded the explanation it builds its repairs from, so the fixes
/// it would build now belong with the message it would write now.
/// </para>
/// <para>
/// Immutable and order-preserving like <see cref="MarkerSet"/>, with one strictness that set does not
/// have: two fixes with the same id for the same finding are rejected. Two entries that answer one
/// question are a programming error, and keeping the first silently would hide it - the same rule
/// <c>SyntaxPalette.Create</c> applies to a style kind mentioned twice.
/// </para>
/// </remarks>
public sealed class FixSet
{
    private readonly ReadOnlyCollection<DiagnosticFix> _all;
    private readonly Dictionary<Diagnostic, ReadOnlyCollection<DiagnosticFix>> _byDiagnostic;

    /// <summary>Collects <paramref name="fixes"/>, which may be empty.</summary>
    /// <param name="fixes">The repairs, in the order they should be offered.</param>
    /// <exception cref="ArgumentNullException">One of <paramref name="fixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two fixes share an id for one finding.</exception>
    public FixSet(params ReadOnlySpan<DiagnosticFix> fixes)
    {
        var all = new List<DiagnosticFix>(fixes.Length);
        var byDiagnostic = new Dictionary<Diagnostic, List<DiagnosticFix>>();
        var ids = new HashSet<(Diagnostic Finding, string Id)>();

        foreach (var fix in fixes)
        {
            ArgumentNullException.ThrowIfNull(fix, nameof(fixes));

            if (!ids.Add((fix.Diagnostic, fix.Id)))
            {
                throw new ArgumentException(
                    $"The fix '{fix.Id}' is already offered for the finding \"{fix.Diagnostic.Message}\".",
                    nameof(fixes));
            }

            all.Add(fix);

            if (!byDiagnostic.TryGetValue(fix.Diagnostic, out var forFinding))
            {
                forFinding = [];
                byDiagnostic[fix.Diagnostic] = forFinding;
            }

            forFinding.Add(fix);
        }

        _all = all.AsReadOnly();
        _byDiagnostic = new Dictionary<Diagnostic, ReadOnlyCollection<DiagnosticFix>>(byDiagnostic.Count);

        foreach (var (diagnostic, forFinding) in byDiagnostic)
        {
            _byDiagnostic[diagnostic] = forFinding.AsReadOnly();
        }
    }

    /// <summary>A set with nothing in it, which is what a document nobody checks reports.</summary>
    public static FixSet Empty { get; } = new();

    /// <summary>How many repairs the set holds.</summary>
    public int Count => _all.Count;

    /// <summary>The repairs offered for <paramref name="diagnostic"/>, in the order they were collected.</summary>
    /// <param name="diagnostic">The finding to ask about.</param>
    /// <remarks>
    /// Empty rather than <see langword="null"/> when a finding offers nothing, because the caller's
    /// question is "what can I offer here" and "nothing" is a real answer: a check that knows about a
    /// problem but not its cure is an ordinary case, not a miss.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> For(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return _byDiagnostic.TryGetValue(diagnostic, out var forFinding) ? forFinding : [];
    }
}
