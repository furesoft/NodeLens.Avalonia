namespace Projectional.Language;

/// <summary>One hop from a node to another, which is what a name body navigates by.</summary>
/// <remarks>
/// A step is navigation and nothing else: it answers <em>which node</em>, never what it is called. That separation is
/// what keeps the vocabulary from growing a variant per relation and per reading - a name is
/// <c>name of &lt;path&gt;</c> over any path, so <c>nextSibling</c> is one step rather than a second kind of name
/// (docs/architecture/behaviours.md).
/// </remarks>
public enum PathStep
{
    /// <summary>The node itself, for a reading that is about this node - <c>index of self</c>.</summary>
    Self,

    /// <summary>The node this one sits in.</summary>
    Parent,

    /// <summary>The one child in a slot, which must hold a single child.</summary>
    Child,

    /// <summary>The target of a reference slot.</summary>
    Reference,

    /// <summary>The sibling after this one in the same slot, or nothing at the end of the list.</summary>
    NextSibling,

    /// <summary>The sibling before this one in the same slot, or nothing at the start of the list.</summary>
    PreviousSibling,
}
