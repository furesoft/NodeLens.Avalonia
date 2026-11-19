namespace Projectional.ScopeRules;

/// <summary>The five places a declared scope can draw its names from, as the symbols a model stores.</summary>
/// <remarks>
/// The spelling is the model's, not the enum's: a rule is data, so these are what an author writes in a
/// <c>scopes</c> document and what the file contains, and <see cref="ScopeLowering"/> is the only place that maps
/// them onto <see cref="Projectional.Semantics.ScopePlace"/>. Declaration order is the order completion offers them
/// in, and it is the order of the chain for <see cref="Everywhere"/>.
/// </remarks>
public static class ScopePlaces
{
    /// <summary>What is around the reference: its node's children and its siblings.</summary>
    public const string Local = "local";

    /// <summary>The same for every enclosing node, nearest first.</summary>
    public const string Ancestors = "ancestors";

    /// <summary>Every root of the model the node lives in.</summary>
    public const string Model = "model";

    /// <summary>Every root of every other model in the same project.</summary>
    public const string Project = "project";

    /// <summary>All four, in that order - the answer a language gives by saying nothing.</summary>
    public const string Everywhere = "everywhere";
}
