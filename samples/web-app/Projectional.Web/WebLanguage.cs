using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Semantics;

namespace Projectional.Web;

/// <summary>
/// One language the browser editor can open a document in: everything a session needs, in one value.
/// </summary>
/// <remarks>
/// <para>
/// A host that embeds the editor has to answer the same five questions for every language it offers - the
/// concepts, the notation, what typing means, what a reference resolves against, and which intentions exist -
/// and a browser shell that asked them at each call site would spread one language's wiring over a page. This
/// record is that wiring, declared once per language, and the document takes it as a whole.
/// </para>
/// <para>
/// It holds no session and no model: a document is the instance, a language is the type. Two documents in two
/// tabs share one <see cref="WebLanguage"/> and nothing else, which is what makes closing a tab leave the
/// language alone.
/// </para>
/// </remarks>
public sealed class WebLanguage
{
    /// <summary>The name a tab and the language menu show, and the name a saved document stores.</summary>
    public required string Name { get; init; }

    /// <summary>The language's concepts.</summary>
    public required ConceptRegistry Concepts { get; init; }

    /// <summary>The notation the documents are projected with.</summary>
    public required NotationRegistry Notations { get; init; }

    /// <summary>What the language says about text typed into an empty slot, or <see langword="null"/> for nothing.</summary>
    public ITypingRules? Typing { get; init; }

    /// <summary>The language's type rules, or <see langword="null"/> when it declares none.</summary>
    public TypeRegistry? Types { get; init; }

    /// <summary>The rules the language states about itself, or <see langword="null"/> when it declares none.</summary>
    public ConstraintSet? Constraints { get; init; }

    /// <summary>What decorates a document beyond its notation, or <see langword="null"/> for nothing.</summary>
    public ICellEmphasis? Emphasis { get; init; }

    /// <summary>The scope the language's references resolve against, or <see langword="null"/> for the default chain.</summary>
    public IScopeProvider? Scopes { get; init; }

    /// <summary>The language's own intentions, in the order the list offers them.</summary>
    public IReadOnlyList<IIntentionProvider> Intentions { get; init; } = [];

    /// <summary>Builds the empty document a new tab starts with.</summary>
    public required Func<Model> NewEmpty { get; init; }

    /// <summary>Builds the worked example the menu opens.</summary>
    public required Func<Model> NewSample { get; init; }

    /// <summary>A project whose language is this one, which is what a stored document is read into.</summary>
    public Project NewProject() => new(Concepts);

    /// <summary>A new model: the worked example or the empty one.</summary>
    /// <param name="sample">Whether the example is wanted instead of the empty shape.</param>
    public Model NewModel(bool sample) => sample ? NewSample() : NewEmpty();

    public override string ToString() => Name;
}
