# Markers

*Planned, not implemented.* This document is the design for the wavy underline under a cell
that is wrong: the error and warning squiggles every code editor has, with an error in red
and a warning in orange, drawn under exactly the text the diagnostic is about. It is written
before the code because the feature is the first one that has to answer a question the
projection layer has never had to answer — *what does the editor know that the model does
not* — and because the answer decides where validation, geometry and colour may each live.

The vocabulary is deliberately split in two:

- a **diagnostic** is a claim about the model: "this rule is missing a condition", "this
  reference does not resolve", "this value is outside the domain". It is semantics, it has a
  severity, and it has nothing to do with drawing.
- a **marker** is that claim placed on a rectangle: this diagnostic, on this cell, over these
  characters, at this height. It is geometry, and it knows nothing about what is wrong.

The squiggle is painted from the second by the control, in a colour the palette supplies.
Three layers, three questions, none of which has to know the others' business.

## Why not emphasis

The projection already has a channel for "draw this cell differently": `TextEmphasis`
(bold, italic, underline, strikethrough), decided by the notation and by an `ICellEmphasis`
resolver while the tree is built, carried on the cell, measured into the layout. A squiggle
looks like an underline, so the obvious move is a fifth flag. It is the wrong move, for
reasons that are about lifetime, not taste:

| | `TextEmphasis` | Marker |
| --- | --- | --- |
| Decided by | the language's notation, or a host resolver | the validator, after the fact |
| Lives in | the editor tree and the layout | the marker set, resolved against the layout |
| Persisted | no, but it is *derived from the model*, so it is stable while the model is | no, and it must not be — it is recomputed |
| Changes geometry | yes, bold and italic are wider, so it must be known before measuring | no, it is painted below the baseline |
| Lifetime | as long as the model says so | until the next validation pass |

The one that matters: emphasis has to exist *before* measurement, markers must be able to
arrive *after* it. Validation is incremental and asynchronous — it runs on a background
thread over a snapshot of the model, and it answers a keystroke or two later. Put a squiggle
in the tree and every arriving diagnostic invalidates the tree, the layout, the caret's
place and the frame. Keep it out of the tree and a diagnostic that arrives late changes one
list and repaints. The split is the same one the caret already makes: structure in the
layout, geometry computed on top of it.

A second reason is honesty about state. Emphasis is "this is how the language reads this
node" and belongs to projection. A squiggle is "as far as the last validation pass could
tell, this is wrong" — a statement with a timestamp. Two statements, two channels.

## The diagnostic

