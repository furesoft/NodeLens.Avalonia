namespace Projectional.Behaviours;

/// <summary>The keys of the behaviour language: the concepts a name is written with.</summary>
/// <remarks>
/// A behaviour is one concept's pure function declared as data, lowered into the delegate a
/// <c>ConceptDeclaration</c> takes (docs/architecture/behaviours.md). The vocabulary is closed for the reason the
/// template and constraint vocabularies are: a part reads or chooses, and never computes.
/// </remarks>
public static class BehaviourConceptKeys
{
    /// <summary>One concept's behaviour: the concept it is for and the functions it declares.</summary>
    public const string Document = "Behaviour.Document";

    /// <summary>One function: which of the closed set it is, and the body that answers it.</summary>
    public const string Function = "Behaviour.Function";

    /// <summary>A part of a body, abstract because it is always one of the kinds below.</summary>
    public const string Part = "Behaviour.Part";

    /// <summary>A fixed word.</summary>
    public const string Literal = "Behaviour.Literal";

    /// <summary>A property's value, as text.</summary>
    public const string Property = "Behaviour.Property";

    /// <summary>The name of the one child in a slot: how a composite name is written.</summary>
    public const string Child = "Behaviour.Child";

    /// <summary>The name of a reference's target.</summary>
    public const string Reference = "Behaviour.Reference";

    /// <summary>The text a reference stored when it was written, for a document whose target is not loaded.</summary>
    public const string ResolveInfo = "Behaviour.ResolveInfo";

    /// <summary>Parts one after another, with a separator, empty ones left out.</summary>
    public const string Join = "Behaviour.Join";

    /// <summary>The names of the children in a slot, with a separator, empty ones left out.</summary>
    public const string Children = "Behaviour.Children";

    /// <summary>A part answered only while a slot holds something.</summary>
    public const string WhenFilled = "Behaviour.WhenFilled";

    /// <summary>A chain of steps to another node, which the two readings answer about.</summary>
    public const string Path = "Behaviour.Path";

    /// <summary>The path that stays put, for a reading about this node.</summary>
    public const string PathSelf = "Behaviour.PathSelf";

    /// <summary>The step to the node this one sits in.</summary>
    public const string PathParent = "Behaviour.PathParent";

    /// <summary>The step to the one child in a slot.</summary>
    public const string PathChild = "Behaviour.PathChild";

    /// <summary>The step to a reference's target.</summary>
    public const string PathReference = "Behaviour.PathReference";

    /// <summary>The step to the sibling after this one.</summary>
    public const string PathNextSibling = "Behaviour.PathNextSibling";

    /// <summary>The step to the sibling before this one.</summary>
    public const string PathPreviousSibling = "Behaviour.PathPreviousSibling";

    /// <summary>The name of the node a path reaches.</summary>
    public const string NameOfPath = "Behaviour.NameOfPath";

    /// <summary>The position of the node a path reaches, counted from one.</summary>
    public const string IndexOfPath = "Behaviour.IndexOfPath";

    /// <summary>The first non-empty part.</summary>
    public const string Fallback = "Behaviour.Fallback";

    /// <summary>One of two parts, by a property's value.</summary>
    public const string Choose = "Behaviour.Choose";

    /// <summary>The property holding the key of the concept a document is for.</summary>
    public const string ConceptSlot = "concept";

    /// <summary>The slot holding a document's functions.</summary>
    public const string FunctionsSlot = "functions";

    /// <summary>The property holding which function this is.</summary>
    public const string KindSlot = "kind";

    /// <summary>The slot holding a function's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The property holding a literal's text.</summary>
    public const string TextSlot = "text";

    /// <summary>The property naming the slot a part reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The property holding the text a join puts between two parts.</summary>
    public const string SeparatorSlot = "separator";

    /// <summary>The slot holding the parts a join or a fallback reads.</summary>
    public const string PartsSlot = "parts";

    /// <summary>The property holding the value a choice compares against.</summary>
    public const string ValueSlot = "value";

    /// <summary>The slot holding the part a choice answers while the property holds the value.</summary>
    public const string ThenSlot = "then";

    /// <summary>The slot holding the part a <c>whenFilled</c> answers while the slot it asks about is filled.</summary>
    public const string PartSlot = "part";

    /// <summary>The slot holding the steps a reading answers about.</summary>
    public const string PathSlot = "path";

    /// <summary>The slot holding the rest of a path, which is what chains two steps.</summary>
    public const string NextSlot = "next";

    /// <summary>The slot holding the part a choice answers otherwise.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>The kind of the name function, which is the one this slice lowers.</summary>
    public const string NameKind = "name";

    /// <summary>The kind of the describe function, which is planned and refused for now.</summary>
    public const string DescribeKind = "describe";
}
