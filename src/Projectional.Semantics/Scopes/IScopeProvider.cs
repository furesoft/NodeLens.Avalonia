using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The aspect that answers "what may this reference point at", per concept.
/// </summary>
/// <remarks>
/// <para>
/// It is an aspect rather than a framework rule because what is visible is a property of the language:
/// a rule may see the other rules of its ruleset, a variable may see the parameters of its function,
/// and a language that has neither still needs an answer. <see cref="DefaultScopeProvider"/> is the
/// answer for a language that does not say, and a language replaces any level of it by answering here.
/// </para>
/// <para>
/// The slot is named rather than the cell, because a scope is a fact about the model: the same
/// reference slot has the same scope whether it is drawn in a table, in a line, or not at all.
/// </para>
/// </remarks>
public interface IScopeProvider
{
    /// <summary>What the reference in <paramref name="referenceSlot"/> of <paramref name="node"/> may point at.</summary>
    /// <param name="node">The node that owns the reference.</param>
    /// <param name="referenceSlot">The reference slot being resolved.</param>
    IScope ScopeOf(Node node, string referenceSlot);
}
