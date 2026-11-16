using Projectional.Core;
using Projectional.Editors;
using Projectional.Expressions;
using Projectional.Editors.Rendering;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Structures;
using Projectional.Structures.Notation;

namespace Projectional.Workbench;

/// <summary>
/// The languages the standalone editor knows: the sample rules language, and the two the editor itself is
/// written with.
/// </summary>
/// <remarks>
/// <para>
/// One registry, three languages, composed by borrowing declarations rather than by inheritance - the pattern
/// [ADR 0014](../../docs/adr/0014-both-authoring-paths-are-permanent.md) prescribes for a host that adds a
/// language to its own. A project holds one registry, so any document the editor can open has to be covered by
/// this one list; that is why the definition languages belong here and not in a mode.
/// </para>
/// <para>
/// Loading them is the point of M2 made usable: with these two languages registered, a <c>structure.mps</c> or an
/// <c>editor.mps</c> opens, is edited and is saved by the same editor as the sample - a language definition is a
/// model, so nothing has to be taught about it. Without them such a file would fail to load with "unknown concept",
/// which is exactly the diagnostic that says a host forgot a language.
/// </para>
/// </remarks>
internal static class WorkbenchLanguage
{
    /// <summary>The registry every document of this app is built and loaded against.</summary>
    internal static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        // The rules language borrows the expression vocabulary, so a host composing it has to bring that too -
        // `ConceptDeclarations` is one language's own concepts, not the transitive closure.
        .. ExpressionsLanguage.ConceptDeclarations,
        .. RulesLanguage.ConceptDeclarations,
        .. StructureLanguage.ConceptDeclarations,
        .. EditorLanguage.ConceptDeclarations,
    ]);

    /// <summary>The notation of all three, in one registry.</summary>
    internal static NotationRegistry Notations { get; } = CreateNotations();

    /// <summary>The sample language's emphasis rules, which are the only ones this app has.</summary>
    internal static ICellEmphasis? Emphasis => RulesLanguage.Emphasis;

    /// <summary>The sample language's typing rules, which are the only ones this app has.</summary>
    internal static ITypingRules? Typing => RulesLanguage.TypingRules;

    /// <summary>A project every document of this app can be loaded into.</summary>
    internal static Project NewProject() => new(Concepts);

    /// <summary>A model of the structure language: an empty language definition, ready to be written.</summary>
    internal static Model NewLanguageDefinition()
    {
        var model = NewProject().NewModel("structure");
        var definition = model.NewNode(StructureConceptKeys.Language);

        model.AddRoot(definition);

        return model;
    }

    /// <summary>A model of the editor language: an empty notation, ready to be written.</summary>
    internal static Model NewNotationDefinition()
    {
        var model = NewProject().NewModel("editor");
        var notations = model.NewNode(EditorConceptKeys.Notations);

        model.AddRoot(notations);

        return model;
    }

    private static NotationRegistry CreateNotations()
    {
        var notations = RulesLanguage.CreateNotations(Concepts);

        StructureNotations.AddTo(notations);
        EditorNotations.AddTo(notations);

        return notations;
    }
}
