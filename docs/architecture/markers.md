# Markers

Status: **delivered** for M1, slices A–D (the tooltip included; click-to-caret and `F8` are
still open). Slices E–F are planned, M4. This document is the design for the wavy underline
under a cell that is wrong: the error and warning squiggles every code editor has, with an
error in red and a warning in orange, drawn under exactly the text the diagnostic is about. It
is written before the code because the feature is the first one that has to answer a question
the projection layer has never had to answer — *what does the editor know that the model does
not* — and because the answer decides where validation, geometry and colour may each live.

Slice A — the vocabulary, the set, and the resolution of a diagnostic to cells — is delivered
in `Projectional.Semantics/Diagnostics/` and `Projectional.Editing/Markers/`, and its decisions
are frozen in [ADR 0013](../adr/0013-markers-are-view-state.md). Slice B — turning a diagnostic
into the rectangles a page can draw — is `MarkerLine` and `MarkerGeometry` in the same folder.
Slice C paints them; slice D adds the tooltip a reader needs to know *what* is wrong. Two
things in this document were changed by writing the code and are marked *superseded* below:
the frame does not carry markers, and a marker set is resolved against the layout at paint time
rather than during a frame build. Three more were changed by slice D and are stated there: the
tooltip is drawn by the control rather than by a `Popup`, it carries its own palette, and the
wrapping lives in the editing layer.

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

public sealed record Diagnostic
{
    public Diagnostic(
        DiagnosticSeverity severity,
        NodeId target,
        string message,
        string? role = null,             // the slot complained about; null = the node
        int? index = null,               // which child of a many-valued role
        string? code = null,             // stable identity, e.g. "RULE_002"
        CharacterRange? range = null);   // part of one value's text; null = all of it

