using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Core.Tests;

/// <summary>A check that reports one fixed finding, so a test can say what composition and order do.</summary>
/// <remarks>
/// The standard catalogue's own checks each need a language to find anything; a check with nothing to
/// inspect but the model itself is what makes <see cref="CheckSet"/> testable without a fixture.
/// </remarks>
internal sealed class StubCheck : ICheck
{
    private readonly string _message;

    internal StubCheck(string message) => _message = message;

    /// <summary>How many times this check has been asked, which is what "runs every check" means.</summary>
    internal int Calls { get; private set; }

    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Calls++;

        if (model.Roots.Count == 0)
        {
            return [];
        }

        var target = model.Roots[0].Id;
        var diagnostic = new Diagnostic(DiagnosticSeverity.Info, target, _message);

        return [new DiagnosticFix(diagnostic, $"stub.{_message}", _message, _ => null)];
    }
}
