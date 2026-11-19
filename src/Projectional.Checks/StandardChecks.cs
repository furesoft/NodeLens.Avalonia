namespace Projectional.Checks;

/// <summary>The framework's own checks, grouped the way the catalogue groups them.</summary>
/// <remarks>
/// <para>
/// A standard check is one that can be computed from the language's declarations and the model's own
/// structure - the name slot, the child slots, the property types - and never from an analysis only the
/// language could do. That is the filter: a check that passes it should not be written again per language,
/// which is what makes the catalogue a library rather than a policy.
/// </para>
/// <para>
/// A language includes what it wants, and nothing has to be switched off: a check whose slots the language
/// never declares simply finds nothing. The groups exist so a language can take "the name rules" without
/// taking the expression rules:
/// </para>
/// <code>
/// public static CheckSet Checks { get; } =
///     new CheckSet([.. StandardChecks.Names, .. StandardChecks.Structure, new ExactlyOneAcceptRule()]);
/// </code>
/// <para>
/// <see cref="All"/> is the same catalogue as one composable set, which is the shorter spelling for a
/// language that takes everything: <c>StandardChecks.All.With(new ExactlyOneAcceptRule())</c>.
/// </para>
/// </remarks>
public static class StandardChecks
{
    /// <summary>The checks about a concept's name slot: <c>STD001</c>, <c>STD002</c> and <c>STD003</c>.</summary>
    public static IReadOnlyList<ICheck> Names { get; } =
    [
        new PlaceholderNameCheck(),
        new DuplicateNameCheck(),
        new SurroundingWhitespaceCheck(),
    ];

    /// <summary>The checks about declared structure: <c>STD010</c> and <c>STD011</c>.</summary>
    public static IReadOnlyList<ICheck> Structure { get; } =
    [
        new EmptyChildSlotCheck(),
        new MinimumChildCountCheck(),
    ];

    /// <summary>The checks that need the expression sub-language: <c>STD040</c>.</summary>
    public static IReadOnlyList<ICheck> Expressions { get; } = [new BinaryGroupingCheck()];

    /// <summary>Every check the framework ships, as one set a language can extend.</summary>
    public static CheckSet All { get; } = new([.. Names, .. Structure, .. Expressions]);
}