    public DiagnosticSeverity Severity { get; }
    public NodeId Target { get; }
    public string Message { get; }
    public string? Role { get; }
    public int? Index { get; }
    public string? Code { get; }
    public CharacterRange? Range { get; }
}
```

`message` is required and second in the argument list, because a diagnostic with no message is
not a diagnostic; `severity`, `target` and `message` are the only positional ones a caller has
to think about.

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
- **There is no `Fixes` field.** Quick fixes are a list of intentions that change the model,
  and putting them on the diagnostic would make the vocabulary of "what is wrong" also the
  vocabulary of "what to do about it" — the check that found the problem is not the thing that
  knows how to repair it. The fix list is a separate host-supplied set beside the marker set,
  keyed by the diagnostic record ([quick-fixes.md](quick-fixes.md)).
- Nothing in a diagnostic mentions colour, thickness, or a wave. A severity is a *meaning*;
  the palette decides what it looks like.

Severity order is `Hint < Info < Warning < Error`, and it is used for two things: which wave
is drawn when several markers share a range, and the order of the tooltip's lines.

## The marker set

The editing layer holds the markers it was given and resolves them while it paints, exactly as
it resolves the caret:

```csharp
public sealed class MarkerSet
{
    public MarkerSet(params ReadOnlySpan<Diagnostic> diagnostics);  // replaces, never appends
    public static MarkerSet Empty { get; }
    public int Count { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }           // build order
    public IReadOnlyList<Diagnostic> For(NodeId node);              // empty, never null
    public DiagnosticSeverity? SeverityOf(NodeId node);             // worst wins
    public MarkerSet Concat(MarkerSet other);                       // e.g. language + host
}
```

`EditorSession` gains one property:

```csharp
public MarkerSet Markers { get; set; }   // never null; empty = nothing marked
```

Setting it does **not** rebuild the tree and does **not** touch the document. It is decoration,
so the control repaints and nothing else happens — no frame is cached anywhere to invalidate,
which is why a late diagnostic is cheap.

Rules that make it behave:

- **Replacing, not appending.** A validation pass reports the whole truth for the units it
  covered, so assigning `Markers` swaps the set. An incremental validator that only knows part
  of the document merges with `Concat` and keeps the rest — the difference is the caller's, and
  it has to be explicit rather than implied by an `Add`.
- **Stale by construction.** A marker whose node is gone resolves to nothing and is dropped
  while painting. Undo brings the node back and the marker resolves again if the set still
  holds it — which is correct for a set that was never edited, and the reason markers cannot
  corrupt history.
- **Not persisted, ever.** A model file contains the model. Diagnostics are recomputed on
  open. This also means a squiggle cannot resurrect itself from a file written by an older
  version of a constraint.
- **Thread-safe to set.** Validation runs off the UI thread, so a background pass builds a
  whole `MarkerSet` and assigns it; the control then posts a repaint. Nothing ever mutates a set
  the paint is iterating, because nothing can mutate a set at all.

## Resolving a diagnostic to cells

This is the part slice A delivers, and it is the only place that knows the mapping. It lives in
`Projectional.Editing/Markers/MarkerCells.cs` and is `internal`: a host supplies diagnostics and
the editor decides what they cover.

```csharp
internal static class MarkerCells
{
    internal static IReadOnlyList<EditorCell> Of(CellLayout layout, Diagnostic diagnostic);
}
```

| Diagnostic | Cells |
| --- | --- |
| `Role` null | the node's own visible cells, and nothing below them |
| `Role` set, `Range` null | that slot's cell and the visible text below it |
| `Role` set, `Range` set | that slot's cell alone |
| `Role` and `Index` set | that one element's cell and the visible text below it |
| `Role` that resolves to nothing | falls back to the node rule |
| a node the layout does not contain | nothing |

Two of those are rules rather than cases, and they are the ones a future change is most likely
to break:

1. **A finding about a node stops at its own slots.** A node's own cells are the cells its
   notation made for it — keywords, values, punctuation and the placeholders of its empty slots —
   not the cells of its children. Marking a parent's children from the parent would make one
   finding look like a finding on everything below it, and would double-cover a line whenever a
   child has a finding of its own.
2. **A finding is never invisible.** A concept whose notation is only a child slot draws no text
   of its own once the child is filled, so the node rule falls back to the node's outermost cell:
   a container still gets a line's worth of marker instead of nothing. A `Range` is ignored when
   `Role` is null, because there is no single value for the offsets to count from.

"Visible text below" is a depth-first walk that keeps only cells with text, so a marker on a
child slot covers what the child draws and never an invisible container's rectangle. A node that
is not in the layout marks nothing: the set may have been computed before the edit that removed
the node, and dropping the marker is the only honest answer — inventing a cell would put the mark
somewhere it does not belong.

## Geometry, per line

*Slice B.* A cell is no longer one line: since multi-line text a cell is a stack of `TextLine`s
with hard breaks and soft wraps. A squiggle has to follow the text it belongs to.

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
}
```

`MarkerLine` is public while its producer is internal, which follows the same split as
`EditorCell` and `MarkerCells`: the rectangle is a value a frontend is handed, and resolving is
the editor's business. `MarkerGeometry` became public with slice C, when the control - a
separate assembly - started calling it, exactly as `EditorFrame` is public for the same reason.

The rules, in the order they matter:

1. **Resolve the diagnostic to cells first** (above), then each of those cells to its lines.
2. **A range inside a value splits per line.** `start`/`length` in model characters map onto the
   lines the cell was laid out into: the first line's part, the middle lines in full, the last
   line's part. One `MarkerLine` each. This is exactly what a wrapped word or a hard `\n`
   requires, and it is why the geometry exists at all instead of a text decoration.
3. **A range that is out of bounds is clipped, not dropped.** A stale range that now overhangs
   the end of the text (the user deleted characters and validation has not caught up) marks what
   is left instead of vanishing; a range that starts entirely past the end marks the whole cell.
4. **Empty ranges mark one character.** A squiggle of zero width would be invisible, and "there
   is an error here" has to be visible; a cell that draws no characters of its own - a concept
   whose notation is only a child slot - gets a marker one character wide at its start, which is
   inside the box layout reserved for it.
