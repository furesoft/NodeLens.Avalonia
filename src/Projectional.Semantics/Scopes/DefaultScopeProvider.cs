using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The scope chain a language gets when it says nothing: local, then the enclosing blocks, then the
/// model, then the project.
/// </summary>
/// <remarks>
/// <para>
/// The order is the one <c>docs/architecture/language.md</c> fixes, and each level is a rule a language
/// can replace by answering <see cref="IScopeProvider"/> itself:
/// </para>
/// <list type="number">
/// <item>the node's own children and its siblings - what is written next to the reference;</item>
/// <item>the same for every enclosing ancestor, nearest first - lexical nesting, like a block;</item>
/// <item>every root of the model - what the document declares;</item>
/// <item>every root of every model in the project - what the project declares.</item>
/// </list>
/// <para>
/// Only named nodes contribute, and the node itself is never in its own scope. Both rules, and the order of the
/// levels, live in <see cref="ScopeWalk"/> - the same walk <see cref="NamedScopeProvider"/> uses, so a language that
/// asks for a place and a language that says nothing cannot disagree about what a place contains.
/// </para>
/// </remarks>
public sealed class DefaultScopeProvider : IScopeProvider
{
    /// <summary>The shared instance, since the chain holds no state.</summary>
    public static DefaultScopeProvider Instance { get; } = new();

    /// <inheritdoc />
    public IScope ScopeOf(Node node, string referenceSlot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(referenceSlot);

        return ScopeChain.Of([.. ScopeWalk.Levels(node, ScopePlace.Everywhere, conceptKey: null)]);
    }
}
