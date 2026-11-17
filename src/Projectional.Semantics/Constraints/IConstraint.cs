namespace Projectional.Semantics;

/// <summary>
/// One rule a language states about a node, as the aspect a concept declares.
/// </summary>
/// <remarks>
/// <para>
/// A constraint <em>reports</em>; it never repairs and never mutates. The repair is a
/// <c>DiagnosticFix</c> built by whoever knows how to fix the finding, which is why a constraint can be
/// written by a language author who has no idea what the editor can do about it.
/// </para>
/// <para>
/// It is an aspect rather than a framework rule because what is wrong is a property of the language: a
/// rule needs a condition, a table needs a header, a pipeline needs a first stage. The engine only knows
/// how to run them and where to put the answers.
/// </para>
/// <para>
/// Constraints must not depend on each other's results, and their order within a concept is unspecified.
/// That is what lets the engine run them in any order - and, later, on several threads.
/// </para>
/// </remarks>
public interface IConstraint
{
    /// <summary>Everything this rule has to say about <see cref="ConstraintContext.Node"/>.</summary>
    /// <param name="context">The node, the scope and the collector to report into.</param>
    void Check(ConstraintContext context);
}
