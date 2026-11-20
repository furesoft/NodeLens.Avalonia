using Projectional.Core;
using Projectional.Language;

namespace Projectional.Testing;

/// <summary>How a declared test is drawn: the document it is about and the assertion it makes.</summary>
/// <remarks>
/// One line, because a test is one sentence. The assertion is a child and draws inline, so
/// <c>test pipeline.mps round trips</c> reads as the claim it is rather than as a small document.
/// </remarks>
public static class TestingNotations
{
    private const string NoAssertion = "<< what it asserts >>";

    /// <summary>The notation registry of the test language.</summary>
    public static NotationRegistry Registry { get; } = Create(TestingLanguage.Concepts);

    /// <summary>The notation registry of <paramref name="concepts"/>.</summary>
    /// <param name="concepts">The concepts to register notations against.</param>
    public static NotationRegistry Create(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }

    /// <summary>Registers the notations into <paramref name="notations"/>.</summary>
    /// <param name="notations">The registry to add to.</param>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(TestingConceptKeys.Test, ed => ed.Line(
            ed.Keyword("test", TextEmphasis.Bold),
            ed.Space,
            ed.Property(TestingLanguage.DocumentSlot),
            ed.Space,
            ed.ChildInline(TestingLanguage.AssertionSlot, NoAssertion)));

        notations.Add(TestingConceptKeys.RoundTrips, ed => ed.Line(
            ed.Keyword("round trips")));
    }
}
