using System.Collections.Immutable;

namespace Projectional.Language;

/// <summary>
/// A <see cref="INotationChoice"/> that remembers one variant per concept, as an immutable value.
/// </summary>
/// <remarks>
/// Immutable because a choice travels with a projection: a rebuild has to see the choice that was in
/// force when it started, and a mutable object owned by a control would let a menu click in the middle
/// of a rebuild decide what the rebuild produces. <see cref="With"/> and <see cref="Without"/> return a
/// new choice instead of changing this one, which makes "switch, then re-project" a single expression
/// whose halves cannot come apart.
/// </remarks>
public sealed class NotationChoice : INotationChoice
{
    private readonly ImmutableDictionary<string, string> _variants;

    private NotationChoice(ImmutableDictionary<string, string> variants) => _variants = variants;

    /// <summary>A choice that expresses no opinion about any concept.</summary>
    public static NotationChoice Default { get; } =
        new(ImmutableDictionary.Create<string, string>(StringComparer.Ordinal));

    /// <summary>Whether this choice leaves every concept to its default variant.</summary>
    public bool IsEmpty => _variants.IsEmpty;

    /// <summary>The variants this choice names, by concept key.</summary>
    public IReadOnlyDictionary<string, string> Variants => _variants;

    /// <inheritdoc />
    public string? VariantOf(string conceptKey) =>
        conceptKey is not null && _variants.TryGetValue(conceptKey, out var variant) ? variant : null;

    /// <summary>This choice, with <paramref name="conceptKey"/> drawn in <paramref name="variant"/>.</summary>
    public NotationChoice With(string conceptKey, string variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(variant);

        return new NotationChoice(_variants.SetItem(conceptKey, variant));
    }

    /// <summary>This choice, with no opinion about <paramref name="conceptKey"/>.</summary>
    public NotationChoice Without(string conceptKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        return new NotationChoice(_variants.Remove(conceptKey));
    }

    public override string ToString()
    {
        if (_variants.IsEmpty)
        {
            return "no notation choice";
        }

        var parts = _variants
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}");

        return string.Join(", ", parts);
    }
}
