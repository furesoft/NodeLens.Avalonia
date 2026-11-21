# Projection

Projection turns a model into something visible and editable. It is the part with the
highest technical risk, so it is specified most carefully and is the first thing to
prototype (spike) before committing to the full implementation.

## Pipeline

```text
Node ──(concept notation)──▶ EditorTree ──(measure + arrange)──▶ CellLayout ──(render)──▶ GPU
```

Three distinct structures:

| Structure | Depends on | Invalidated by |
| --- | --- | --- |
| `EditorTree` | node data + concept notation + visibility aspects | any change on that node |
| `CellLayout` | editor tree + available width (for cells that need it) | editor tree change, or width change |
| Canvas | placements + scroll offset | scroll, selection, focus, error markers |

No structure is cached yet: the tree is built and laid out on demand, which is fast enough
for a whole model. The invalidation column is the rule a cache has to honour once the
control adds one.

Separating the editor tree from layout is what makes the notation declarative: the
tree is pure structure, layout is pure geometry, and neither knows about Avalonia.

## Editor tree

An editor tree is built from one node and its concept's notation, and it is disposable:
it describes the model as it was at build time and is dropped when either changes.
Nothing in the model points at a cell, so anything that must survive a rebuild — the
caret, a selection, a scroll anchor — holds a `CellAddress` instead.

Cells of the editor tree (namespace `Projectional.Projection`, folder `Tree/`):

| Cell | Content |
| --- | --- |
| `EditorCell` | abstract base: address, parent, children, `IsLeaf`/`IsCaretTarget` |
| `StructureCell` | arranges other cells and claims them as children |
| `ContainerCell` | `Line` → horizontal, `Sequence` → vertical, `Indent` → vertical + indented |
| `TextCell` | abstract base for every cell whose content is one string |
| `ConstantCell` | literal text (keyword, punctuation), with its `ConstantKind` |
| `SpaceCell` | one column of blank the notation asked for |
| `PropertyCell` | property text, its `PropertyType`, whether the slot is set, and the notation's stand-in while it is empty |
| `ChildCell` | one child slot: the child's projection, one per line when the notation asks for it, or its empty text |
| `CollectionCell` | a list slot: items interleaved with separators and spacing |
| `ReferenceCell` | target name, `<unresolved>`, or the notation's unset text |
| `MissingNotationCell` | marker for a concept whose notation is not written yet |

Three notes on this shape, all of which were decided while building it:

- **There is no inline/block split at runtime.** A layout container decides whether its
  content runs horizontally or vertically, so the same `ChildCell` can be stacked in one
  notation and run on a line in another. The inline/block distinction lives in the
  notation types, where it is checked once at registration. A notation asks for the column
  form with `Children(slot, stacked: true)`; a `CollectionCell` stays inline, because a
  separator drawn between two lines needs a rule of its own that no language has needed yet.
- **Orientation is on the container, not on the child.** An inline child whose notation
  root is vertical breaks the line, exactly as `[ ... ]` does in MPS. Deciding this per
  child at runtime cannot work, because the child's notation is not visible from the
  parent's tree.
- **A concept without notation is a marker, not an exception.** A language is written
  concept by concept, and a model that uses a concept whose notation is missing must
  still open, save and show the rest of itself.
- **Every text-bearing cell shares one base.** `TextCell` exists so that layout and
  rendering ask a single type for the string to draw instead of switching over five
  cell kinds; the kind still decides what the text *means* (a keyword versus a
  reference target), not how wide it is.

`TableCell` and `ErrorOverlay` are not part of M1: tables arrive with the table language
in M2, and error markers are decorations drawn on top of the cell tree rather than cells
inside it.

### Addresses

Every cell carries its **address**: `(owning NodeId, slot, index in list)`, which is what
caret, hit testing, completion and undo use. There are three shapes:

