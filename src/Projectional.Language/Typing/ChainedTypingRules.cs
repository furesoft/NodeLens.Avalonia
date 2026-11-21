namespace Projectional.Language;

/// <summary>Several rule sets asked in order, where the first claim wins.</summary>
/// <remarks>
/// <para>
/// What an extending language needs: its own typing rules and the ones it inherits answer one question - "what does
/// this burst of text create here" - and the order is the answer to a conflict. The project puts the language's own
/// rules first and its bases' nearest first, so a language that refines its base's rule wins with the nearer one.
/// </para>
/// <para>
/// The chain is immutable and holds the sets it was given, so it costs one delegation per set that does not claim
/// anything - and a claim is a predicate over a short string, which is the same work the sets would have done
/// themselves.
/// </para>
/// </remarks>
public sealed class ChainedTypingRules : ITypingRules
{
    private readonly IReadOnlyList<ITypingRules> _rules;

    private ChainedTypingRules(IReadOnlyList<ITypingRules> rules) => _rules = rules;

    /// <summary>The sets, in the order they are asked.</summary>
    public IReadOnlyList<ITypingRules> Rules => _rules;

    /// <summary>The chain of <paramref name="rules"/>, or the one set itself when there is only one.</summary>
    /// <param name="rules">The sets to ask, first claim wins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rules"/> is empty.</exception>
    public static ITypingRules Of(IEnumerable<ITypingRules> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        List<ITypingRules> sets = [.. rules];

        return sets.Count switch
        {
            0 => throw new ArgumentException("A chain of typing rules needs at least one set.", nameof(rules)),
            1 => sets[0],
            _ => new ChainedTypingRules(sets),
        };
    }

    /// <summary>The first set that claims <paramref name="text"/> for <paramref name="conceptKey"/>.</summary>
    public TypingRule? ClaimFor(string conceptKey, string text)
    {
        foreach (var rules in _rules)
        {
            if (rules.ClaimFor(conceptKey, text) is { } rule)
            {
                return rule;
            }
        }

        return null;
    }

    public override string ToString() => $"{_rules.Count} rule set(s)";
}
