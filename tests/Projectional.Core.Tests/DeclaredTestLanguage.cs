using Projectional.Core;
using Projectional.Editors;
using Projectional.Language;
using Projectional.Structures;

namespace Projectional.Core.Tests;

/// <summary>
/// <see cref="TestLanguage"/> and <see cref="TestNotations"/>, written as a model instead of as C#.
/// </summary>
/// <remarks>
/// The oracle of the parity rule ([ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md)): the
/// hand-written language is the reference, and this is the same language expressed the other way. Both are lowered
/// to the same kinds of object by the code that ships, so what the parity tests compare is what a user would load.
/// </remarks>
internal static class DeclaredTestLanguage
{
    /// <summary>The declared twin, lowered into the registry a session would use.</summary>
    public static ConceptRegistry Concepts { get; } = Lower();

    /// <summary>The declared twin's notation, lowered into the registry a session would draw with.</summary>
    public static NotationRegistry Notations { get; } = Notation();

    /// <summary>A project for the declared twin, so a document can be built against it.</summary>
    public static Project NewProject() => new(Concepts);

    private static ConceptRegistry Lower()
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            TestLanguage.Tag,
            slots: [definition.Property("label", "string")],
            nameSlot: "label");

        definition.Concept(
            TestLanguage.Rule,
            slots:
            [
                definition.Property("name", "string", defaultValue: "unnamed"),
                definition.Property("enabled", "bool", defaultValue: "true"),
                definition.Child("body", TestLanguage.Body),
                definition.Child("extends", TestLanguage.Rule),
                definition.Reference("tag", TestLanguage.Tag),
                definition.Reference("inherits", TestLanguage.Rule),
                definition.Reference("self", TestLanguage.Rule, allowOwnSubtree: true),
            ],
            nameSlot: "name",
            description: "A named rule with a body and an outcome.",
            collapseSlot: "enabled");

        definition.Concept(
            TestLanguage.Body,
            slots: [definition.Child("steps", TestLanguage.Step, "many"), definition.Child("notes", TestLanguage.Note, "many")]);

        definition.Concept(
            TestLanguage.Step,
            slots:
            [
                definition.Property("text", "string", description: "What the step does."),
                definition.Property("index", "int"),
                definition.Property("due", "date"),
                definition.Property("status", "Test.Status"),
                definition.Child("detail", TestLanguage.Step),
            ]);

        definition.Concept(TestLanguage.Note, slots: [definition.Property("text", "string")]);

        // Declared after the concepts so the enumeration exists when the property names it.
        definition.Enumeration("Test.Status", "draft", "approved");

        return StructureLowering.CreateRegistry(definition.Roots);
    }

    private static NotationRegistry Notation()
    {
        var notation = new NotationBuilder();

        // rule name as tag {
        //   body
        //   extends …
        // }
        var rule = notation.Notation(TestLanguage.Rule);
        var ruleRoot = notation.Cell(notation.SetRoot(rule, EditorConceptKeys.Sequence), EditorConceptKeys.Sequence);

        var header = notation.Cell(ruleRoot, EditorConceptKeys.Line);

        notation.Word(header, EditorConceptKeys.Keyword, "rule");
        notation.Cell(header, EditorConceptKeys.Space);
        notation.Slot(header, EditorConceptKeys.Property, "name");
        notation.Cell(header, EditorConceptKeys.Space);
        notation.Word(header, EditorConceptKeys.Keyword, "as");
        notation.Cell(header, EditorConceptKeys.Space);
        notation.Slot(header, EditorConceptKeys.Reference, "tag");
        notation.Cell(header, EditorConceptKeys.Space);
        notation.Word(header, EditorConceptKeys.Punct, "{");

        var indent = notation.Cell(ruleRoot, EditorConceptKeys.Indent);

        notation.ChildIn(notation.Cell(indent, EditorConceptKeys.Line), "body");

        var extends = notation.Cell(indent, EditorConceptKeys.Line);

        notation.Word(extends, EditorConceptKeys.Keyword, "extends");
        notation.Cell(extends, EditorConceptKeys.Space);
        notation.ChildIn(extends, "extends");

        var close = notation.Cell(ruleRoot, EditorConceptKeys.Line);

        notation.Word(close, EditorConceptKeys.Punct, "}");

        // The body: steps, then notes separated by semicolons.
        var body = notation.Notation(TestLanguage.Body);
        var bodyRoot = notation.SetRoot(body, EditorConceptKeys.Sequence);

        notation.Cell(bodyRoot, EditorConceptKeys.Children, ("slot", "steps"));

        var notes = notation.Cell(bodyRoot, EditorConceptKeys.Collection, ("slot", "notes"));

        notation.Separator(notes, ";");

        // index : text detail
        var step = notation.Notation(TestLanguage.Step);
        var stepLine = notation.SetRoot(step, EditorConceptKeys.Line);

        notation.Slot(stepLine, EditorConceptKeys.Property, "index");
        notation.Cell(stepLine, EditorConceptKeys.Space);
        notation.Word(stepLine, EditorConceptKeys.Punct, ":");
        notation.Cell(stepLine, EditorConceptKeys.Space);
        notation.Slot(stepLine, EditorConceptKeys.Property, "text");
        notation.Cell(stepLine, EditorConceptKeys.Space);
        notation.ChildIn(stepLine, "detail");

        // note text
        var note = notation.Notation(TestLanguage.Note);
        var noteLine = notation.SetRoot(note, EditorConceptKeys.Line);

        notation.Word(noteLine, EditorConceptKeys.Keyword, "note");
        notation.Cell(noteLine, EditorConceptKeys.Space);
        notation.Slot(noteLine, EditorConceptKeys.Property, "text");

        return EditorLowering.CreateRegistry(notation.Roots, Concepts);
    }
}