5. **Every line produces geometry, and the renderer filters.** The result covers the whole
   document: a page that scrolled has to draw a marker it did not draw a moment ago, and
   re-resolving because the viewport moved would give the editing layer a reason to know about
   scrolling. A control that wants only the rows on screen compares `MarkerLine.Y` against its
   viewport - a filter on a list it already holds - so a document with ten thousand diagnostics
   still paints the thirty lines on screen. Filtering by row is not an optimisation to add
   later; it is the default.
6. **The wave lives under the baseline**, inside the line's box, so it changes no bounds and no
   hit test. Its thickness is a renderer constant, not a measure constant: a squiggle must not
   make a line taller.
7. **The list comes back in reading order** - by `Y`, then `X` - because both of its consumers
   need an order the user can predict: the painter draws top to bottom, and `F8` walks the same
   list. Geometry sorts once; nobody downstream has to.

### Superseded: the frame does not carry markers

An earlier draft of this document gave `EditorFrame` a `Markers` property and `Build` an eighth
parameter. That is impossible without breaking the dependency arrows: `Projection` cannot
reference `Editing` or `Semantics` ([ADR 0010](../adr/0010-public-api-surface.md)), so a frame
carrying `MarkerLine` would drag both up. `MarkerGeometry.Of` therefore takes the layout and the
measure, and the control calls it when it paints. The cost is one recomputation per paint; the
gain is that assigning a marker set needs no rebuild of the tree, the layout or the caret.

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
- **The dash is two thicknesses long, not one.** A one-unit dash on a one-unit-thick stroke
  falls between pixel centres and antialiases to roughly half the brush's opacity, which makes
  the two *mild* severities look fainter than the serious ones - the opposite of what the
  pattern is for. Measured on a rendered frame, a one-unit dash reached 68 % (hint) and 50 %
  (info) coverage of the brush colour; two units reach 94 % and 77 %. The dash length is
  therefore a rendering constant with a reason, not a taste.
- **The context is clipped to the text area for the whole pass**, and a run entirely outside the
  viewport is skipped before its geometry is built. So scrolling costs nothing extra and a
  squiggle can never spill into the gutter or past the edge; a run that is partly visible still
  builds its full path and is trimmed by the clip.
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
| hover a squiggle | a tooltip with the message and the code — only inside the wave's band, so hovering the text does not open it |
| caret on a marked line | the shell's status bar shows the highest-severity message for that line |
| `F8` / `Shift+F8` | next / previous marker in the document; the shell binds it, the editing layer supplies the order (document order, then severity) — *planned* |
| `Alt+Enter` on a marked line | the light bulb, with the fixes listed first ([context-actions.md](context-actions.md)) |
| click a squiggle | places the caret inside the marked range — no popup, no navigation — *planned* |

The tooltip is **drawn by the control**, like the completion list and unlike an Avalonia
`Popup`. Three reasons, in order of weight: a `Popup` is a separate top-level surface, so it
cannot be rasterised in a headless check and this feature would have had no rendered truth at
all; a popup can be placed partly outside the host's box, which is not ours to occupy when the
editor is embedded in someone else's window; and the control already owns the scroll offset and
the round from layout to pixels that the box has to agree with. The cost is that the tip is
clipped by the editor's own bounds — a host that gives the control no room around the document
sees a truncated tip rather than one floating over its neighbours. That is the right side of
the trade for an embedded control.

It is derived from the marker under the pointer on each pass and cached only by the anchor it is
attached to, so an edit that fixes the error cannot leave a stale tooltip behind: the set is
replaced, the anchor is gone with it, and the tip closes.

Ordering for `F8` is document order of the markers' first line, then severity descending at
one position. A document-order walk is what a user can predict; sorting by severity first
teleports around the file.

### One tip at a time

A second tooltip is planned: hovering a node shows its concept's description
([concept-descriptions.md](concept-descriptions.md)). It reuses `MarkerTip.Place`, so both boxes
flip and clamp the same way, and it never competes for the pointer - **the marker wins**. A wave is
a specific finding at a specific place and the description is ambient, so a pointer over a marker
band shows the finding and suppresses the description. The description's hover also carries a delay
and this one does not, for the same reason: a wave is a shape a user aims at, ordinary text is what
the pointer crosses on its way somewhere else.

