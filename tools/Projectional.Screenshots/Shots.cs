using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions.Intentions;
using Projectional.ImportRules;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.Intentions;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Projectional.Testing;

namespace Projectional.Screenshots;

/// <summary>The catalogue of images: one entry per editor feature the README documents.</summary>
/// <remarks>
/// Each shot is a real session driven through the same public API a host uses, so the picture is the
/// feature rather than an illustration of it. The documents are built to isolate one behaviour at a
/// time, and every image ends up in <c>docs/images</c> under the name the README links to.
/// </remarks>
internal static class Shots
{
    /// <summary>The sample language's notation, which every shot is drawn with.</summary>
    internal static NotationRegistry Notations { get; } = RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    /// <summary>Everything to capture, in the order the README presents the features.</summary>
    internal static IReadOnlyList<Shot> All { get; } =
    [
        new(
            "feature-syntax-highlighting",
            Documents.ThreeRules,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name"))),

        new(
            "feature-completion",
            Documents.OpenCondition,
            Arrange: OpenCompletion,
            ExtraWidth: 420,
            ExtraHeight: 360),

        new(
            "feature-offers",
            Documents.ReviewRule,
            Arrange: OpenOffers,
            ExtraWidth: 460,
            ExtraHeight: 360),

        new(
            "feature-selection",
            Documents.ThreeRules,
            Arrange: SelectSecondRule),

        new(
            "feature-folding",
            Documents.ThreeRules,
            Prepare: CollapseSecondRule,
            LineNumbers: true),

        new(
            "feature-triggers",
            Documents.OpenCondition,
            Arrange: TypeAnAddition),

        new(
            "feature-emphasis",
            Documents.AcceptedAndRejected,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(Rule(session, 1).Id, "name"))),

        new(
            "editor-light",
            Documents.ThreeRules,
            Arrange: MarkEverything,
            LineNumbers: true),

        new(
            "feature-line-numbers",
            Documents.ThreeRules,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(Rule(session, 1).Id, "name")),
            LineNumbers: true),

        new(
            "feature-markers",
            Documents.ThreeRules,
            Arrange: MarkEverything),

        new(
            "feature-marker-tooltip",
            Documents.ThreeRules,
            Arrange: ShowMarkerTip,
            ExtraWidth: 460,
            ExtraHeight: 300,
            Hover: HoverKind.Marker),

        new(
            "feature-concept-tooltip",
            Documents.ThreeRules,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            ExtraWidth: 520,
            ExtraHeight: 220,
            Hover: HoverKind.Text,
            HoverAddress: session => CellAddress.OfSlot(session.Document.Id, "name"),
            HoverDelayMs: 750),

        new(
            "feature-property-tooltip",
            Documents.ThreeRules,

            // The caret stays on the ruleset's name: an enumerated property opens its value list the moment the
            // caret arrives, and two boxes under one pointer would show the wrong thing.
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            ExtraWidth: 520,
            ExtraHeight: 240,
            Hover: HoverKind.Text,
            HoverAddress: session => CellAddress.OfSlot(Rule(session, 0).Id, "outcome"),
            HoverDelayMs: 750),

        new(
            "feature-property-placeholder",
            Documents.MissingOperator,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name"))),

        new(
            "feature-font-size",
            Documents.AcceptedAndRejected,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(Rule(session, 0).Id, "name")),
            FontSize: 20),

        new(
            "feature-dark-theme",
            Documents.ThreeRules,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            Dark: true,
            LineNumbers: true),

        new(
            "feature-table-view",
            Documents.ThreeRules,
            Prepare: TableView),

        // The three worked examples, each drawn by its own language - the point of the pictures is that these are
        // three *different* languages, so they share nothing with the sample's notation registry.
        new(
            "language-state-machine",
            LanguageDocuments.StateMachine,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            Surface: StateMachine),

        new(
            "language-feature-model",
            LanguageDocuments.FeatureModel,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            Surface: FeatureModel),

        new(
            "language-sql-schema",
            LanguageDocuments.SqlSchema,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            Surface: SqlSchema),

        // The state machine's second notation, which is the sample proving ADR 0012 rather than a feature of the
        // control: the same document as a table of its transitions.
        new(
            "language-state-machine-table",
            LanguageDocuments.StateMachine,
            Prepare: StateMachineTable,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            Surface: StateMachine),

