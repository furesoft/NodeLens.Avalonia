using Projectional.Core;
using Projectional.Editors;

namespace Projectional.Core.Tests;

/// <summary>
/// Writes a notation the way a language author would: one document per notation, cells created through it.
/// </summary>
/// <remarks>
/// <para>
/// A builder rather than free functions for the same reason <see cref="DefinitionBuilder"/> is one: every cell of a
/// notation has to belong to the same model, so a fixture that built them separately would fail on the first insert
/// for a reason that has nothing to do with the test.
/// </para>
/// <para>
/// One document per notation, because a language project stores one definition per file: the cells of the notation
/// that was started last go into that document, which is what makes <see cref="Notation"/> a boundary rather than a
/// convenience.
/// </para>
/// </remarks>
internal sealed class NotationBuilder
{
    private readonly Project _project = EditorLanguage.NewProject();
    private readonly List<Model> _documents = [];
    private readonly List<Node> _roots = [];

    private Model _current;

    public NotationBuilder() => _current = NewDocument();

    /// <summary>The root of the first notation, which is what a single-notation test lowers.</summary>
    public Node Root => _roots[0];

    /// <summary>The root of every notation, in the order they were written.</summary>
    public IReadOnlyList<Node> Roots => _roots;

    /// <summary>One document per notation, in the order they were written.</summary>
    public IReadOnlyList<Model> Documents => _documents;

    /// <summary>Starts the notation of <paramref name="conceptKey"/> and answers the node.</summary>
    public Node Notation(string conceptKey, string? variant = null)
    {
        if (_current.Roots.Count > 0)
        {
            _current = NewDocument();
        }

        var notation = _current.NewNode(EditorConceptKeys.Notation);

        _current.AddRoot(notation);
        _roots.Add(notation);

        notation.Set(EditorLanguage.ConceptKeySlot, conceptKey);

        if (variant is not null)
        {
            notation.Set("variant", variant);
        }

        return notation;
    }

    /// <summary>Gives <paramref name="notation"/> a root block.</summary>
    public Node SetRoot(Node notation, string conceptKey) =>
        _current.NewChild(notation, EditorLanguage.RootSlot, conceptKey);

    /// <summary>Adds a cell of <paramref name="conceptKey"/> to <paramref name="parent"/>.</summary>
    public Node Cell(Node parent, string conceptKey, params (string Slot, string Value)[] values)
    {
        var cell = _current.NewChild(parent, EditorLanguage.CellSlot, conceptKey);

        foreach (var (slot, value) in values)
        {
            cell.Set(slot, value);
        }

        return cell;
    }

    /// <summary>A keyword or a punctuation mark, which carries its own text.</summary>
    public Node Word(Node parent, string conceptKey, string text) =>
        Cell(parent, conceptKey, ("text", text));

    /// <summary>A property or reference cell, which names a slot of the drawn concept.</summary>
    public Node Slot(Node parent, string conceptKey, string slot) =>
        Cell(parent, conceptKey, ("slot", slot));

    /// <summary>The single child a cell draws.</summary>
    public Node ChildIn(Node parent, string slot)
    {
        // The cell is itself an inline cell of the line; `slot` names the child of the drawn concept.
        var child = _current.NewChild(parent, EditorLanguage.CellSlot, EditorConceptKeys.ChildInline);

        child.Set("slot", slot);

        return child;
    }

    /// <summary>The separator of a collection, which is an inline cell rather than part of its list.</summary>
    public Node Separator(Node collection, string text)
    {
        var separator = _current.NewChild(collection, "separator", EditorConceptKeys.Punct);

        separator.Set("text", text);

        return separator;
    }

    /// <summary>One column of a table, which hangs off the table's own slot rather than off <c>cells</c>.</summary>
    public Node Column(Node table, string slot)
    {
        var column = _current.NewChild(table, EditorLanguage.ColumnSlot, EditorConceptKeys.Column);

        column.Set("slot", slot);

        return column;
    }

    /// <summary>The header of a column, which is the column's own single child rather than a cell of a line.</summary>
    public Node Header(Node column, string conceptKey, string text)
    {
        var header = _current.NewChild(column, "header", conceptKey);

        header.Set("text", text);

        return header;
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

    /// <summary>A new empty document, which the next notation is written into.</summary>
    private Model NewDocument()
    {
        var model = _project.NewModel($"editor{_documents.Count + 1}");

        _documents.Add(model);

        return model;
    }
}
