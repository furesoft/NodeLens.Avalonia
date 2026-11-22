using Projectional.Semantics;

namespace Projectional.Expressions.Scopes;

/// <summary>Which functions a call in <see cref="ExpressionsLanguage"/> can reach.</summary>
/// <remarks>
/// <para>
/// One rule, because this vocabulary has one: the functions of the project. A definition is not written inside the
/// expression that uses it - it is a document of the same project, and recursion needs the function to see itself - so
/// <see cref="ScopePlace.Project"/> is the whole answer and the nearest-first walk the other places use would find
/// nothing.
/// </para>
/// <para>
/// The target is the concept rather than the concept key of a particular definition, so a host that extends
/// <see cref="ExpressionsConceptKeys.Function"/> makes its own functions reachable without touching this registry.
/// </para>
/// </remarks>
public static class ExpressionsScopes
{
    /// <summary>The scope providers this language declares.</summary>
    public static ScopeProviderRegistry Registry { get; } = ScopeProviderRegistry.Empty
        .Add(ExpressionsConceptKeys.Call, NamedScopeProvider.For("callee", ScopePlace.Project, ExpressionsConceptKeys.Function));
}
