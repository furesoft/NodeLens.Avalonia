namespace Projectional.IntentionRules;

/// <summary>The keys of the intention language: the concepts a language's own intentions are written with.</summary>
/// <remarks>
/// A rule says what an entry is called and what it does, and the doing is one of a closed set of bodies - a wrap with
/// the values the wrapper is born with, a property value, a sibling, a child in an empty slot, a delete, a move, or a
/// replace. The set is closed for the same reason the constraint and template languages are: an intention produces an
/// <c>EditAction</c>, and an expression over the model would turn a declaration into a program the framework has to
/// interpret.
/// </remarks>
public static class IntentionConceptKeys
{
    /// <summary>One intention: which concept it is offered on, how it reads, and what it does.</summary>
    public const string Rule = "Intent.Rule";

    /// <summary>What an intention does, abstract because it is always one of the bodies below.</summary>
    public const string Action = "Intent.Action";

    /// <summary>Surrounds the node with a wrapper concept, born with the values the rule lists.</summary>
    public const string Wrap = "Intent.Wrap";

    /// <summary>One value the wrapper is born with.</summary>
    public const string Preset = "Intent.Preset";

    /// <summary>Inserts a named concept into the empty slot at the caret.</summary>
    public const string Insert = "Intent.Insert";

    /// <summary>Inserts a sibling of the node the caret is on.</summary>
    public const string InsertSibling = "Intent.InsertSibling";

    /// <summary>Deletes the node the caret is on.</summary>
    public const string Delete = "Intent.Delete";

    /// <summary>Moves the node among its siblings.</summary>
    public const string Move = "Intent.Move";

    /// <summary>Replaces the node with a node of another concept.</summary>
    public const string Replace = "Intent.Replace";

    /// <summary>Writes a property value on the node the caret is on.</summary>
    public const string Set = "Intent.Set";
}
