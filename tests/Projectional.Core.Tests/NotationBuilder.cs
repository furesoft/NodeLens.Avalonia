using Projectional.Core;
using Projectional.Editors;

namespace Projectional.Core.Tests;

/// <summary>
/// Writes a notation the way a language author would: one model, cells created through it.
/// </summary>
/// <remarks>
/// A builder rather than free functions for the same reason <see cref="DefinitionBuilder"/> is one: every cell of
/// a notation has to belong to the same model, so a fixture that built them separately would fail on the first
/// insert for a reason that has nothing to do with the test.
/// </remarks>
internal sealed class NotationBuilder
{
    private readonly Model _model = EditorLanguage.NewModel();

    public NotationBuilder() => Root = _model.NewNode(EditorConceptKeys.Notations);

    /// <summary>The root of the notation document.</summary>
    public Node Root { get; }

    /// <summary>Starts the notation of <paramref name="conceptKey"/> and answers the node.</summary>
    public Node Notation(string conceptKey, string? variant = null)
    {
        var notation = _model.NewChild(Root, EditorLanguage.NotationSlot, EditorConceptKeys.Notation);

        notation.Set(EditorLanguage.ConceptKeySlot, conceptKey);

        if (variant is not null)
        {
            notation.Set("variant", variant);
        }

        return notation;
    }

    /// <summary>Gives <paramref name="notation"/> a root block.</summary>
    public Node SetRoot(Node notation, string conceptKey) =>
        _model.NewChild(notation, EditorLanguage.RootSlot, conceptKey);

    /// <summary>Adds a cell of <paramref name="conceptKey"/> to <paramref name="parent"/>.</summary>
    public Node Cell(Node parent, string conceptKey, params (string Slot, string Value)[] values)
    {
        var cell = _model.NewChild(parent, EditorLanguage.CellSlot, conceptKey);

        foreach (var (slot, value) in values)
        {
            cell.Set(slot, value);
        }

        return cell;
    }

    /// <summary>A line holding the given inline cells.</summary>
    public Node Line(Node parent, params string[] cells)
    {
        var line = Cell(parent, EditorConceptKeys.Line);

        foreach (var cell in cells)
        {
            Cell(line, cell);
        }

        return line;
    }

    /// <summary>A sequence holding the given blocks.</summary>
    public Node Sequence(Node parent, params Node[] blocks)
    {
        var sequence = Cell(parent, EditorConceptKeys.Sequence);

        foreach (var block in blocks)
        {
            sequence.AddChild(EditorLanguage.CellSlot, block);
        }

        return sequence;
    }
}
