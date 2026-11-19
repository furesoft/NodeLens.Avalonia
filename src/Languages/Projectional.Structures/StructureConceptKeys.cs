namespace Projectional.Structures;

/// <summary>The keys of the structure language: the concepts a language definition is written with.</summary>
/// <remarks>
/// One root and eight concepts, and the small number is the design. A language definition says what a concept
/// <em>is</em> - its key, its slots, what it extends - and nothing about how it looks; how it looks is the editor
/// language, which is a separate model so that a definition can be reviewed without its presentation.
/// </remarks>
public static class StructureConceptKeys
{
    /// <summary>The document: the enumerations and concepts of one language.</summary>
    public const string Language = "Structure.Language";

    /// <summary>One concept: its key, its flags, what it extends and what it holds.</summary>
    public const string Concept = "Structure.Concept";

    /// <summary>One super concept, named by key.</summary>
    public const string SuperType = "Structure.SuperType";

    /// <summary>What a slot is, abstract because a slot is always one of the three kinds.</summary>
    public const string Slot = "Structure.Slot";

    /// <summary>A slot holding a primitive value.</summary>
    public const string PropertyDecl = "Structure.PropertyDecl";

    /// <summary>A slot holding owned children.</summary>
    public const string ChildDecl = "Structure.ChildDecl";

    /// <summary>A slot pointing at a node owned elsewhere.</summary>
    public const string ReferenceDecl = "Structure.ReferenceDecl";

    /// <summary>A named set of symbols, which a property can be declared over.</summary>
    public const string Enumeration = "Structure.Enumeration";

    /// <summary>One symbol of an enumeration.</summary>
    public const string EnumSymbol = "Structure.EnumSymbol";
}
