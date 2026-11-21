# The terminal tool: a program of your choosing, watched and answered

The workbench can run a document (F5), run a language's tests and generate text — all of them *its own* commands
with their answers in a list. The terminal is the other half of trying something real: an arbitrary program, its
output painted as a terminal paints it, and the keyboard wired back into its input.

It is a dockable tool (`View ▸ Panels ▸ Terminal`), not a document, because it is not about a model: switching
documents leaves it running, and only the user puts it away.

## What it is made of

| Type (`src/Projectional.Workbench/Terminal/`) | What it owns |
| --- | --- |
| `TerminalSession` | the emulator (`VirtualTerminalController`), the parser in front of it (`DataConsumer`), and the size the screen is drawn at |
| `TerminalProcess` | one child process with its three streams redirected; bytes in, bytes out, and the exit code |
| `TerminalView` | painting: a grid of character cells, the cursor, the wheel over the scrollback, keys turned into bytes |
| `TerminalPanelView` | the command box, Start/Stop, the wiring, and **local echo** |
| `TerminalEcho` | which bytes of what was sent are shown on the screen |

The split is what makes the interesting half testable: what a program's bytes *become* is `TerminalSession`'s
answer, asserted headlessly (`TerminalSessionTests`), while the control only paints it.

## The three decisions worth knowing

**A span has no column.** The emulator answers a page as rows of spans — runs of text with one attribute
combination — that tile the row from its left edge. So the painter walks them and adds up the widths it has drawn,
and a row's spans always add up to the column count (a test asserts that, because a painter that trusted its own
arithmetic instead would draw a short line's tail over the cursor).

**The parser is stateful across writes.** A program's write is not a unit: a pipe splits where it likes, so
`\u001b[3` and `1m` may arrive separately and are still one sequence. A parser per read would paint the first
half as text — which is why the session owns the `DataConsumer` rather than creating one per write.

**Everything a child process tells the UI arrives on another thread.** The platform raises output reads and the
exit event on threads of its own, so both handlers marshal to the dispatcher before touching the session or a
button. This is not defensive coding: the first version set `IsEnabled` straight from the exit callback and took
the application down when a short-lived program finished.

## What it deliberately does not do

There is **no pseudo-terminal**. The three streams are ordinary pipes, so:

- the child does not echo — the panel writes the visible part of what it sent instead (`TerminalEcho`), and
  escape sequences and control characters are deliberately not shown;
- there is no line editing, no history and no Tab completion *by the terminal*: those are the program's job, and
  the ones that implement them need a tty to know they should;
- a program that colours its output only when it detects a terminal will not colour it here;
- `Console.ReadKey` and friends cannot work, because there is no console.

Each of those is a pty's job. The deletion condition is explicit: **the day a pty arrives, `TerminalEcho` is
deleted rather than switched off.**

The child is started with its working directory at the shell's own, and `Stop` kills the whole process tree —
a program that spawns a build server and is stopped must not leave the build server behind.

**A short screen scrolls, and the picture shows only the visible page.** The emulator keeps what went past the top
in its scrollback and `TerminalSession.TopRow` is how it is reached — a picture of the tool that shows three lines
where the program printed four is telling the truth about a panel three rows tall, which is what the first attempt
at that picture did. The wheel moves the view over the scrollback.

## What it is for next

The reason it exists before it has a client: the shell's own commands can move into it when their answer stops
being a list. A language's tests, a generator run or a long import are all "start something, watch it, answer it",
and the panel already has the parts. Nothing in the tool knows what a document is, so that move is a caller, not a
rewrite.

## Open questions

- **A pty per platform** (ConPTY on Windows, `forkpty`/`posix_openpt` elsewhere) — the one gap that a user will
  notice first, because a shell in this panel is not interactive in the way a shell is. Decide before the tool is
  offered as "a terminal" in the README's own words rather than as "a program's output".
- **A scrollback limit.** The emulator keeps history by row count; a program that writes a megabyte a second will
  grow it until something stops it. Nothing does today.
- **Selection and copy.** The panel has none: Ctrl+C is the program's break, and the shell's clipboard story for a
  terminal (select without Ctrl) is a decision of its own.
