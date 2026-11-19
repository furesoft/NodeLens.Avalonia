using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>An importer that exists for one reason: to prove the contract a language package writes against.</summary>
/// <remarks>
/// <para>
/// Deliberately not a language. It appends <c>Test.Rule</c> roots and then either commits, refuses or throws, which is
/// every outcome <see cref="ITextImporter"/> has, and it does it with the plain public API a third-party language has -
/// no <c>InternalsVisibleTo</c>, no detached node, no second seam.
/// </para>
/// <para>
/// Keeping the fixture here rather than borrowing a real language is what makes the contract tests independent of what
/// any one format happens to look like: they go on proving the whole-document-is-one-undo-step promise after the JSON
/// grammar has changed shape.
/// </para>
/// </remarks>
public sealed class ProbeImporter : ITextImporter
{
    private readonly int _nodes;
    private readonly ImportDiagnostic? _refusal;
    private readonly bool _throws;

    public ProbeImporter(int nodes, ImportDiagnostic? refusal = null, bool throws = false)
    {
        _nodes = nodes;
        _refusal = refusal;
        _throws = throws;
    }

    /// <summary>A name no real format has, so a host cannot pick this by accident.</summary>
    public string Format => "probe";

    /// <summary>Every text is a candidate, because the probe is about the contract and not about a grammar.</summary>
    /// <param name="text">Ignored.</param>
    public bool CanImport(string text) => true;

    public ImportResult Import(Model model, string text)
    {
        ArgumentNullException.ThrowIfNull(model);

        // The documented shape of a bulk mutation: one transaction, committed last, so a refusal or a throw leaves
        // nothing behind and a success is a single undo step.
        using var transaction = model.BeginTransaction();

        var roots = new List<Node>();
        for (var index = 0; index < _nodes; index++)
        {
            var root = model.NewNode(TestLanguage.Rule);
            root.Set("name", $"rule{index}");
            roots.Add(root);
        }

        if (_throws)
        {
            throw new InvalidOperationException("The probe gave up half way.");
        }

        if (_refusal is not null)
        {
            return ImportResult.Refused(_refusal);
        }

        transaction.Complete();
        return ImportResult.Imported(roots);
    }
}
