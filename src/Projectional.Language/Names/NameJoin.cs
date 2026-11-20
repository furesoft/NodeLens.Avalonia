using Projectional.Core;

namespace Projectional.Language;

/// <summary>Several parts one after another, with the empty ones left out.</summary>
/// <param name="Separator">What stands between two answers that are both non-empty.</param>
/// <param name="Parts">The parts to answer, in order.</param>
/// <remarks>
/// The omission is the point: <c>Join(".", property, child)</c> answers <c>left.item 2</c> for a complete node and
/// <c>left</c> for one whose child is not written yet, without a conditional per part. Without it a half-written node
/// would be called <c>left.</c>, which is a name no user typed.
/// </remarks>
public sealed record NameJoin : NameBody
{
    /// <summary>Creates a part that joins what <paramref name="parts"/> answer.</summary>
    /// <param name="separator">What stands between two answers; may be empty.</param>
    /// <param name="parts">The parts to answer, in order; empty answers nothing.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public NameJoin(string separator, params NameBody[] parts)
    {
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(parts);

        Separator = separator;
        Parts = parts;
    }

    /// <summary>What stands between two answers that are both non-empty.</summary>
    public string Separator { get; }

    /// <summary>The parts to answer, in order.</summary>
    public IReadOnlyList<NameBody> Parts { get; }

    private protected override string Evaluate(Node node)
    {
        List<string> answers = [];

        foreach (var part in Parts)
        {
            var answer = part.NameOf(node);

            if (answer.Length > 0)
            {
                answers.Add(answer);
            }
        }

        return string.Join(Separator, answers);
    }

    public override string ToString() => $"join \"{Separator}\"";
}
