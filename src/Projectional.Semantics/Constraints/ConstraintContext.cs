using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// What one constraint sees while it runs: the node, the scope, and the place to report findings.
/// </summary>
/// <remarks>
/// <para>
/// The context is the whole surface a constraint gets, and it is deliberately small. It can read the
/// node, ask what a reference may point at, and report - it cannot mutate, cannot reach the editor and
/// cannot see a cell. A rule that could do any of those would be a second editing path, and a rule that
/// ran on a model nobody is drawing (a load, a batch check) would stop working.
/// </para>
/// <para>
/// Reporting is a method rather than a returned list so that one constraint can say several things about
/// one node without building a collection, and so that the severity is named at the call site where the
/// author is thinking about it.
/// </para>
/// </remarks>
public sealed class ConstraintContext
{
    private readonly List<Diagnostic> _diagnostics = [];

    internal ConstraintContext(Node node, IScopeProvider scopes, TypeRegistry types)
    {
        Node = node;
        Scopes = scopes;
        Types = types;
    }

    /// <summary>The node being checked.</summary>
    public Node Node { get; }

    /// <summary>What a reference may point at, which is the aspect a language replaces.</summary>
    public IScopeProvider Scopes { get; }

    /// <summary>The language's type rules, so a rule can ask what a child evaluates to.</summary>
    public TypeRegistry Types { get; }

    /// <summary>What <paramref name="node"/> evaluates to, or <see cref="UnknownType"/> when nothing says.</summary>
    public IType TypeOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return Types.TypeOf(node);
    }

    /// <summary>Everything reported so far, in the order it was reported.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>What the reference in <paramref name="referenceSlot"/> of the node may point at.</summary>
    public IScope ScopeOf(string referenceSlot) => Scopes.ScopeOf(Node, referenceSlot);

    /// <summary>Reports something that is wrong.</summary>
    /// <param name="message">What is wrong, in one sentence the user can act on.</param>
    /// <param name="role">The slot of the node to point at, or <see langword="null"/> for the node itself.</param>
    /// <param name="code">The rule's own identifier, for grouping and suppression.</param>
    public void Error(string message, string? role = null, string? code = null) =>
        Report(DiagnosticSeverity.Error, Node, message, role, code);

    /// <summary>Reports something that is probably wrong.</summary>
    /// <param name="message">What is wrong, in one sentence the user can act on.</param>
    /// <param name="role">The slot of the node to point at, or <see langword="null"/> for the node itself.</param>
    /// <param name="code">The rule's own identifier, for grouping and suppression.</param>
    public void Warning(string message, string? role = null, string? code = null) =>
        Report(DiagnosticSeverity.Warning, Node, message, role, code);

    /// <summary>Reports something worth pointing out.</summary>
    /// <param name="message">What is worth pointing out, in one sentence.</param>
    /// <param name="role">The slot of the node to point at, or <see langword="null"/> for the node itself.</param>
    /// <param name="code">The rule's own identifier, for grouping and suppression.</param>
    public void Hint(string message, string? role = null, string? code = null) =>
        Report(DiagnosticSeverity.Hint, Node, message, role, code);

    /// <summary>
    /// Reports a finding about <paramref name="target"/>, which is how a rule on a parent points at the
    /// child that is actually wrong.
    /// </summary>
    /// <param name="severity">How much it matters.</param>
    /// <param name="target">The node the finding is about.</param>
    /// <param name="message">What is wrong, in one sentence the user can act on.</param>
    /// <param name="role">The slot of <paramref name="target"/> to point at.</param>
    /// <param name="code">The rule's own identifier, for grouping and suppression.</param>
    public void Report(
        DiagnosticSeverity severity,
        Node target,
        string message,
        string? role = null,
        string? code = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(message);

        _diagnostics.Add(new Diagnostic(severity, target.Id, message, role, code: code));
    }
}