Semantics produces it, and it is the only part of the feature a language author writes. The
record is the one sketched in [semantics.md](semantics.md#diagnostics) — restated here with
the fields the display reads:

```csharp
public enum DiagnosticSeverity { Hint, Info, Warning, Error }

public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    NodeId Target,
    string? Role = null,              // the slot or property complained about; null = the node
    int? Index = null,                // which element in that slot
    string Message,
    string? Code = null,              // stable identity, e.g. "RULE_002"
    CharacterRange? Range = null,     // part of one property's text; null = the whole target
    IReadOnlyList<DiagnosticFix>? Fixes = null);
```

- **`Target`**, never a cell address. A cell address is a projection artefact: change the
  notation variant and the addresses under a node change, but the node does not. Diagnostics
  are resolved to cells by the editor, every time the layout changes — the same rule the
  caret follows.
- **`Range`** is optional and is a slice of one property's *model* text, not of the screen. A
  constraint that knows only the node leaves it out and the whole node is marked; a
  constraint that knows the offending characters can underline three of forty. Because it is
  in model characters, a wrapped line cannot confuse it.
- **`Code`** is for tests and documentation, and is shown next to the message. A message is
  prose that gets reworded; a code is the stable handle.
- **`Fixes`** are the quick fixes: the same `Intention` shape the light bulb uses, listed in
  [context-actions.md](context-actions.md). Empty until fixes exist.
- Nothing in a diagnostic mentions colour, thickness, or a wave. A severity is a *meaning*;
  the palette decides what it looks like.

Severity order is `Hint < Info < Warning < Error`, and it is used for two things: which wave
is drawn when several markers share a range, and the order of the tooltip's lines.

## The marker set

The editing layer holds the markers it was given and resolves them during layout, exactly as
it resolves the caret:

```csharp
public sealed class MarkerSet
{
    public MarkerSet(IEnumerable<Diagnostic> diagnostics);   // replaces, never appends
    public MarkerSet Concat(MarkerSet other);                // e.g. language + host diagnostics
    public MarkerSet For(NodeId node);                        // what marks one node
    public DiagnosticSeverity? SeverityOf(NodeId node);
    public int Count { get; }
}
```

`EditorSession` gains one property:

```csharp
public MarkerSet Markers { get; set; }   // null/empty = nothing marked
```

Setting it does **not** rebuild the tree and does **not** touch the document. It drops the
cached frame, so the next paint recomputes geometry — that is the whole invalidation story,
and it is why a late diagnostic is cheap.

Rules that make it behave:

- **Replacing, not appending.** A validation pass reports the whole truth for the units it
  covered, so `SetMarkers` swaps the set. An incremental validator that only knows part of
  the document merges with `Concat` and keeps the rest — the difference is the caller's, and
  it has to be explicit rather than implied by an `Add`.
- **Stale by construction.** A marker whose node is gone resolves to nothing and is dropped
  while the frame is built. Undo brings the node back and the marker resolves again if the
  set still holds it — which is correct for a set that was never edited, and the reason
  markers cannot corrupt history.
- **Not persisted, ever.** A model file contains the model. Diagnostics are recomputed on
  open. This also means a squiggle cannot resurrect itself from a file written by an older
  version of a constraint.
- **Thread-safe to set.** Validation runs off the UI thread, so `SetMarkers` takes a
  snapshot into an immutable set and the control posts a repaint. It never mutates a set the
  paint is iterating.

## Resolving a marker to a rectangle

This is the slice with the real work in it, because a cell is no longer one line: since
multi-line text a cell is a stack of `TextLine`s with hard breaks and soft wraps. A squiggle
has to follow the text it belongs to.

```csharp
public readonly record struct MarkerLine(
    CellAddress Cell,
    Diagnostic Diagnostic,
    double X,
    double Y,
    double Width,
    double Height,
    int Line);       // which line of the cell, for a marker that spans several
```

```csharp
internal static class MarkerGeometry
{
    internal static IReadOnlyList<MarkerLine> Of(CellLayout layout, MarkerSet markers, ITextMeasure measure);
    internal static IReadOnlyList<MarkerLine> Of(EditorFrame frame, MarkerSet markers);
}
```

The rules, in the order they matter:

1. **Resolve the node to its cells.** A node's own cells are the cells the notation made for
   it, not the cells of its children. A node that has no text of its own — a container — is
   marked on its first cell's line, spanning its whole width, so that a broken table row is
   visibly broken at the row and not somewhere inside it.
2. **A range inside a property splits per line.** `start`/`length` in model characters map
   onto the lines the cell was laid out into: the first line's part, the middle lines in
   full, the last line's part. One `MarkerLine` each. This is exactly what a wrapped word or
   a hard `\n` requires, and it is why the geometry exists at all instead of a text
   decoration.
3. **A range that is out of bounds is clipped, not dropped.** A stale range past the end of
   the text (the user deleted characters and validation has not caught up) marks the last
   character instead of vanishing; a range that is entirely past the end marks the cell.
4. **Empty ranges mark one character**, or the whole cell when the cell is empty — a squiggle
   of zero width would be invisible, and "there is an error here" has to be visible.
5. **Only visible lines produce geometry.** `EditorFrame.Build` already clips to the viewport,
   so `MarkerGeometry.Of(frame, …)` inherits that: a document with ten thousand diagnostics
   paints the thirty lines that are on screen. Filtering by row before building the wave path
   is not an optimisation to add later; it is the default.
6. **The wave lives under the baseline**, inside the line's box, so it changes no bounds and
   no hit test. Its thickness is a renderer constant, not a measure constant: a squiggle must
   not make a line taller.

`EditorFrame` therefore gains one member and `Build` one optional parameter:

```csharp
public IReadOnlyList<MarkerLine> Markers { get; }
```

`EditorFrame.Build(layout, viewport, offsetX, offsetY, selection, measure, styler, markers)`.
The frame is the right home because it is already the layer that says "these are the
rectangles for what you can see", and because a second renderer that consumes `EditorFrame`
gets squiggles for free — the same argument that put `TextRun` and `CaretRect` there.

## Painting

The control draws the wave itself. Avalonia's text decorations cannot: `TextDecoration`
offers underline and strikethrough with a dash pattern, and no wave; and a decoration applies
to a whole run, so a range shorter than the cell would be impossible. What the toolkit does
give is a `DrawingContext`, a pen and a geometry, which is all a wave needs.

- **The shape**: a polyline down-and-up between `y + h - t/2` and `y + h - t/2 - amplitude`,
  one segment per `period`. Constants in the renderer (`MarkerWave.Period`, `.Amplitude`,
  `.Thickness`), assigned so that a row of monospace text gets a recognisable wave without
  touching the row below.
- **The pattern is a function of severity**, not the colour alone: `Error` and `Warning` are
  solid waves, `Info` and `Hint` are dotted (dash style on the same path). A colour-blind
  user, a greyscale print and a high-contrast theme all still distinguish "wrong" from
  "noteworthy". This is the accessibility answer and it costs no extra code.
- **The colour comes from the palette**, never from the model:
  `MarkerPalette.Error/Warning/Info/Hint`, carried on `SyntaxTheme` next to the text and
  caret colours. Defaults: error `#E51400` (light) / `#F14C4C` (dark), warning `#BF8803` /
  `#CCA700`, info `#1A85FF` / `#75BEFF`, hint `#808080` / `#A0A0A0`.
- **Painted after the text**, in the same pass, so a squiggle is never covered by a glyph;
  and **before the caret**, so a caret that stands on a marked character is still visible.
- **Overlapping markers on the same range**: the highest severity is drawn, and the others are
  reachable through the tooltip. Two waves at one baseline is a smear, not more information.

## Interaction

| Input | Effect |
| --- | --- |
| hover a squiggle | a tooltip with the message, the code and "N quick fixes" — only inside the wave's band, so hovering the text does not open it |
| caret on a marked line | the shell's status bar shows the highest-severity message for that line |
| `F8` / `Shift+F8` | next / previous marker in the document; the shell binds it, the editing layer supplies the order (document order, then severity) |
| `Alt+Enter` on a marked line | the light bulb, with the fixes listed first ([context-actions.md](context-actions.md)) |
| click a squiggle | places the caret inside the marked range — no popup, no navigation |

The tooltip is drawn by the control (a `Popup`, like the context menu) and is derived from
the marker under the pointer on each open: nothing is cached, so an edit that fixes the error
cannot leave a stale tooltip behind.

Ordering for `F8` is document order of the markers' first line, then severity descending at
one position. A document-order walk is what a user can predict; sorting by severity first
teleports around the file.

## Where markers come from, and when

M4. Until constraints exist there is nothing to report, so the feature arrives in slices that
are useful on their own — the model and the geometry are testable with a fixture validator,
long before a real one exists:

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/marker-model` | `DiagnosticSeverity`, `Diagnostic`, `CharacterRange`, `MarkerSet`, `EditorSession.Markers`, resolution of node ids to cells, tests for stale nodes, undo, and replacement versus `Concat` |
| B | `feat/marker-geometry` | `MarkerLine`, `MarkerGeometry`, per-line splitting, clipping, the visible-lines rule, `EditorFrame.Markers`, tests over wrapped and hard-broken cells |
| C | `feat/marker-paint` | `MarkerPalette` on `SyntaxTheme`, the wave in the control's `Render`, light and dark defaults, pattern-per-severity |
| D | `feat/marker-hover` | the tooltip, the click-to-caret hit test, and `F8` ordering in the editing layer |
| E | (M4) `feat/validation-markers` | the constraint runner publishes diagnostics through `SetMarkers`, incrementally, off the UI thread |
| F | (M4) `feat/quick-fixes` | a diagnostic's fixes reach the light bulb, which is a change in [context-actions.md](context-actions.md) rather than here |

The status-bar line and the `F8` binding are shell work and land with the standalone editor's
own slices; the library supplies the order and the message, not a widget.

## Performance

The one thing this feature can do wrong is make typing slower, so the rules are stated as
budgets rather than as wishes:

- **No work while typing.** A keystroke rebuilds tree and layout; markers are not resolved
  during that rebuild. Geometry is built when a frame is built, and a frame is built when
  something is painted.
- **Cost is per visible line.** Because `MarkerGeometry.Of(frame, …)` sees only the viewport,
  the cost is bounded by the window, not by the document.
- **One wave path per marker line, built from a shared pattern.** A thousand repeats of a
  four-point wave is one `StreamGeometry` with a repeated figure, not a thousand geometries.
- **No re-validation on caret movement.** Moving the caret changes the bulb and the status
  line; it must not trigger a validation pass. That is the validator's incremental contract,
  and stating it here keeps the marker layer from adding its own trigger.
- **A repaint, not a rebuild,** when `Markers` changes: the cached frame is dropped, the
  editor tree is not.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Persistent annotations ("TODO", "NOTE") | different lifetime, different storage; that is a model feature, not a diagnostic |
| A problems list / error pane | a document-wide report with its own navigation and filtering; a feature of its own, after squiggles prove they are enough |
| Inline error text after the marked cell | moves the layout, wraps unpredictably, and duplicates what the tooltip and the bulb already say |
| Colouring the whole cell background | a background colour is a second signal competing with the squiggle, and it hides the selection highlight |
| Auto-fixing on save | a fix changes the model; it must be a user action with one undo step |
| Remote or CI diagnostics | the same shape, a different transport; the protocol question belongs with the LSP question, not here |
| Suppressing or disabling a warning | needs a place to record the suppression, which is a model decision |
| A minimap or a scrollbar rail with severity colours | a shell rendering of the same set, once the set exists and is cheap to query |

## Decisions this plan proposes

To be frozen as an ADR with slice A, not before:

1. A marker is **transient editor state**, supplied by the host, keyed by node id, never
   persisted and never part of the editor tree.
2. Diagnostics are **resolved to geometry at frame-build time**, per line, and only for the
   visible area; the projection carries no diagnostics.
3. Severity is conveyed by **colour and pattern together**, and the colour comes from a
   palette in the theme, never from the model or the language.
4. A marker is **attached to a node, optionally narrowed to a range of one property's text**,
   because a constraint usually knows the node and sometimes knows the characters.
5. When markers overlap, the **highest severity is drawn** and all of them are discoverable
   through the tooltip and the light bulb.

## Testing

- **Model, headless.** `MarkerSet` replacement versus `Concat`; a marker on a node that was
  deleted; undo restoring the node; a diagnostic with no range marking the node's own cells;
  the same severity and message for two nodes.
- **Geometry, headless.** A range inside a wrapped line (one segment per line, correct
  widths); a range spanning a hard `\n`; a range that starts before and ends after a break;
  a range past the end of the text; an empty cell; a container node with no text; a marker
  whose line is scrolled out of the viewport (no geometry); two markers on one range (one
  drawn).
- **Palette, headless.** Every severity has a brush in both themes, and the wave constants
  keep the wave inside a `LineHeight` row.
- **Control, when `tests/Projectional.Avalonia.Tests` exists.** A rendered wave for one
  error; the tooltip opening inside the band and not over the text; `F8` moving the caret to
  the next marker.
- **Rendering truth.** `LayoutSnapshot` is deliberately unchanged: it draws the layout, and a
  marker is not layout. Geometry tests assert rectangles, which is what a broken squiggle
  actually is.

## Open questions

Rows 35–38 in [open-questions.md](../open-questions.md) are the parts of this that are
choices rather than mechanics, and this document states its recommendation for each.
