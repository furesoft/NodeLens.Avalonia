namespace Projectional.Semantics;

/// <summary>Which stretch of a document a scope draws its names from.</summary>
/// <remarks>
/// <para>
/// The four places are the levels <see cref="DefaultScopeProvider"/> already has, named so a language can pick one
/// as data instead of writing a provider: what is written next to the reference, what encloses it, what the document
/// declares, and what the project declares. <see cref="Everywhere"/> is the whole chain in that order, which is what
/// the default provider answers - so "everywhere" and "say nothing" are the same scope.
/// </para>
/// <para>
/// A place is a place, not a filter: which nodes inside it are visible is
/// <see cref="NamedScopeProvider"/>'s second question, and the reason this enum stays this small.
/// </para>
/// </remarks>
public enum ScopePlace
{
    /// <summary>The node's own children and its siblings.</summary>
    Local,

    /// <summary>The same for every enclosing node, nearest first.</summary>
    Ancestors,

    /// <summary>Every root of the model.</summary>
    Model,

    /// <summary>Every root of every other model in the project.</summary>
    Project,

    /// <summary>All four, in the order above - the chain a language gets when it says nothing.</summary>
    Everywhere,
}
