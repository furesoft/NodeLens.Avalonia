using System.Text;

using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;
using VtNetCore.XTermParser;

namespace Projectional.Workbench;

/// <summary>A terminal's screen: the emulator, the parser in front of it, and the size it is drawn at.</summary>
/// <remarks>
/// <para>
/// This is the whole of the emulation, and it is the only type of the panel that is not a control: a screen is fed
/// bytes and answers with the rows to paint, which is what lets the interesting half - what a program's output turns
/// into - be asserted without a window.
/// </para>
/// <para>
/// The parser is stateful across writes, because an escape sequence may arrive in two reads: a program that writes
/// <c>\u001b[3</c> and then <c>1m</c> is writing one sequence. Handing each read to a fresh parser would paint the
/// halves as text.
/// </para>
/// </remarks>
internal sealed class TerminalSession
{
    private readonly VirtualTerminalController _terminal = new();

    // Created after the first resize, because the emulator reports the size it was given rather than the other way
    // round: a parser made before it would answer for a screen nobody draws.
    private readonly DataConsumer _consumer;

    /// <summary>A blank screen of the given size.</summary>
    /// <param name="columns">How many character cells fit across.</param>
    /// <param name="rows">How many rows are on screen.</param>
    internal TerminalSession(int columns = 80, int rows = 24)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);

        _terminal.ResizeView(Columns, Rows);

        _consumer = new DataConsumer(_terminal);
    }

    /// <summary>Raised after bytes were consumed, so a view knows to repaint.</summary>
    internal event EventHandler? Changed;

    /// <summary>How many character cells fit across.</summary>
    internal int Columns { get; private set; }

    /// <summary>How many rows are on screen.</summary>
    internal int Rows { get; private set; }

    /// <summary>Whether the cursor is meant to be shown at all.</summary>
    internal bool ShowCursor => _terminal.CursorState.ShowCursor;

    /// <summary>Where the cursor stands, as a column and a row of the screen.</summary>
    internal (int Column, int Row) Cursor =>
        (_terminal.CursorState.CurrentColumn, _terminal.CursorState.CurrentRow);

    /// <summary>The first row of the scrollback that is on screen.</summary>
    /// <remarks>
    /// A terminal keeps the lines that scrolled out above the visible page, and this is how a panel looks at them:
    /// moving it up is what the wheel does. Row zero of a painted page is this row of the history.
    /// </remarks>
    internal int TopRow
    {
        get => _terminal.ViewPort.TopRow;
        set
        {
            _terminal.ViewPort.TopRow = Math.Max(0, value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>What the screen says, one line per row, without trailing blanks.</summary>
    /// <remarks>
    /// The emulator's own answer rather than this type's, because it already knows that a wide character occupies two
    /// cells and that a cell holding a combining mark is not a character of its own.
    /// </remarks>
    internal string Text => _terminal.GetScreenText();

    /// <summary>Feeds what a program wrote to the screen.</summary>
    /// <param name="bytes">The bytes, in whatever split they arrived.</param>
    internal void Write(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        _consumer.Push(bytes.ToArray());

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Feeds text to the screen, which is what local echo and the panel's own notices write.</summary>
    /// <param name="text">The text to write.</param>
    internal void Write(string text) => Write(Encoding.UTF8.GetBytes(text));

    /// <summary>Changes how many cells fit, which the program is told about through the emulator's own report.</summary>
    /// <param name="columns">How many character cells fit across.</param>
    /// <param name="rows">How many rows are on screen.</param>
    /// <remarks>
    /// Following the view is the whole job: the emulator keeps what scrolled out above the visible page, and
    /// <see cref="TopRow"/> is how it is reached. A screen shorter than what a program prints therefore scrolls, exactly
    /// as a terminal does - which is worth knowing when a picture shows fewer lines than the program wrote.
    /// </remarks>
    internal void Resize(int columns, int rows)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        if (columns == Columns && rows == Rows)
        {
            return;
        }

        Columns = columns;
        Rows = rows;

        _terminal.ResizeView(Columns, Rows);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The rows to paint, top to bottom, each one a run of spans that tiles the whole width.</summary>
    /// <remarks>
    /// A span carries no column of its own - the runs tile the row from the left edge - so a painter walks them and
    /// adds up the widths it has drawn. That is also why the emulator is asked for the row count it was given rather
    /// than for "everything": a span past the last column belongs to a line that was wrapped.
    /// </remarks>
    internal IReadOnlyList<LayoutRow> Page() => _terminal.GetPageSpans(TopRow, Rows, Columns, null!);

    /// <summary>The bytes a named key sends, or an empty array for a key this terminal has no sequence for.</summary>
    /// <param name="name">The key's name as the emulator spells it, for instance <c>Up</c> or <c>Enter</c>.</param>
    /// <param name="shift">Whether shift is held.</param>
    /// <param name="control">Whether control is held.</param>
    internal byte[] Key(string name, bool shift, bool control) =>
        _terminal.GetKeySequence(name, shift, control) ?? [];
}
