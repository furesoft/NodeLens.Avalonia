using Projectional.Checks;
using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;

namespace Projectional.Benchmarks;

/// <summary>One language, its documents and the rule sets a host runs on them.</summary>
/// <remarks>
/// A scenario is a language plus a way to build a document of a given size, because "how expensive is checking" has
/// no answer without saying which rules run: a rules document has no references at all, while a feature model is made
/// of them - and the scope lookup a reference costs is what makes such a document more expensive than its node count
/// suggests.
/// </remarks>
internal abstract class Scenario
{
    /// <summary>What the table calls this scenario.</summary>
    public abstract string Name { get; }

    /// <summary>The document sizes to measure, in the scenario's own unit.</summary>
    public abstract IReadOnlyList<int> Sizes { get; }

    /// <summary>The constraints a host runs - the language's own plus the framework's ambiguity rule.</summary>
    public abstract ConstraintSet Constraints { get; }

    /// <summary>The checks a host runs, which is the set the language publishes.</summary>
    public abstract CheckSet Checks { get; }

    /// <summary>The type rules the language declares, which a constraint reads through its context.</summary>
    public abstract TypeRegistry Types { get; }

    /// <summary>The notation registry that draws the built model, which a session needs.</summary>
    public abstract NotationRegistry Notations { get; }

    /// <summary>Builds a document of <paramref name="units"/> units and answers it with its root.</summary>
    public abstract (Model Model, Node Root) Build(int units);

    /// <summary>One unit of the built document, which is the subtree an incremental caller would recheck.</summary>
    public abstract Node Representative(Node root);

    /// <summary>The node an incremental edit would touch, deep in the document so the rebuild has a path to follow.</summary>
    /// <remarks>
    /// The <em>last</em> unit rather than the first, because the last one is the far end of the document's child list:
    /// an edit there dirties the path from the root down to it, which is the most a single property change can cost.
    /// </remarks>
    public abstract Node EditTarget(Node root);
}
