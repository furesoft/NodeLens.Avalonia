namespace Projectional.ConstraintRules;

/// <summary>The keys of the constraint language: the concepts a language's own rules are written with.</summary>
/// <remarks>
/// <para>
/// Six concepts, and the shape follows MPS' typesystem in the one way this framework can honour: a rule names the
/// node it is about and says what is wrong, and the <em>check</em> is one of a closed set of bodies rather than a
/// statement list. A body either asks the node a question - is this slot empty, is this value still the default -
/// or it does not exist, which keeps a declared rule from being a programming language hiding in a model.
/// </para>
/// <para>
/// A document holds exactly one rule, like every other definition folder: one rule per file, so a diff of a rule is
/// a diff of one file, and the file name order is the order the rules report in.
/// </para>
/// </remarks>
public static class ConstraintConceptKeys
{
    /// <summary>One rule: the concept it is about, what is wrong, how much it matters, and what it checks.</summary>
    public const string Rule = "Constraint.Rule";

    /// <summary>What a rule checks, abstract because it is always one of the four kinds below.</summary>
    public const string Body = "Constraint.Body";

    /// <summary>A single-child slot that holds nothing.</summary>
    public const string EmptySlot = "Constraint.EmptySlot";

    /// <summary>A property whose value is still the one the concept declares for it.</summary>
    public const string DefaultValue = "Constraint.DefaultValue";

    /// <summary>A value with whitespace in front of or behind it.</summary>
    public const string SurroundingWhitespace = "Constraint.SurroundingWhitespace";

    /// <summary>A reference that points at nothing.</summary>
    public const string EmptyReference = "Constraint.EmptyReference";
}
