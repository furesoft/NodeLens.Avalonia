namespace Projectional.TextGen;

/// <summary>The keys of the generator language: the concepts a generator is written with.</summary>
/// <remarks>
/// <para>
/// Sixteen concepts, and the shape is MPS': one document holds any number of <em>generators</em>, each generator maps
/// concepts to templates, and a template is a list of parts. A language therefore declares as many generators as it
/// has targets - the same rule table as JSON, as C#, as a second C# version - which is why the document's root is a
/// list of them and not a single generator.
/// </para>
/// <para>
/// The part vocabulary is closed and small for the reason the template itself is: a part either emits, reads, expands
/// or chooses. There is deliberately no "compute", no variable and no counter, so a declared generator can never be a
/// programming language hiding in a model.
/// </para>
/// </remarks>
public static class TextGenConceptKeys
{
    /// <summary>The document: every generator of one language.</summary>
    public const string Generators = "TextGen.Generators";

    /// <summary>One generator: its name and the template of each concept.</summary>
    public const string Generator = "TextGen.Generator";

    /// <summary>One concept's template within a generator.</summary>
    public const string Mapping = "TextGen.Mapping";

    /// <summary>An ordered list of parts.</summary>
    public const string Template = "TextGen.Template";

    /// <summary>One part of a template, abstract because it is always one of the kinds below.</summary>
    public const string Part = "TextGen.Part";

    /// <summary>Literal text.</summary>
    public const string Text = "TextGen.Text";

    /// <summary>A property's value, emitted as stored.</summary>
    public const string Property = "TextGen.Property";

    /// <summary>A reference's target name, emitted as the projection would draw it.</summary>
    public const string Reference = "TextGen.Reference";

    /// <summary>The generated text of the single child in a slot.</summary>
    public const string Child = "TextGen.Child";

    /// <summary>Every child of a list slot, separated by a template.</summary>
    public const string Children = "TextGen.Children";

    /// <summary>One repetition per child, with a body applied to each.</summary>
    public const string Loop = "TextGen.Loop";

    /// <summary>A template emitted one indentation level deeper.</summary>
    public const string Indent = "TextGen.Indent";

    /// <summary>A line break at the current indentation.</summary>
    public const string Line = "TextGen.Line";

    /// <summary>A choice between two templates, decided by a property's value.</summary>
    public const string Conditional = "TextGen.Conditional";

    /// <summary>A choice between two templates, decided by whether a slot holds anything.</summary>
    public const string WhenFilled = "TextGen.WhenFilled";

    /// <summary>The node's own template, applied to itself.</summary>
    public const string Self = "TextGen.Self";
}
