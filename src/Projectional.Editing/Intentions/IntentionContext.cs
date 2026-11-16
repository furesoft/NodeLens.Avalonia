using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>What an intention may look at while deciding whether it applies and what it would do.</summary>
/// <remarks>
/// <para>
/// The same shape an <see cref="EditAction"/> gets, minus everything that can change the model. That omission
/// is the point: an intention that could add a child while it was still deciding whether to offer itself would
/// make "which offers exist" a question with side effects, and the list is derived on every paint. Deciding
/// and applying stay separate - the intention <em>produces</em> an action, and
/// <see cref="EditorSession.Apply(Intention)"/> runs it.
/// </para>
/// <para>
/// The caret is resolved once, before any provider is asked, so every provider in one list sees the same
/// place. <see cref="Node"/> is the node that owns the cell the caret stands in - which is what makes an
/// empty slot answer with its owner: an empty slot has no concept of its own, and "insert a rule here" is an
/// intention about the parent.
/// </para>
/// </remarks>
public sealed class IntentionContext
{
    internal IntentionContext(
        Node document,
        EditorTree tree,
        CellLayout layout,
        ITextMeasure measure,
        Selection selection,
        CaretPlace? place)
    {
        Document = document;
        Tree = tree;
        Layout = layout;
        Measure = measure;
        Selection = selection;
        Place = place;
    }

    /// <summary>The node being edited, which is the root of the tree.</summary>
    public Node Document { get; }

    /// <summary>The tree the caret was resolved against.</summary>
    public EditorTree Tree { get; }

    /// <summary>The layout of <see cref="Tree"/>.</summary>
    public CellLayout Layout { get; }

    /// <summary>The measure the layout was computed with.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>The selection as it was when the list was asked.</summary>
    public Selection Selection { get; }

    /// <summary>The caret stop the selection's caret resolved to, or <see langword="null"/>.</summary>
    /// <remarks>
    /// Read through this rather than through <see cref="Selection"/> when the question is about the cell -
    /// "is this an empty slot", "what does it say" - because only the resolved place knows the cell, and the
    /// position alone cannot tell two cells with one address apart.
    /// </remarks>
    public CaretPlace? Place { get; }

    /// <summary>The address of the cell the caret stands in.</summary>
    public CellAddress Address => Selection.Caret.Address;

    /// <summary>The node that owns that cell, or <see langword="null"/> when it is no longer in the model.</summary>
    /// <remarks>
    /// For a caret on a keyword or a value this is the node those belong to; for a caret in an empty slot it is
    /// the node the slot belongs to. An id that no longer resolves answers <see langword="null"/> rather than
    /// throwing, because a provider can outlive the model it was asked about.
    /// </remarks>
    public Node? Node => Document.Model.Project.FindNode(Address.Owner);

    /// <summary>The concept of <see cref="Node"/>, or <see langword="null"/>.</summary>
    public Concept? Concept => Node?.Concept;

    /// <summary>Whether the caret stands in a slot waiting to be filled.</summary>
    public bool IsEmptySlot => Place is { IsEmptySlot: true };

    /// <summary>The concepts of the language the document is written in.</summary>
    public ConceptRegistry Concepts => Document.Model.Concepts;

    public override string ToString() => $"{Address} ({Concept?.Key ?? "<gone>"})";
}
