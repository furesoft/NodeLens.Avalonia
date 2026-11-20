namespace Projectional.ImportRules;

/// <summary>The four kinds of part a rule is built from.</summary>
/// <remarks>
/// One enum rather than four record types, because this is the lowered shape the importer walks and not the language's
/// surface - the four kinds are concepts the author writes, and they are closed on purpose: a part either finds text,
/// fills a property, reads a child or repeats one.
/// </remarks>
public enum ImportPartKind
{
    /// <summary>Text that has to stand here.</summary>
    Literal,

    /// <summary>The run up to the next literal, filled into a property.</summary>
    Capture,

    /// <summary>A child, read by the rule its slot allows.</summary>
    Nest,

    /// <summary>The entries of a list, separated by a literal.</summary>
    Repeat,
}