## Where markers come from, and when

M4. Until constraints exist there is nothing to report, so the feature arrives in slices that
are useful on their own — the model and the geometry are testable with a fixture validator,
long before a real one exists:

| Slice | Branch | Delivers |
| --- | --- | --- |
| A ✓ | `feat/marker-model` | `DiagnosticSeverity`, `Diagnostic`, `CharacterRange`, `MarkerSet`, `EditorSession.Markers`, `MarkerCells` (node ids to cells), tests for stale nodes, replacement versus `Concat`, the node-versus-container rule |
| B ✓ | `feat/marker-geometry` | `MarkerLine`, `MarkerGeometry`, per-line splitting, clipping, reading order, tests over wrapped and hard-broken cells |
| C ✓ | `feat/marker-paint` | `MarkerPalette` on `SyntaxTheme`, `MarkerRuns` (overlap merge), the wave in the control's `Render`, light and dark defaults, pattern-per-severity |
| D ✓ | `feat/marker-hover` | `MarkerHit` and `MarkerHitTest` (which finding is under the pointer), `MarkerTip` (its text and where its box goes), `MarkerTipPalette`, `MarkerTipPresenter` on the control, and dropping the tip when the set is replaced |
| E | (M4) `feat/validation-markers` | the constraint runner publishes diagnostics through `Markers`, incrementally, off the UI thread |

Slice F of the original plan is gone: fixes have their own design and their own slices in
[quick-fixes.md](quick-fixes.md), and they land **before** the constraint runner because
diagnostics already exist and nothing yet makes them actionable. Nothing about the marker model
changes for them.

Still open from slice D: the click-to-caret gesture on a squiggle and `F8` ordering, which is a
list function in the editing layer and a binding in the shell.

The status-bar line and the `F8` binding are shell work and land with the standalone editor's
own slices; the library supplies the order and the message, not a widget.

**Until M4 there has to be something to see, and no check exists to produce it.** So the
standalone app ships one, and it is the reference proof that the public surface is enough:
`Projectional.Workbench/Checks/DefaultNames.cs` walks the model, reports a `Hint` on every node
whose name still equals the concept's declared default, and `MainWindow` publishes
`new MarkerSet([.. DefaultNames.Of(document.Model)])` into the editor's `Markers` at open and
again from `Model.Changed`. Three things fall out of it that are worth keeping:

- **A host can produce findings with what is already public** — `Concept.NameSlot`,
  `PropertySlot.DefaultValue`, `Node.ChildrenInDocumentOrder`, `MarkerSet`, `editor.Markers` —
  with no reference from the app to an internal project.
