using Projectional.Core;

namespace Projectional.Language;

/// <summary>The first part that answers anything - how a concept says "the name, else the value, else a word".</summary>
/// <param name="Parts">The parts to try, in order.</param>
/// <remarks>
/// The alternative to <see cref="NameJoin"/>: where a join adds, a fallback chooses. It is what makes "a stored name if
/// there is one, otherwise a name computed from the value" one declaration instead of a choice per node.
/// </remarks>
public sealed record NameFallback : NameBody
{
    /// <summary>Creates a part that answers the first non-empty answer of <paramref name="parts"/>.</summary>
    /// <param name="parts">The parts to try, in order; when none answers, this answers nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> is <see langword="null"/>.</exception>
    public NameFallback(params NameBody[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        Parts = parts;
    }

    /// <summary>The parts to try, in order.</summary>
    public IReadOnlyList<NameBody> Parts { get; }

    private protected override string Evaluate(Node node)
    {
        foreach (var part in Parts)
        {
            var answer = part.NameOf(node);

            if (answer.Length > 0)
            {
                return answer;
            }
        }

        return string.Empty;
    }

    public override string ToString() => $"fallback of {Parts.Count} part(s)";
}
