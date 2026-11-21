namespace Projectional.Structures;

/// <summary>The keys of the structure language: the concepts a language definition is written with.</summary>
/// <remarks>
/// One root and eight concepts, and the small number is the design. A language definition says what a concept
/// <em>is</em> - its key, its slots, what it extends - and nothing about how it looks; how it looks is the editor
/// language, which is a separate model so that a definition can be reviewed without its presentation.
/// </remarks>
public static class StructureConceptKeys
{
    /// <summary>The language root: what a language is called, which version it carries and what it stands on.</summary>
    /// <remarks>
    /// The one concept that is not a definition. It is the root of <c>language.mps</c> at the project root - the
    /// file every other document of the project is a part of - and no document under an aspect folder may hold it.
    /// </remarks>
    public const string Language = "Structure.Language";

    /// <summary>One language this one depends on or extends, named and optionally versioned.</summary>
    public const string Dependency = "Structure.Dependency";

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
