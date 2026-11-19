namespace Projectional.ScopeRules;

/// <summary>The keys of the scope language: the concepts a language's own scopes are written with.</summary>
/// <remarks>
/// One concept, because a scope rule names rather than computes: which concept, which reference, which place, and
/// optionally which concept the visible nodes must be. A body vocabulary like the constraint language's would be
/// wrong here - a scope is a fact about where names are written, not a question about a node - and a general
/// expression would turn the aspect into an interpreter.
/// </remarks>
public static class ScopeConceptKeys
{
    /// <summary>One rule: whose reference it answers for, and where that reference's names come from.</summary>
    public const string Rule = "Scope.Rule";
}
