using Projectional.Core;
using Projectional.Language;

namespace Projectional.Testing;

/// <summary>How a declared test is drawn: the document it is about and the assertion it makes.</summary>
/// <remarks>
/// A header line naming the document, then the assertion indented below it. The assertion is a block rather than
/// an inline child, because a drawing or a list of findings is itself several lines - an inline child would have
/// to flatten them into one line, which is not what the expectation means.
/// </remarks>
public static class TestingNotations
{
    private const string NoAssertion = "<< what it asserts >>";
    private const string NoLines = "<< what it draws >>";
    private const string NoFindings = "<< what it reports >>";
    private const string Anywhere = "<< anywhere >>";
    private const string NoGenerated = "<< what it generates >>";
    private const string NoGenerator = "<< which generator >>";
    private const string NoVariables = "<< no variables >>";
    private const string NoOutcome = "<< what it answers >>";

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

        notations.Add(TestingConceptKeys.Test, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("test", TextEmphasis.Bold),
                ed.Space,
                ed.Property(TestingLanguage.DocumentSlot)),
            ed.Indent(ed.Children(TestingLanguage.AssertionSlot, NoAssertion, stacked: true))));

        notations.Add(TestingConceptKeys.RoundTrips, ed => ed.Line(
            ed.Keyword("round trips")));

        notations.Add(TestingConceptKeys.Draws, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("draws"),
                ed.Space,
                ed.Keyword("at"),
                ed.Space,
                ed.Property(TestingLanguage.WidthSlot),
                ed.Space,
                ed.Keyword("columns")),
            ed.Indent(ed.Children(TestingLanguage.LinesSlot, NoLines, stacked: true))));

        notations.Add(TestingConceptKeys.Line, ed => ed.Line(
            ed.Property(TestingLanguage.TextSlot)));

        notations.Add(TestingConceptKeys.Findings, ed => ed.Sequence(
            ed.Line(ed.Keyword("reports")),
            ed.Indent(ed.Children(TestingLanguage.ExpectedSlot, NoFindings, stacked: true))));

        notations.Add(TestingConceptKeys.ExpectedFinding, ed => ed.Line(
            ed.Property(TestingLanguage.CodeSlot),
            ed.Space,
            ed.Property(TestingLanguage.SeveritySlot),
            ed.Space,
            ed.Property(TestingLanguage.RoleSlot, emptyText: Anywhere)));

        notations.Add(TestingConceptKeys.Generates, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("generates"),
                ed.Space,
                ed.Property(TestingLanguage.GeneratorSlot, emptyText: NoGenerator)),
            ed.Indent(ed.Children(TestingLanguage.LinesSlot, NoGenerated, stacked: true))));

        notations.Add(TestingConceptKeys.Evaluates, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("evaluates"),
                ed.Space,
                ed.Property(TestingLanguage.VariablesSlot, emptyText: NoVariables)),
            ed.Indent(ed.Line(
                ed.Keyword("answers"),
                ed.Space,
                ed.Property(TestingLanguage.OutcomeSlot, emptyText: NoOutcome)))));
    }
}
