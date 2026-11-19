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
    /// <summary>The checks about a concept's name slot: <c>STD001</c>, <c>STD002</c>, <c>STD003</c> and <c>STD004</c>.</summary>
    public static IReadOnlyList<ICheck> Names { get; } =
    [
        new PlaceholderNameCheck(),
        new DuplicateNameCheck(),
        new SurroundingWhitespaceCheck(),
        new IdentifierNameCheck(),
    ];

    /// <summary>The checks about declared structure: <c>STD010</c>, <c>STD011</c> and <c>STD012</c>.</summary>
    public static IReadOnlyList<ICheck> Structure { get; } =
    [
        new EmptyChildSlotCheck(),
        new MinimumChildCountCheck(),
        new EmptyNodeCheck(),
    ];

    /// <summary>The checks about references: <c>STD020</c> and <c>STD021</c>.</summary>
    /// <remarks>
    /// Both codes come from one check, because which of the two holds is a fact about the same broken pointer - a
    /// reference with a close candidate is repaired by pointing it there, and one without is repaired by clearing it.
    /// A language whose references cross documents on purpose leaves this group out, since an unopened document's
    /// targets are unresolved by design: `Projectional.Samples.SqlSchema` does exactly that.
    /// </remarks>
    public static IReadOnlyList<ICheck> References { get; } = [new UnresolvedReferenceCheck()];

    /// <summary>The checks that need the expression sub-language: <c>STD040</c> and <c>STD041</c>.</summary>
    public static IReadOnlyList<ICheck> Expressions { get; } = [new BinaryGroupingCheck(), new DoubleNegationCheck()];

    /// <summary>Every check the framework ships, as one set a language can extend.</summary>
    public static CheckSet All { get; } = new([.. Names, .. Structure, .. References, .. Expressions]);
}