- **A host must republish on every change**, because a marker set is not re-resolved against a
  new model; a host that published once would leave the waves describing the document as it was
  before the edit. The set is immutable and a repaint is cheap (see [Performance](#performance)).
- **The check reads the placeholder from the language, not from a list of its own**, so a language
  that changes a default, or declares a name slot with no default (the user must fill it), needs
  no change here. `Expressions.Name` is the second case and is deliberately not reported.

## Performance

The one thing this feature can do wrong is make typing slower, so the rules are stated as
budgets rather than as wishes:

- **No work while typing.** A keystroke rebuilds tree and layout; markers are not resolved
  during that rebuild. Geometry is built when something is painted.
- **Cost is per visible line.** Because the control clips to the viewport before building the
  wave path, the cost is bounded by the window, not by the document.
- **One wave path per marker line, built from a shared pattern.** A thousand repeats of a
  four-point wave is one `StreamGeometry` with a repeated figure, not a thousand geometries.
- **No re-validation on caret movement.** Moving the caret changes the bulb and the status
  line; it must not trigger a validation pass. That is the validator's incremental contract,
  and stating it here keeps the marker layer from adding its own trigger.
- **A repaint, not a rebuild,** when `Markers` changes: the control invalidates its visual, the
  editor tree and the layout are untouched.

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

## Decisions frozen with slice A

[ADR 0013](../adr/0013-markers-are-view-state.md) records them. In short:

1. A marker is **transient editor state**, supplied by the host, keyed by node id, never
   persisted and never part of the editor tree.
2. The diagnostic vocabulary lives in `Projectional.Semantics`, because a check must be able to
   run on a model nobody is drawing; the set and the geometry live in `Projectional.Editing`.
3. Diagnostics are **resolved to cells, and then to per-line geometry, when the control paints**,
   never during a frame build — the frame may not reference `Editing` or `Semantics`.
4. Severity is conveyed by **colour and pattern together**, and the colour comes from a
   palette in the theme, never from the model or the language.
5. A marker is **attached to a node, optionally narrowed to a range of one value's text**,
   because a check usually knows the node and sometimes knows the characters.
6. When markers overlap, the **highest severity is drawn** and all of them are discoverable
   through the tooltip and the light bulb.

## Decisions frozen with slice B

Writing the geometry settled three things the plan had left open. They are in this document
rather than a new ADR because none of them is expensive to reverse, but they should not be
re-litigated by accident:

1. **Geometry reports every marker line, including duplicates on one rectangle.** Dropping the
   loser of an overlap here would make it unreachable: `MarkerLine` carries its `Diagnostic`, and
   that is what a tooltip and the light bulb read. Collapsing is therefore a painting decision,
   made where the palette is known, and slice C does it.
2. **The answer is document-wide, not viewport-filtered.** Filtering by scroll position would
   make the same call return different lists for the same model, and the layer that produced it
   would have to know what scrolling is.
3. **A stale range is clipped, never dropped.** A check that measured a longer value than the one
   now on screen marks what is left; a range entirely past the end marks the whole cell. A
   diagnostic that is still in the set has to be visible, or a fixable problem looks fixed.

## Decisions frozen with slice C

Painting settled four things. They are here rather than in a new ADR for the same reason as
slice B's - none is expensive to reverse, and none should be re-litigated by accident.

1. **The overlap merge is a separate, headless step, and it merges *strict* overlaps only.**
   `MarkerRuns.Of(markerLines)` sorts by `(Y, X)`, sweeps each line, and merges two rectangles
   only when they genuinely overlap - touching is not overlapping, so `0..10` and `10..20` stay
   two runs and the user sees two marks, not one long one. The worst severity wins, the tallest
   height wins, and the output is in reading order. It lives in `Projectional.Editing` because
   deciding which finding is drawn is not a toolkit's business, and it is testable there with
   fourteen cases instead of a screenshot.
2. **`MarkerGeometry` and `MarkerRuns` are public.** `Projectional.Avalonia` has no
   `InternalsVisibleTo` - and granting it one would be worse than widening the surface, because
   the control is the reference consumer of the editing layer. A host that draws its own
   markers (a second frontend, a print view, a test that asserts rectangles) needs exactly
   these two functions and nothing else, so they are public with a `<remarks>` saying why.
3. **The palette property is `MarkerColors`, not `MarkerPalette`.** A member named like a type
   in the same namespace shadows it, so `public MarkerPalette MarkerPalette { get; set; }` does
   not compile. The precedents are `Palette`/`SyntaxPalette` and `ListPalette`/
   `CompletionPalette`; the pattern is "the type keeps the domain word, the property says what
   it holds".
4. **The wave is drawn by the control, not by a text decoration and not by the theme.** A
   `TextDecoration` applies to a whole run and has no wave; a palette that could change the
   amplitude or thickness would let a host draw outside the line the layout reserved. So the
   geometry constants are `internal` to the control and only the colours are themeable.

Two smaller consequences worth knowing:

- **Severity is a pattern as well as a colour, and the dash length is a rendering constant with
  a measured reason** (see [Painting](#painting)). A colour-blind reader, a greyscale print and a
  high-contrast theme all still separate "wrong" from "noteworthy".
- **The run list is cached against `(layout, marker set, measure)` identity.** The caret blink
  repaints twice a second, and recomputing geometry on every blink would be the one way this
  feature could make typing feel slow. `MarkerSet` is immutable, so reference identity is a
  sound key.

## Decisions frozen with slice D

The tooltip settled five things. They are here rather than in an ADR for the same reason as
slice B's and C's: none is expensive to reverse, and none should be re-litigated by accident.

1. **The tip is drawn by the control, not by an Avalonia `Popup`.** Testable-ish headlessly,
   consistent with `CompletionPresenter` and the completion list, and never occupies space
   outside the box the host gave the editor. Accepted cost: the tip is **clipped by the
   control's bounds**. A host that wants the tip to float over its own chrome moves the control
   instead.
2. **The tip carries its own palette.** `MarkerTipPalette` is `Background`/`Border`/`Foreground`
   rather than a widened `MarkerPalette`, because a marker palette describes *severity colours*
   and the tip needs *chrome colours* - and the tip's background deliberately is not the page
   colour, or the box would have no edge over a page of the same shade.
3. **The text is broken in `Projectional.Editing`, not in the control.** `MarkerTip.Lines` wraps
   the message against a maximum width and answers lines; `MarkerTip.Place` puts the box under
   the anchor and clamps it into the viewport. Both are pure and testable, and the box has to be
   placed **after** its height is known, which is why wrapping cannot be a paint-time detail.
   `MarkerTip.Wrap` stays `internal`: what a host needs is the lines, not the algorithm.
4. **The box is placed from the anchor's layout rectangle, never from the pointer.** Anchoring on
   the pointer makes the tip slide as the hand moves and puts it wherever the wave was last
   touched; the marker line is the stable answer and it is what the message is about.
   `TipBounds` clamps against the control's whole `Bounds` (gutter included), not the text
   viewport, so a tip next to the leftmost column may use the gutter's width.
5. **The tip is dropped on every state change that could invalidate its anchor**: scroll, press,
   pointer exit, `Show()` (any edit or move), a session swap, and a **new `MarkerSet`**. The last
   one is the subtle case and it is why the `Markers` setter calls `ClearHover()`: the tip is
   anchored on a `MarkerLine` that belongs to the set which was current when the pointer
   stopped, so a fresh set makes the old anchor a rectangle from a document that no longer
   exists.

Two smaller consequences:

- **`MarkerHit` holds a list of diagnostics and the anchor line.** `MarkerHitTest.At` answers with
  a *fresh* list on every call, so record equality never matches two hits on the same marker;
  `IsSameHover` therefore compares the anchor and the count. That is what keeps the tip from
  being rebuilt on every pointer move while still reopening it for a second finding on the same
  text.
- **The tip does not promise a fix it cannot deliver.** The severity word, the message and the
  code, and nothing else - "N quick fixes" needed the fix list from
  [quick-fixes.md](quick-fixes.md) slice Q5, and a line that always reads "no fixes" is worse
  than no line. That slice landed, so the tip now adds one line for a finding with repairs
  (`2 quick fixes - Alt+Enter`) and none at all for a finding without them, read from the same set
  the bulb reads.

## Testing

- **Model, headless.** Done in `MarkerSetTests`: replacement versus `Concat`, duplicates kept,
  `For` answering empty for a healthy node, `SeverityOf` reporting the worst finding.
- **Resolution, headless.** Done in `MarkerCellsTests`: the node rule, a slot that names a
  property, a slot that names a child, an index naming one element, a range narrowing to one
  cell, a stale node marking nothing, an unknown slot falling back to the node, and a node with
  no text of its own falling back to its container.
- **Geometry, headless.** Done in `MarkerGeometryTests`: a range inside a wrapped value (one
  segment per line, with the continuation line starting at the cell's own left edge); a range
  spanning a hard `\n`; a range that starts on the first line and ends on the last, marking the
  middle lines whole; a range that overhangs the end, clipped to what is left; a range entirely
  past the end, marking the cell; an empty range marking one character; a value with no range
  marked on every line it occupies; a cell that draws no characters still getting a visible
  marker; two findings on one range both surviving, because which one is drawn is a painting
  decision and the other must stay reachable; and reading order across two markers.
- **Merge, headless.** Done in `MarkerRunsTests` (14 cases): one marker is one run; two findings
  on the same text are one run; overlapping markers merge; a contained marker does not widen the
  run; a chain of three collapses to one; the worse severity wins and a milder one does not
  downgrade a run; markers that merely *touch* stay two runs; separate stretches on one line stay
  separate; markers on different lines are different runs; a merge keeps the tallest line; runs
  come back in reading order; nothing marked is an empty answer; and a `null` list is rejected.
- **Palette and wave constants, by inspection.** `MarkerPalette` is four brushes with a
  `BrushOf` switch and `Light`/`Dark` presets; it has no arithmetic to get wrong, and the
  wave constants keep the wave inside a `LineHeight` row by construction
  (`amplitude = min(2, height - thickness)`).
- **Rendered truth, by probe.** A throwaway Avalonia window marked the payroll sample with all
  four severities and rasterised the frame. Checked: the four patterns are visually distinct;
  the sampled colour of each stroke is the palette's (error and warning land within a few units
  of the exact hex, the two dotted ones are antialiased blends of it); a `Warning` and a `Hint`
  on the same range collapse to one run and the warning is drawn; clearing the set removes every
  squiggle. This is the check that a geometry assertion cannot make - the rectangles were right
  the whole time the picture was wrong.
- **Hit test, headless.** Done in `MarkerHitTestTests` (14 cases): a point inside the wave's band
  finds the finding, a point above it on the same text finds nothing (the text is not the
  squiggle), a point below the line finds nothing, the band's own top and bottom edges are
  inside, a point in the gutter at that height finds nothing, the nearest marker horizontally
  wins, two findings on the same rectangle both come back with the worse one first, a vertical
  hit that overlaps two lines picks the nearer one, `null` and empty lists answer nothing, and
  the answer carries the anchor line so the tip can be placed without asking again.
- **Tip text and placement, headless.** Done in `MarkerTipTests` (17 cases): the heading is the
  severity word, the message and the code; a long message wraps instead of overflowing; a word
  longer than the line still advances; blank lines are kept in the wrap and skipped when drawn;
  the box sits below the anchor with the `Gap` between them; a box near the right edge moves left
  instead of leaving the viewport; a box near the bottom moves above the anchor; a viewport
  smaller than the box still keeps the top-left corner visible; and not one case reads pixels.
- **Tooltip rendering, by probe.** A throwaway window hosted the control with one `Warning` on a
  ruleset's `name`, put the pointer on the wave and rasterised the frame. Checked: the tip box is
  `(48.65, 20.62, 370, 50)` - under the anchor's line, one `Gap` below it, inside the control -
  with two wrapped lines, a `#F3F3F3` fill, a `#C8C8C8` border and `#1E1E1E` text; and the page
  outside the box is untouched. It also established that the wave under a `Warning` in
  `MarkerPalette.Light` is drawn but very soft, which is a palette question and not a bug in the
  tip.
- **Nodes that are still unnamed, headless.** Done in `DefaultNamesTests` (9 cases): a new
  document reports its ruleset; typing a name silences it; the message names the concept and the
  placeholder; a rule is reported as well as the root; findings follow document order; the named
  sample reports nothing; a model with no roots reports nothing; a concept that declares no
  default is not reported; and a `null` model is refused.
- **Control, when `tests/Projectional.Avalonia.Tests` exists.** A rendered wave for one
  error; the tooltip opening from a real pointer move and closing on a press; `F8` moving the
  caret to
  the next marker.
- **Rendering truth.** `LayoutSnapshot` is deliberately unchanged: it draws the layout, and a
  marker is not layout. Geometry tests assert rectangles, which is what a broken squiggle
  actually is.

## Open questions

Rows 35–38 in [open-questions.md](../open-questions.md) are the parts of this that are
choices rather than mechanics, and this document states its recommendation for each.
