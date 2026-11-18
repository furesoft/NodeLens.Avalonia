using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Samples.Checks;

namespace HostApp;

/// <summary>
/// The whole host: one document, one control, and the three things a shell does with a document - check it, run it,
/// generate its text.
/// </summary>
/// <remarks>
/// <para>
/// Every call here is one a third party could make, which is what the sample exists to show. The composition is
/// three lines - the language's concept registry, its notation registry, its typing rules - and the rest is the
/// window's own business, exactly as [integration.md](../../docs/architecture/integration.md) promises.
/// </para>
/// <para>
/// The findings are recomputed after every change rather than patched, because a diagnostic is a snapshot of an
/// opinion about a document that has since moved. The same reason makes a run's report something to drop rather
/// than to update: it describes a model that may no longer exist.
/// </para>
/// </remarks>
internal sealed class HostWindow : Window
{
    private const string Inputs = "amount=1500";

    private readonly ProjectionalEditorControl _editor = new();
    private readonly TextBlock _output = new()
    {
        FontFamily = new FontFamily("Consolas, Menlo, monospace"),
        TextWrapping = TextWrapping.NoWrap,
    };

    private readonly Model _model;
    private readonly EditorSession _session;

    public HostWindow()
    {
        Title = "Host application - the embedded projectional editor";
        Width = 1040;
        Height = 640;

        _model = PayrollSample.NewSampleModel();

        // The language, composed by the host from the packages it references. Nothing here is editor knowledge.
        var notations = RulesLanguage.CreateNotations(RulesLanguage.Concepts);
        _session = _editor.OpenSession(_model.Roots[0], notations, RulesLanguage.TypingRules);

        // A change is the host's cue to look again; the control repaints itself.
        _model.Changed += (_, _) => Check();

        Content = Layout();
        Closed += (_, _) => _session.Dispose();

        Check();
        Show("Open the document, edit it, then press Check, Run or Generate.");
    }

    private Grid Layout()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
        };

        toolbar.Children.Add(Button("Check", Check));
        toolbar.Children.Add(Button("Run (" + Inputs + ")", Run));
        toolbar.Children.Add(Button("Generate C#", Generate));
        toolbar.Children.Add(Button("Break a rule", BreakARule));

        var output = new ScrollViewer
        {
            MaxHeight = 160,
            Content = new Border
            {
                Background = Brushes.WhiteSmoke,
                Padding = new Thickness(8),
                Child = _output,
            },
        };

        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        page.Children.Add(toolbar);
        page.Children.Add(_editor);
        page.Children.Add(output);

        Grid.SetRow(toolbar, 0);
        Grid.SetRow(_editor, 1);
        Grid.SetRow(output, 2);

        return page;
    }

    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();

        return button;
    }

    private void Check()
    {
        // Repairs first (a check always repairs), then the language's constraints, then the framework's own
        // ambiguity rule - one list, because squiggles and a list that disagreed would be two answers.
        var findings = DocumentFindings.Of(
            _model,
            DuplicateRuleNames.Of(_model),
            SampleConstraints.All,
            ExpressionsLanguage.Types,
            _session.Scopes);

        _editor.Markers = findings.Markers;
        _editor.Fixes = findings.Fixes;

        Show(findings.Diagnostics.Count == 0
            ? "No findings: the document is clean."
            : string.Join(Environment.NewLine, findings.Diagnostics.Select(finding => finding.Message)));
    }

    private void Run()
    {
        var report = DocumentRun.Of(_model.Roots[0], RulesEvaluation.Registry, Inputs);

        Show(string.Join(Environment.NewLine, new[] { report.Headline }.Concat(report.Rows.Select(row => row.Text))));
    }

    private void Generate()
    {
        var generated = DocumentGenerate.Generate(_model.Roots[0], RulesTextGenerators.CSharp);
        var report = DocumentGenerate.Report(generated, RulesTextGenerators.CSharp);

        Show(string.Join(Environment.NewLine, new[] { report.Headline }.Concat(report.Rows.Select(row => row.Text))));
    }

    /// <summary>
    /// Adds a rule with no condition, which the language reports and the framework's own check repairs - so both
    /// halves of a finding are visible without editing by hand.
    /// </summary>
    private void BreakARule()
    {
        using var transaction = _model.BeginTransaction();
        _model.NewChild(_model.Roots[0], RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        transaction.Complete();

        Check();
    }

    private void Show(string text) => _output.Text = text;
}
