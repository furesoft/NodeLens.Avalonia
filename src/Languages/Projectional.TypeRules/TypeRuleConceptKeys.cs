namespace Projectional.TypeRules;

/// <summary>The keys of the type-rule language: the concepts a type rule is written with.</summary>
/// <remarks>
/// One root and the concepts beneath it, and the small number is the design. A type rule says what a concept
/// evaluates to - a fixed type, the type of one of its children, or a choice between those two decided by
/// a property - and nothing about how it is computed. That is the declarative subset of MPS' typesystem:
/// enough to state the rules a DSL actually has, and not a programming language.
/// <para>
/// The second half is the type expressions: a type is written as data too, so a rule can say what a
/// concept evaluates to when it is not one of the four primitives. They are deliberately a separate
/// family from the bodies - a body may read the node (<c>type of left</c>), a type may not, and mixing
/// them would allow a record field whose type depends on the node that holds the rule.
/// </para>
/// </remarks>
public static class TypeRuleConceptKeys
{
    /// <summary>The document: the rules of one language.</summary>
    public const string Rules = "TypeRule.Rules";

    /// <summary>One rule: the concept it is about and what that concept evaluates to.</summary>
    public const string Rule = "TypeRule.Rule";

    /// <summary>What a rule answers, abstract because a body is always one of the three kinds.</summary>
    public const string Body = "TypeRule.Body";

    /// <summary>A type named outright.</summary>
    public const string Fixed = "TypeRule.Fixed";

    /// <summary>The type of one of the node's children.</summary>
    public const string Child = "TypeRule.Child";

    /// <summary>A choice between bodies, decided by a property's value.</summary>
    public const string Switch = "TypeRule.Switch";

    /// <summary>One value of a switch, and the body it selects.</summary>
    public const string Case = "TypeRule.Case";

    /// <summary>A type written as data, abstract because it is always one of the kinds below.</summary>
    public const string TypeExpr = "TypeRule.TypeExpr";

    /// <summary>One of the framework's primitives, named.</summary>
    public const string Primitive = "TypeRule.Primitive";

    /// <summary>A record: a fixed set of named fields.</summary>
    public const string Record = "TypeRule.Record";

    /// <summary>One field of a record: a name and the type it holds.</summary>
    public const string Field = "TypeRule.Field";

    /// <summary>A list of one element type.</summary>
    public const string List = "TypeRule.List";

    /// <summary>A value that may be absent.</summary>
    public const string Optional = "TypeRule.Optional";

    /// <summary>A type the language declared under a name.</summary>
    public const string Named = "TypeRule.Named";

    /// <summary>A type expression that names a declared type.</summary>
    public const string Ref = "TypeRule.Ref";

    /// <summary>Either of two types, as a type expression.</summary>
    public const string Join = "TypeRule.Join";

    /// <summary>Both of two types at once, as a type expression.</summary>
    public const string Meet = "TypeRule.Meet";

    /// <summary>A body that states relations instead of answering a type.</summary>
    public const string Equations = "TypeRule.Equations";

    /// <summary>One relation a rule states between two type expressions.</summary>
    public const string Equation = "TypeRule.Equation";

    /// <summary>A type expression the solver gives a type to.</summary>
    public const string TypeVariable = "TypeRule.TypeVariable";

    /// <summary>A body that reports instead of answering a type.</summary>
    public const string Complain = "TypeRule.Complain";

    /// <summary>
    /// One declared edge of the subtype relation: a type that may stand where another is required.
    /// </summary>
    public const string Supertypes = "TypeRule.Supertypes";

    /// <summary>
    /// A condition a complaint states: the types of two child slots are not interchangeable.
    /// </summary>
    public const string NotComparable = "TypeRule.NotComparable";

    /// <summary>
    /// One declared rewrite: a claim of a relation between two type patterns is replaced by other relations.
    /// </summary>
    public const string Replacement = "TypeRule.Replacement";

    /// <summary>
    /// Two types a language declares interchangeable, without claiming either is a subtype of the other.
    /// </summary>
    public const string Comparable = "TypeRule.Comparable";
}