        // A language's own test, written as data in the test language: the picture the tests-as-data slice is for.
        // The declared importer, which is the last aspect a language can state as a model: the picture is the
        // declaration itself, drawn by the notation that aspect ships.
        new(
            "language-declared-importer",
            LanguageDocuments.DeclaredImporter,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            FontSize: 12,
            Surface: LanguageSurface.Plain(ImportLanguage.Notations)),
        new(
            "language-test",
            LanguageDocuments.Testing,
            Arrange: (_, session) => PutCaret(session, CellAddress.OfSlot(session.Document.Id, "name")),
            LineNumbers: true,
            Surface: Testing),
    ];

    /// <summary>The state machine's own language, with its two notations.</summary>
    private static LanguageSurface StateMachine =>
        LanguageSurface.Plain(StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts));

    /// <summary>The feature model's own language.</summary>
    private static LanguageSurface FeatureModel =>
        LanguageSurface.Plain(FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts));

    /// <summary>The SQL schema's own language.</summary>
    private static LanguageSurface SqlSchema =>
        LanguageSurface.Plain(SqlSchemaLanguage.CreateNotations(SqlSchemaLanguage.Concepts));

    /// <summary>The test language a language's own tests are written in.</summary>
    private static LanguageSurface Testing =>
        LanguageSurface.Plain(TestingNotations.Registry);

    /// <summary>Switches the machine to the table of its transitions, which is a view state and not a model change.</summary>
    private static void StateMachineTable(ProjectionalEditorControl editor, EditorSession session)
    {
        session.ChooseVariant(SmConceptKeys.Machine, StateMachineLanguage.TableVariant);

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
    }

    /// <summary>The rule at <paramref name="index"/> of the document's ruleset.</summary>
    private static Node Rule(EditorSession session, int index) =>
        session.Document.GetChildren(RulesLanguage.RulesSlot)[index];

    /// <summary>Puts the caret at the start of the cell at <paramref name="address"/>.</summary>
    private static void PutCaret(EditorSession session, CellAddress address) =>
        session.Select(Selection.At(address));

    /// <summary>Ctrl+Space in the empty condition: the concepts that could stand there.</summary>
    private static void OpenCompletion(ProjectionalEditorControl editor, EditorSession session)
    {
        PutCaret(session, CellAddress.OfSlot(Rule(session, 0).Id, "condition"));

        session.Apply(new OpenCompletionAction());

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
    }

    /// <summary>Alt+Enter on a rule that has both a repair and a language intention.</summary>
    private static void OpenOffers(ProjectionalEditorControl editor, EditorSession session)
    {
        var rule = Rule(session, 0);

        PutCaret(session, CellAddress.OfSlot(rule.Id, "name"));

        var finding = new Diagnostic(
            DiagnosticSeverity.Warning,
            rule.Id,
            "This rule is still named after its concept.",
            role: "name",
            code: "STD001");

        var fix = new DiagnosticFix(
            finding,
            "demo.rename",
            "Rename to \"Review1\"",
            context => context.Node is { } node
                ? new SetNodePropertyAction(node.Id, "name", "Review1")
                : null,
            "Names the rule after the concept it declares.");

        editor.Markers = new MarkerSet(finding);
        editor.Fixes = new FixSet(fix);

        // Registered once, at open, exactly as a host does it: the provider is a source, not a snapshot.
        session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        session.ApplyOffers();

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
    }

    /// <summary>Ctrl+A twice: the value, then the whole rule.</summary>
    private static void SelectSecondRule(ProjectionalEditorControl editor, EditorSession session)
    {
        PutCaret(session, CellAddress.OfSlot(Rule(session, 1).Id, "name"));

        session.Apply(new SelectAllAction());
        session.Apply(new SelectAllAction());

        editor.InvalidateVisual();
    }

    /// <summary>Folds the second rule, so the gutter twisty and the collapsed line are both on show.</summary>
    private static void CollapseSecondRule(ProjectionalEditorControl editor, EditorSession session)
    {
        editor.ShowFoldTwisties = true;

        session.ToggleCollapsed(Rule(session, 1).Id);
        editor.Refresh();
    }

    /// <summary>Types <c>10</c> and then <c>+</c>, which the language turns into an addition.</summary>
    private static void TypeAnAddition(ProjectionalEditorControl editor, EditorSession session)
    {
        PutCaret(session, CellAddress.OfSlot(Rule(session, 0).Id, "condition"));

        session.Apply(new TypeTextAction("1"));
        session.Apply(new TypeTextAction("0"));
        session.Apply(new TypeTextAction("+"));

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
    }

    /// <summary>One finding per severity, so every wave style is in one picture.</summary>
    private static void MarkEverything(ProjectionalEditorControl editor, EditorSession session)
    {
        var first = Rule(session, 0);
        var second = Rule(session, 1);
        var third = Rule(session, 2);
        var condition = first.GetChildren("condition")[0];

        editor.Markers = new MarkerSet(
            new Diagnostic(
                DiagnosticSeverity.Warning,
                first.Id,
                "This rule is still named after its concept.",
                role: "name",
                code: "STD001"),
            new Diagnostic(
                DiagnosticSeverity.Error,
                condition.Id,
                "Comparing a number with a name looks wrong.",
                role: "left",
                code: "STD040"),
            new Diagnostic(
                DiagnosticSeverity.Info,
                second.Id,
                "A review rule needs a reviewer.",
                role: "name",
                code: "PAY002"),
            new Diagnostic(
                DiagnosticSeverity.Hint,
                third.Id,
                "This rule never accepts anything.",
                role: "outcome",
                code: "PAY007"));

        PutCaret(session, CellAddress.OfSlot(first.Id, "name"));
    }

    /// <summary>Hovers the error squiggle, whose tooltip also promises the repair it has.</summary>
    private static void ShowMarkerTip(ProjectionalEditorControl editor, EditorSession session)
    {
        var first = Rule(session, 0);
        var condition = first.GetChildren("condition")[0];

        var finding = new Diagnostic(
            DiagnosticSeverity.Error,
            condition.Id,
            "Comparing a number with a name looks wrong.",
            role: "left",
            code: "STD040");

        editor.Markers = new MarkerSet(finding);
        editor.Fixes = new FixSet(new DiagnosticFix(
            finding,
            "demo.accept",
            "Accept this rule",
            context => context.Node is { } node
                ? new SetNodePropertyAction(node.Id, "outcome", RuleOutcomes.Accept)
                : null,
            "Sets the outcome so this rule decides in favour of whatever it matches."));

        PutCaret(session, CellAddress.OfSlot(first.Id, "name"));
    }

    /// <summary>Reads the ruleset as a table instead of as a block.</summary>
    private static void TableView(ProjectionalEditorControl editor, EditorSession session)
    {
        PutCaret(session, CellAddress.OfSlot(Rule(session, 0).Id, "name"));

        session.ChooseVariant(RulesConceptKeys.Ruleset, RulesLanguage.TableVariant);
        editor.Refresh();
    }
}
