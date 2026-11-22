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
    private const string NoCode = "<< which code >>";
    private const string NoImporter = "<< which importer >>";
    private const string Anywhere = "<< anywhere >>";
    private const string NoGenerated = "<< what it generates >>";
    private const string NoGenerator = "<< which generator >>";
    private const string NoVariables = "<< no variables >>";
    private const string NoOutcome = "<< what it answers >>";
    private const string NoSteps = "<< what a user does >>";
    private const string NoScriptedLines = "<< what it draws afterwards >>";
    private const string NoAt = "<< where the caret goes >>";
    private const string NoTyped = "<< what is typed >>";
    private const string NoKey = "<< which key >>";
    private const string NoIntention = "<< which offer >>";
    private const string NoNode = "<< which node >>";
    private const string NoType = "<< which type >>";
    private const string NoSlot = "<< which slot >>";
    private const string NoNames = "<< which names it sees >>";

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
            ed.Indent(ed.Indent(ed.Children(TestingLanguage.AssertionSlot, NoAssertion, stacked: true)))));

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
            ed.Property(TestingLanguage.CodeSlot, emptyText: NoCode),
            ed.Space,
            ed.Property(TestingLanguage.SeveritySlot),
            ed.Space,
            ed.Property(TestingLanguage.RoleSlot, emptyText: Anywhere)));

        notations.Add(TestingConceptKeys.Reads, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("reads"),
                ed.Space,
                ed.Property(TestingLanguage.ImporterSlot, emptyText: NoImporter),
                ed.Space,
                ed.Keyword("as"),
                ed.Space,
                ed.Property(TestingLanguage.GeneratorSlot, emptyText: NoGenerator)),
            ed.Indent(ed.Children(TestingLanguage.LinesSlot, NoLines, stacked: true))));
        notations.Add(TestingConceptKeys.Imports, ed => ed.Line(
            ed.Keyword("imports"),
            ed.Space,
            ed.Property(TestingLanguage.GeneratorSlot, emptyText: NoGenerator)));

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

        notations.Add(TestingConceptKeys.Script, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("script"),
                ed.Space,
                ed.Property(TestingLanguage.WidthSlot),
                ed.Space,
                ed.Keyword("columns")),
            ed.Indent(ed.Children(TestingLanguage.StepsSlot, NoSteps, stacked: true)),
            ed.Line(ed.Keyword("then draws")),
            ed.Indent(ed.Children(TestingLanguage.LinesSlot, NoScriptedLines, stacked: true))));

        notations.Add(TestingConceptKeys.Caret, ed => ed.Line(
            ed.Keyword("caret at"),
            ed.Space,
            ed.Property(TestingLanguage.AtSlot, emptyText: NoAt)));

        notations.Add(TestingConceptKeys.Type, ed => ed.Line(
            ed.Keyword("type"),
            ed.Space,
            ed.Property(TestingLanguage.TypedSlot, emptyText: NoTyped)));

        notations.Add(TestingConceptKeys.Press, ed => ed.Line(
            ed.Keyword("press"),
            ed.Space,
            ed.Property(TestingLanguage.KeySlot, emptyText: NoKey)));

        notations.Add(TestingConceptKeys.Invoke, ed => ed.Line(
            ed.Keyword("invoke"),
            ed.Space,
            ed.Property(TestingLanguage.IntentionSlot, emptyText: NoIntention)));

        notations.Add(TestingConceptKeys.TypeOf, ed => ed.Line(
            ed.Keyword("the node at"),
            ed.Space,
            ed.Property(TestingLanguage.AtSlot, emptyText: NoNode),
            ed.Space,
            ed.Keyword("has type"),
            ed.Space,
            ed.Property(TestingLanguage.TypeSlot, emptyText: NoType)));

        notations.Add(TestingConceptKeys.ScopeOf, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("the node at"),
                ed.Space,
                ed.Property(TestingLanguage.AtSlot, emptyText: NoNode),
                ed.Space,
                ed.Keyword("sees into"),
                ed.Space,
                ed.Property(TestingLanguage.ScopeSlot, emptyText: NoSlot)),
            ed.Indent(ed.Children(TestingLanguage.LinesSlot, NoNames, stacked: true))));
    }
}