| Shape | Carries | Given to |
| --- | --- | --- |
| node address (`slot` null) | the node | constants, spaces and the containers in between |
| slot address (`index` -1) | node + slot | the cell that shows a property, reference, child or collection |
| element address (`index` ≥ 0) | parent + slot + position | the root of one child's own projection |

Addresses come from the model, never from the shape of the notation, so adding a keyword
to a notation changes the tree without moving a single address. Element addresses name
the *parent*, so deleting a child shifts the addresses of the ones after it without
anything being renumbered on the child side.

Below a child's root cell, addresses belong to the child itself: reaching `body` at
`(rule, body, 0)` says nothing about the addresses of the cells inside `body`.

Language authors do not build trees imperatively; they declare notation and
`EditorTreeBuilder` builds the tree (see
[language.md](language.md#notation)).

## Layout

Layout turns the cell tree into absolute positions. It is one measure/arrange pass over
the tree, in abstract **layout units** rather than pixels.

- **Units, not pixels.** `ITextMeasure` is the only component that knows what a unit is.
  `MonospaceTextMeasure` makes one character one unit wide and one line one unit tall,
  which is what lets the layout tests state their expectations by counting characters
  off the notation. A measure backed by real font metrics (and DPI) plugs in without
  touching the arrangement.
- **A flat placement list, not a second box tree.** The pass produces a `CellLayout`:
  the root size plus one `PlacedCell(cell, bounds)` per cell in document order. Nesting
  stays observable because a container is placed before its children and covers the same
  area, so both consumers - drawing the viewport and hit testing - read absolute
  coordinates instead of walking the tree again.
- **Two arrangement rules.** A horizontal container advances a cursor to the right and
  grows downwards to its tallest child; a vertical container stacks its children and, when
  it is an indent, shifts them right by `ITextMeasure.IndentWidth`. The width of a stacked
  container is its widest child, not the sum of them.
- **Continuation.** After a taller child inside a line, the cursor continues on that
  child's *last* line. A nested block therefore keeps the cell that follows it - typically
  a closing brace - beside its last line, which is the look MPS has.
- **A list slot says how wide its gaps are, and the gap comes after the separator.**
  `CollectionNotationCell.Spacing` and `ChildNotationCell.Spacing` (one column by default)
  are the blank that *follows* each separator, or the whole gap when the notation declares
  no separator. Nothing is inserted in front of a separator, because whether a token wants
  a space before it belongs to the token - a comma does not, `and` does - so that space is
  written into the separator notation itself (`Punct(",")` beside `Line(Space, Keyword("and"))`).
  There is no trailing gap after the last item. A gap is real cells, not padding applied by
  the layout, so a wide gap is several `SpaceCell`s and every one of them is a caret stop.
- **A stacked child slot can leave a blank row between groups.** `ChildNotationCell.BlankLineBetweenGroups`
  asks for one, and `ChildCell.BlankLineBefore` carries the positions it applies to: the stack advances the
  cursor by one line height before each of them. The gap is *space*, not a cell, because a childless cell is a
  leaf and every leaf is a caret target - a spacer would be a caret stop on a row nobody drew. The positions
  come from `ChildGroups.IsBoundary`, the same rule the generator applies, so the drawing and the generated
  text put the blank line in the same place. A group boundary is vertical space, so only a stacked slot can
  draw one; the flag on a single-line slot does nothing.
- **Where a list may stand is a type, not a flag.** `CollectionNotationCell` is a block cell and
  starts its own line; `InlineCollectionNotationCell` stays inside the line it is drawn on, with the
  same `Separator`/`Spacing`/`EmptyText` and the same runtime `CollectionCell` (which arranges its
  items inline either way). A notation that asks for the wrong one fails to compile, which is why the
  two exist rather than one cell with a `Stacked` flag.
- **A text cell is laid out as lines, and breaks itself at a space when the page does
  not reach.** The text of a property is broken into `TextLine`s by `TextWrapper` before
  it is measured: a `\n` in the value always ends a line, and when a wrap width is known
  the wrapper also breaks at the last space that still fits. Each line starts at the
  cell's left edge, so the columns of a continuation line are counted from the break, not
  from the start of the cell. A break the user put there and a break the width forced are
  the same line; only the model knows which is which, and only for the purpose of editing.
- **A word wider than the room left is drawn whole, past the edge.** A soft break is only
  ever taken at a space, never between two characters of a word. Breaking a word is what
  turns a cell that starts just inside the page edge - one character of room - into one
  line per character, which makes a three-line document eight rows tall in the gutter and
  is unreadable besides. Running past the edge is the behaviour a cell that starts *past*
  the edge already has, and the horizontal scrollbar is what makes the overflow reachable.
  The consequence for a host is that a wrapping cell can end up wider than the wrap width,
  so the layout's own width grows with it.
- **Wrapping is a limit, not a page.** The wrap width is the room a cell has, and
  `CellLayouter.LimitAt(x)` subtracts the x of the cell from it, so an indented value wraps
  earlier than a flush one. With no width - a host that does not measure, or an unbounded
  measure pass - nothing soft-wraps and only a `\n` in the value breaks a line.
- **Nothing is zero width.** An empty text cell is measured as one space. An unset
  property and an empty slot are still positions the caret can occupy, so they must
  occupy a column. This is why `PropertyCell.Text` is the empty string when unset rather
  than null, and why an empty `ChildCell` or `CollectionCell` is measured to its
  `EmptyText`: the placeholder is what fills the slot, so it is also what sizes it. A
  property that declares an `EmptyText` - `<<operator>>` - is measured to that instead, and
  the value replaces it the moment there is one. The placeholder is one token, never
  wrapped, and never part of the text the caret moves through.
- **Lookups.** `BoundsOf(CellAddress)` scans document order and answers with the outermost cell
  carrying that address; `Find(x, y)` scans in reverse and answers with the innermost cell under
  the point. `BoundsOf(EditorCell)` answers from a map that is built on its first call, because a
  painter never asks for it - it walks `Cells` and has each cell's bounds in hand - while the caret,
  the hit test and the markers do. `LayoutRect.Contains` is half-open, so a point on a border
  belongs to exactly one cell.
- **What the layout remembers per cell, and what it refuses to.** The placement list is the layout;
  everything else is an index into it. `LinesOf(cell)` answers from a map holding only the texts
  that **wrapped**, because one line is the text plus its width and needs no break to remember; a
  single-line text is answered from the measure, which is not a re-measure of the case that would
  cost one. Three rules follow, and each was worth several hundred bytes per cell of a document:
  the text map is **kept, never copied** (a `CellLayout` is the only holder of the layouter's map),
  the bounds map is **built lazily**, and `CellLayouter` asks `TextWrapper.SingleLineWidth` before it
  builds a `LaidOutText` at all - a text with no break of its own that fits the limit is one line, and
  that is most cells of most documents. Measured on 280 011 cells: 286 ms and 134 MB before, 35 ms
  and 43 MB after.

Only a text cell breaks with the width; nothing else in the notation does. A narrow page
therefore makes a value taller instead of rearranging the notation, which is what keeps a
projection recognisable at any size. The width itself is view state the host publishes once
per measure pass (`EditorSession.WrapWidth`), so two hosts can show one document at
different widths; setting the same width again is free, so a control that publishes on every
frame does not rebuild the layout the caret was measured in.

Deferred on purpose, each with the milestone that needs it:

| Deferred | Why it can wait |
| --- | --- |
| Viewport-only layout, line-height cache | needs a scrolling control (M1) |
| Width classes, container-level reflow | only text reflows today; nothing else depends on width |
| Re-breaking only the changed line | costs more than re-breaking one value until values get long |
| Snapping to device pixels | needs a real text stack (M1 control) |
| Re-arranging only the changed subtree | needs the diffing of a repeat layout (M1 control) |

Budget (targets, to be measured by a benchmark suite once the control exists):

| Operation | Target |
| --- | --- |
| Full layout of a 1000-line model | < 150 ms |
| Incremental layout after one keystroke | < 8 ms |
| Hit test / caret placement | < 0.2 ms |
| Frame render of the viewport | < 8 ms |

## Caret and selection

The caret is a **structural address plus a cell discriminator**, never a character index:

```text
CaretPosition(address, offsetInCellText, documentOrderOfCell)
```

Rules:

- An address alone does **not** identify a cell: every constant keyword, punctuation mark
  and space of a node carries that node's address, so `rule` at the start of a line and the
  `{` at its end share one. The document-order index of the cell is what disambiguates
  them. [editing.md](editing.md#the-caret-is-a-cell-not-an-address) owns this rule.
- Adjacent cells collapse — a caret between `then` and `Accept` is one position, not two.
- When bounds are missing (empty list, abstract node), the caret attaches to the
  nearest *visible* leaf cell, not to an invisible structural node. A caret must
  always correspond to something the user can see.
- Selection is a pair of positions plus a resolution rule; a selection only lasts as
  long as the nodes involved exist. If a selected node is deleted, the selection
  shrinks to what is left — it never silently jumps to a different node.
- The caret address survives re-layout, so scroll, resize, undo and language changes
  do not move it.
- A position names a **line** as well as a cell, because a cell can be several lines tall.
  The offset decides which line it is on, and every question the caret asks about its own
  geometry - where the bar goes, which offset a click found, where Home ends, which line is
  "up" - is answered from that one place (`CaretGeometry`), so the bar, the highlight, the
  click and the vertical move cannot drift apart. Columns are counted from the start of the
  line rather than of the cell, so the second line of a wrapped value starts at column zero
  again. [editing.md](editing.md) owns the movement rules.

## Hit testing and input

Input is routed through the touched cell:

| Input | Routing |
| --- | --- |
| Printable character | the cell decides: replace selection, append to property text, filter completion, or trigger substitution |
| Arrow keys | structural movement between cells along the logical line, then lines |
| Ctrl+Left / Ctrl+Right | one character inside the current cell, because the plain arrows move a whole cell |
| Tab / Shift+Tab | next / previous slot of the current node in declaration order; a filled child or collection is entered rather than skipped, because only its leaves are caret stops |
| Alt+Up / Alt+Down | move the node the caret is in one place earlier / later among its siblings, identity and caret preserved ([node-moves.md](node-moves.md)) |
| Enter | in a value of text, opens a line at the caret; everywhere else, fills the empty slot at the caret per the list strategy of the enclosing cell |
| Space | same as a printable character; there are no whitespace semantics |
| Delete/Backspace | per cell: shrink text, remove child, delete node, or nothing when the slot is mandatory |
| Escape | close completion, clear selection |

Every action is expressed as an **edit action** object, not as a direct mutation. This
keeps the input layer testable headlessly and gives the substitution menu the same
entry point as the keyboard. [editing.md](editing.md) describes the action model, the
caret place rules and the session cycle.

### What M1 implements

The table above is the target. M1 delivers the skeleton of it, and the boundaries are
worth knowing because they are the seams every later feature attaches to:

| Piece | Responsibility |
| --- | --- |
| `HitTest.Caret` | pointer position → caret place; picks the innermost cell under the point, then snaps to the nearest character boundary |
| `EditorKeyMap` | Avalonia `Key` + modifiers → `EditorKey`, in one table, and `null` for everything else |
| `KeyRouter` | `EditorKey` → edit action; the only place that decides what a key means |
| `EditorSession` | applies the action, rebuilds when the model changed, settles the caret, records undo |

Two rules fall out of having those three layers instead of one event handler:

- **A key held with Control, Alt or Meta is not the editor's**, with two deliberate
  exceptions. Ctrl+Left is a word jump in the host application and Ctrl+Delete deletes a
  file in a file manager; consuming them because the bare key happens to be routed would
  take shortcuts away from the very program that embeds the editor. The first exception is
  Ctrl+Left / Ctrl+Right: the plain arrows move a whole cell, so the character step has to
  stay reachable, and no host is likely to want those two keys more than a text editor
  wants them. The second is Alt+Up / Alt+Down, which reorder the node under the caret: the
  vertical arrows carry no meaning without a modifier, so the gesture costs the host
  nothing it could have been using. Shift is different as well — Shift+Tab walks slots
  backwards, so it belongs to the editor.
- **A refused character is not consumed.** Typing a letter into a number yields
  `EditOutcome.Rejected`, the model is untouched, and the event stays unhandled so the
  host can beep, report it or reroute it. The three outcomes exist for exactly this:
  applied, unchanged and rejected are not a boolean.

Text arrives as text input, never as key events, so the control sees `"lohn"` as one
edit rather than four. That is also where IME composition will land: the composition
string becomes the text of the edit, and nothing below the control has to change.


## Substitutions and transformations

The mechanism that separates an editor from a form. A substitution menu is offered at
a caret position (by shortcut, context menu, or automatically on an empty slot) and
lists actions that create or wrap nodes:

```text
caret on empty slot  →  "Insert rule", "Insert group", "Paste as rule"
selection            →  "Wrap in condition", "Extract to action", "Convert to table row"
```

Actions come from an aspect per concept, filtered by what is legal at that caret. Each
action produces an edit action, so undo and change tracking are automatic.

**Landed as the intention list.** An action provider is an `IIntentionProvider`
(`Projectional.Editing/Intentions/`), the list is derived per caret from the host's and the
language's providers plus the quick fixes, and the standard operations - rename, duplicate,
delete, move, wrap in X, replace with Y, sort, insert, select - ship in
`Projectional.Intentions`. [context-actions.md](context-actions.md) is the plan of record and
[implemented.md](../implemented.md) the status. A **range** is addressed too: `Wrap selection in
"X"` acts on every node the selection covers completely, as one undo step, so selecting several
siblings and surrounding them together is one entry rather than a keyboard-only gesture.

## Tables

Tables are a first-class cell, not a hack, because the pilot DSL needs them. The design
shape, and how much of it M1 already delivers:

- Rows come from a child list; columns are fixed (declared) or taken from each row's
  roles. **M1: declared columns only.**
- Column widths are computed per table; a cell edit invalidates the column, not the whole
  model. **Computed per layout, and a cell edit re-lays out the table.** The column cache
  belongs with the incremental invalidation slice. A reader may drag a column wider:
  `IColumnWidths` is the question layout asks, `ColumnWidths` (in `Projectional.Editing`) is
  the answer, and it is **view state** - never saved, never an undo step, keyed by the stable
  node id. The width is a **floor, not an exact width**: a column narrower than its content
  would clip a value or overlap its neighbour, and a projectional editor never hides what the
  model says. `ColumnResize.HandleAt` finds the boundary under a point (the right edge of a
  column, inside the header row only - the body is where the reader selects and types), and
  the control turns a drag into `EditorSession.SetColumnWidth`.
- The caret moves cell-wise with Tab and row-wise with Up/Down. **M1: Tab still inserts a
  sibling; the caret reaches the next column by walking to the end of the cell's text.**
- A table with zero rows must render its header plus an "add row" affordance. **Done: an
  empty table draws its header and one `AddRowCell` row**, whose text is the notation's
  `EmptyText` (`ed.Table("rules", "+ rule", ...)`) and whose address is the *slot's*, not
  the table's - a table that is the root of its node's notation carries the node's address,
  which its headers carry too, so an affordance addressed that way would be
  indistinguishable from the first header. Enter on it fills the slot through the same
  `InsertChildAction` an empty child slot uses, and `CaretPlace.IsEmptySlot` counts it, so
  Tab reaches it.

### What M1 implements

A table is a variant of a concept's notation rather than a special document mode, which
is what makes it work on a concept that also has a textual form
([ADR 0012](../adr/0012-concept-rendered-more-than-one-way.md)):

| Layer | Type | Job |
| --- | --- | --- |
| Language | `TableColumn` | one column: the row slot it shows, its header cell, its empty text |
| Language | `TableNotationCell` | the table: the child slot holding the rows, the ordered columns |
| Language | `NotationRegistry.ValidateTable` | refuses a column naming no slot, a slot of another concept, a name that is no property, child or reference, or a many-valued slot |
| Projection | `TableCell` | the shape: row-major flattened content, `Headers`, `Rows`, `ColumnCount`, `IsEmpty` |
| Projection | `AddRowCell` | the one row an empty table draws: the placeholder in the first column, blanks in the rest |
| Projection | `EditorTreeBuilder.BuildTable` | one cell per column per row, each row cell addressed exactly as the row's own notation would address it |
| Layout | `CellLayouter.ArrangeTable` | column widths and row heights |
| Snapshot | `LayoutSnapshot` | the text grid a test can compare |

The layout rules are few because they are the ones that survive contact with real data:
a column is as wide as its widest header or cell, a row is as tall as its tallest cell,
columns are separated by one space, and rows are not separated at all. Measuring happens
detached at the origin and the finished grid is translated afterwards, so a table cannot
inherit the offset of whatever block preceded it.

The header cells belong to the *table's* notation, not to the row's, and may only be
constants and spaces — a header that named a slot would have to be answered by whichever
row happened to be first. That, the "no column twice", and the "at least one column" rules
are all refused by the cell constructors, and the column's slot is checked against the row
concept by `NotationRegistry`, so a broken table fails when the language is built. **A column
shows a property, a single child or a reference** - the three slots that hold exactly one thing
- so a foreign key can be drawn as a column and is projected by the same `ReferenceCell` a
notation's reference cell is, with the column's own empty text standing in for an unset one. A
table without rows still draws its header, followed by the affordance row, so the reader can see
what the columns are before there is anything in them; `TableCell` refuses a table with no
columns and every row's column count with a `ProjectionException`, so even a table built by
hand fails at the first projection instead of painting half a grid.

## Rendering in Avalonia

- One custom `Control` owning a `ScrollViewer` with `ILogicalScrollable` implemented by
  the editor itself (needed for pixel-perfect caret visibility and virtualized
  scrolling).
- Text drawn per line via `Avalonia.Media.TextFormatting.TextLayout`; glyphs are shaped
  once per line and cached.
- No per-cell controls. A viewport with thousands of `TextBlock`s would destroy
  performance and make hit testing ambiguous.
- Selection and error underlines are drawn as rectangles/underlines from the same
  layout data, so they cannot drift from the glyph positions.

### What M1 implements

`ProjectionalEditorControl` is one `Control` that paints the whole frame itself:
background, selection rectangles, one `FormattedText` per text run, then the caret bar — which
is skipped while its blink phase is off, so a repaint can mean "same picture minus the caret".
It asks `EditorFrame` what to paint and shapes glyphs with `AvaloniaTextMeasure`, so
nothing about *what* is on screen is decided in the control.

Three deltas from the design above, each deliberate and each a later slice:

- **`FormattedText`, not `TextLayout`.** One run is one cell's text — a keyword, a number, a
  name — not a line, so per-line shaping would fight the cell boundaries the caret and the
  hit test depend on. Shaping is cached per string, which is the same win for the common
  case where a model repeats its keywords.
- **No `ScrollViewer` yet.** The control clamps its own offsets through
  `LayoutViewport.Clamp` and scrolls on wheel and on caret movement, which is enough for
  a keyboard-driven editor. `ILogicalScrollable` is what makes host scrollbars and
  smooth kinetic scrolling behave, so it is additive rather than a rewrite. The plan for
  it, including the property mapping and why the bars belong to a `ScrollViewer` the host
  owns rather than to the control, is [scrolling.md](scrolling.md).
- **No incremental invalidation yet.** A changed model rebuilds the tree and the layout,
  which is correct and fast enough at the size M1 targets; the invalidation scheme below
  is a performance slice, gated by the M1 latency budget.

### Known hard parts

These are called out because they are the parts that get deferred and then hurt:

| Area | Requirement |
| --- | --- |
| **IME / dead keys** | implement `TextInputMethodClient`; composition text must be shown at the caret and committed key by key. Required for CJK and accented input. |
| **Accessibility** | custom `AutomationPeer` exposing the model as a text-like tree; screen readers need line/cell granularity. |
| **Clipboard** | two formats at once: structural (XML fragment, preserves node data) and plain text (the projection). Paste prefers structural, falls back to text. |
| **Bidirectional text** | out of scope before 1.0, but caret math must not assume left-to-right only. |
| **Fonts** | fallback chains per language for symbol keywords (e.g. arrows, `≤`). |

## Styling

Highlighting is derived, like everything else. The model stores no colour, the notation
stores no colour, and a saved model cannot carry formatting that disagrees with the
concept that produced it. What travels is *meaning*, and meaning is decided at projection
time from what a cell already is:

| Layer | Decides | Type |
| --- | --- | --- |
| `Projectional.Projection` | what a cell's text *is* | `TextStyleKind` + `ICellStyler` |
| `Projectional.Projection` | how a cell's text is *set* | `TextEmphasis` on the cell |
| `Projectional.Editing` | which run carries which kind and setting | `TextRun.Style` + `TextRun.Emphasis` |
| `Projectional.Avalonia` | what that kind and setting *look like* | `SyntaxPalette` + the shaped text |

`CellStyler.Default` reads the cell and nothing else: a `ConstantCell` is a keyword or
punctuation as the notation declared it, an `int` or `date` property is a number, a
`string` property is text, a resolved reference is a reference and an unresolved one is
unresolved, and an empty slot or a concept without a notation has its own kind. It never
reaches back into the node, because a style that consulted the model could describe a
value the layout never measured.

`ICellStyler` is an interface rather than a fixed switch for two reasons. Classification
is a judgement about *the language* - a DSL may want its operators in their own colour, or
its type names - and the host is the only one who knows; and a styler is cheap to replace
because it is pure. **Purity is a requirement, not a nicety**: layout measures a cell's
text before the frame is built, so a styler that changed its answer between two calls
would produce a frame whose colours describe different text than its bounds.

The two axes are separated by what they cost. **Colour changes no number, so a palette may
change it; emphasis changes the advance of a glyph, so only the projection may decide it.**
Layout runs before any palette is consulted and its bounds are what the caret, the hit test
and the selection rectangles are computed from. A palette that set a keyword bold would
draw a word wider than the box the layout handed it, and every one of those would sit a few
pixels off. Emphasis is therefore not a palette decision at all: it is on the cell, and the
frame is measured with it.

Two consequences worth knowing:

- **Swapping a palette is a repaint, not a re-projection.** The model, the tree, the layout
  and every bound in it stay exactly as they were. This is the whole point of separating
  meaning from appearance.
- **Shaping is cached per `(text, brush, emphasis)`.** Measuring shapes at the default brush,
  so a `Default` run is painted from text that has already been shaped; the control points
  its measure at the palette's default brush to keep it that way, and only genuinely coloured
  runs pay for a second shape. Emphasis is part of the key because the same word in two
  settings is two different shapes.

A palette that omits a kind falls back to `Default` rather than throwing, which makes a
partial palette usable: a host that wants its own keyword colour says just that.

### Emphasis

`TextEmphasis` is a `[Flags]` set of the four settings a run can carry: bold, italic,
underline and strikethrough. It has two sources, and both are consulted while the tree is
built rather than while it is painted:

| Source | Says | Declared in |
| --- | --- | --- |
| The notation | how this concept *always* reads | `NotationEditor.Keyword("rule", TextEmphasis.Bold)` |
| `ICellEmphasis` | how *this node* reads today | a resolver the host hands to the session |

The notation is the language's own statement, so it is where a keyword that is bold in every
document belongs. `ICellEmphasis` is the model-driven statement: `EmphasisOf(Node)` is asked
once per rebuild for every node, and the sample answers "struck through" for a rule whose
outcome is `Reject`. Nothing is stored, nothing is persisted, and changing the outcome back
undoes the decoration on the next rebuild - which is exactly the difference between "a rule
always reads like this" and "this rule reads like this right now".

Three rules make the two compose predictably:

- **A resolver's answer inherits into the subtree.** Asking for a struck-through rule strikes
  its keyword, its name, its condition and its outcome word, because the rule answered for
  everything drawn inside it. A resolver only has to name the node it cares about.
- **The notation's emphasis is added on top at each leaf**, so a struck-through rule keeps
  its bold `rule` keyword: the cell carries `Bold | Strikethrough`.
- **`ICellEmphasis` is not `ICellStyler`.** A styler is pure and may be swapped by a palette;
  an emphasis resolver is consulted during a rebuild and its answer is measured. Folding them
  together would let a "styler" silently change the layout.

`ICellEmphasis` lives in `Projectional.Language` beside the notation rather than in
`Projectional.Projection`. A resolver is the same kind of statement as a notation - how this
language reads - and a language package must be able to declare one without referencing the
projection layer.

The measure takes the emphasis because it has to: `ITextMeasure.Measure(string, TextEmphasis)`
has no default, so a host cannot forget that a cell's width depends on how it is set. The
monospace measure ignores emphasis on purpose - a monospaced face keeps its advance when bold,
and pretending otherwise would make the headless snapshot disagree with every real editor.

### Diagnostics are not emphasis

A squiggle under a mistaken cell looks like an underline and is not one. Emphasis is a
property of the projection: it is decided while the tree is built, it is measured into the
layout, and it is stable as long as the model is. A diagnostic is a claim from the last
validation pass — it arrives asynchronously, it is recomputed rather than stored, and it must
not touch the tree, because a tree that carried it would have to be rebuilt every time a
diagnostic came in.

So markers are painted from a channel beside the frame rather than from the cell: the editing
layer resolves a diagnostic's node to rectangles per line, and the control draws a wave in a
colour the theme supplies. Nothing about the projection changes, and a palette swap still moves
no number. The plan is [markers.md](markers.md).

## Incremental invalidation

On a change event:

1. Invalidate editor trees of the changed node and its ancestors up to the model root.
2. Invalidate layout boxes for the same range, plus the enclosing table column if the
   node is a table cell.
3. Recompute only the visible viewport during the next render pass.
4. Preserve scroll offset by anchoring on the node at the top of the viewport. Anchoring
   on a node ID (not a line index) keeps the view stable when lines above change height.

## Headless rendering for tests

The projection engine can render into a text grid without Avalonia:

```csharp
string ascii = LayoutSnapshot.Render(model, notations, width: 80);
string oneNode = LayoutSnapshot.Render(node, notations, width: 80);
```

It builds the tree, lays it out with a monospace measure and draws each text-bearing cell
at its placement, so what it prints is what layout decided rather than a second opinion
about it. One layout unit is one character and one row, text wider than the requested
width is cut, and an empty slot shows its `EmptyText`. This one function carries most of the
notation regression tests and is a hard requirement for M1, not a nice-to-have.
