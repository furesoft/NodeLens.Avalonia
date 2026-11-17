using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// Runs a language's constraints over a document and answers the findings.
/// </summary>
/// <remarks>
/// <para>
/// The engine is the only thing that knows how to walk a document and where to put the answers, which is
/// what keeps a constraint a sentence about one node. It holds no state between calls: a check is a
/// function of the model, so a stale finding cannot exist and a caller decides when to run it again.
/// </para>
/// <para>
/// <see cref="Check(Model)"/> walks every root; <see cref="Check(Node)"/> walks one subtree, which is the
/// seam an incremental caller uses - rechecking the part that changed instead of the document. The
/// findings of a subtree check are that subtree's alone, so a caller that keeps the rest has to merge
/// them itself; that is deliberate, because only the caller knows what it kept.
/// </para>
/// <para>
/// Findings come back in document order, and within one node in the order the constraints were declared.
/// A caller that draws them wants a stable order, and a test that compares them wants one too.
/// </para>
/// </remarks>
public sealed class ConstraintEngine
{
    private readonly ConstraintSet _constraints;
    private readonly IScopeProvider _scopes;
    private readonly TypeRegistry _types;

    /// <summary>Creates an engine for <paramref name="constraints"/>.</summary>
    /// <param name="constraints">The language's rules.</param>
    /// <param name="types">The language's type rules, or <see langword="null"/> when it declares none.</param>
    /// <param name="scopes">
    /// What a reference may point at, or <see langword="null"/> for the default chain. A language that
    /// replaced a level of the chain passes the same provider here, so a constraint and the editor agree
    /// about what is visible.
    /// </param>
    public ConstraintEngine(ConstraintSet constraints, IScopeProvider? scopes = null, TypeRegistry? types = null)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        _constraints = constraints;
        _scopes = scopes ?? DefaultScopeProvider.Instance;
        _types = types ?? TypeRegistry.Empty;
    }

    /// <summary>The rules this engine runs.</summary>
    public ConstraintSet Constraints => _constraints;

    /// <summary>Every finding in <paramref name="model"/>, in document order.</summary>
    /// <param name="model">The document to check; every root is walked, whole subtrees included.</param>
    public IReadOnlyList<Diagnostic> Check(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_constraints.IsEmpty)
        {
            return [];
        }

        List<Diagnostic> findings = [];

        foreach (var root in model.Roots)
        {
            Check(root, findings);
        }

        return findings;
    }

    /// <summary>Every finding in the subtree of <paramref name="node"/>, in document order.</summary>
    /// <param name="node">The node to check, itself included.</param>
    public IReadOnlyList<Diagnostic> Check(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_constraints.IsEmpty)
        {
            return [];
        }

        List<Diagnostic> findings = [];

        Check(node, findings);

        return findings;
    }

    private void Check(Node node, List<Diagnostic> findings)
    {
        foreach (var constraint in _constraints.For(node.Concept))
        {
            var context = new ConstraintContext(node, _scopes, _types);

            constraint.Check(context);

            findings.AddRange(context.Diagnostics);
        }

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Check(child, findings);
        }
    }
}
