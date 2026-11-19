# Editor tree, layout and the headless snapshot renderer

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How the editor tree is built

`Projectional.Projection/Tree/` is the runtime tree: notation cells are *data describing* what
to show, these cells *are* what is shown. `EditorTreeBuilder` is the only thing that turns one
into the other.

- **`EditorTreeBuilder(NotationRegistry).Build(node)` is the only public entry point.**
  `EditorTree`'s constructor is `internal` so no caller can invent a tree that was not built
  from notation; if a second construction path is ever needed, add it to the builder.
- **One tree per node, not one per model.** A change to a node rebuilds that node's tree and
  leaves every other tree alone. Child trees are nested through the child cells, and the
  child's root cell is the same object as the content of its `ChildCell`.
- **There is deliberately no inline/block split at runtime.** `ContainerCell` carries
  `Orientation` (`Line` -> horizontal, `Sequence`/`Indent` -> vertical), and `ChildCell` /
  `CollectionCell` are orientation-free. An inline child whose notation root is vertical
  breaks the line; do not push the orientation down onto the child, because the parent cannot
  see the child's notation.
- **Addresses come from the model, never from the notation.** Three shapes: node address
  (`slot` null) for constants, spaces and containers; slot address (`index` -1) for a cell
  showing a property/reference/child/collection; element address (`index` >= 0) for the root of
  one child's projection. Adding a keyword to a notation therefore moves no address, which is
  what keeps the caret stable across a rebuild.
- **Element addresses name the parent, not the child.** Deleting a child shifts the addresses
  of the ones after it without renumbering anything on the child side. Below a child's root,
  addresses belong to the child itself.
- **`Children` is fixed at construction and a cell is claimed by its parent.** `StructureCell`
  throws `ProjectionException` if a cell already has a parent, so assembling bottom-up makes
  "one cell, one place" a construction invariant rather than a convention. This is why the
  builder passes a root address *into* the recursive build instead of fixing addresses up
  afterwards.
- **A missing notation is a `MissingNotationCell`, not an exception.** A language is written
  concept by concept, so a model using a concept whose notation is not written yet must still
  open, save and display. `ProjectionException` is reserved for broken combinations - a
  duplicate cell, a slot the concept does not declare, a collection with the wrong number of
  separators.
- **Reference cells show text, never the target's subtree**, and have three states: `Unset`
  (notation's `UnsetText`), `Unresolved` (`<unresolved>`, target kept for repair) and `Resolved`
  (target's `Name`, or `<Concept.Key>` when the concept declares no name slot). A resolved
  `ReferenceCell` without a `Target` throws.
- **`CollectionCell` owns the interleaving.** `Content` is document order including
  separators; `Items` and `Separators` are kept because navigation and layout want different
  questions answered. Separators are never shared between gaps.
- **Spacing is the blank *after* a separator, never before it.** `CollectionNotationCell.Spacing`
  and `ChildNotationCell.Spacing` (default 1, negative throws) are the gap the notation asks for;
  with no separator the spacing *is* the whole gap, and there is no gap after the last item. A
  separator's own leading space belongs to the token (`Punct(",")` not `Punct(" ,")`, and
  `Line(Space, Keyword("and"))` for a word), so `ed.Punct(", ")` is now a bug rather than a
  workaround. Gaps are real `SpaceCell`s built from the collection's or slot's own address, so a
  gap of 3 is 3 cells and 3 caret stops - that is intended, not an oversight.
- **`TableCell` is a `StructureCell` whose content is the flattened grid** (headers, then rows,
  row-major), so every existing walk - caret, snapshot, style - sees table cells in drawn order
  without knowing what a table is. `Headers`, `Rows`, `ColumnCount` and `IsEmpty` exist because
  layout needs the grid, not the flat list. One cell per column always exists, even when the row
  has nothing there, so a column width is computed once. `Flatten` throws `ProjectionException`
  for headers without rows and for a ragged row - do not "repair" a grid in layout.
- **Table row cells are addressed exactly as the row's own notation would address them**
  (`CellAddress.OfSlot`/`OfElement` against the row node), which is why a table needs no address
  scheme of its own. Headers carry the *table's* node address, `ColumnCount` is `Headers.Count`,
  and a header may only be built from constants and spaces.
- `EditorTree.Find(address)` returns the **first** match in document order - usually the
  outermost container carrying a node address. `EditorTreeWalker` is the single place that
  defines document order; a second walk that disagrees with it is a caret bug.

## How layout works

`Projectional.Projection/Layout/` turns a cell tree into absolute positions. One pass, no
second tree.

- **`ITextMeasure` is the only thing that knows what a layout unit is.**
  `MonospaceTextMeasure` makes 1 character = 1 unit wide and 1 line = 1 unit tall, which is
  why layout expectations in tests can be counted off the notation. Never measure a string
  anywhere else, and never put pixels in this folder.
- **`CellLayouter(measure).Layout(tree)` returns a `CellLayout`**: root `Size` plus a flat
  `IReadOnlyList<PlacedCell>` in document order. A container is placed *before* its children
  and covers the same area, which is what makes both consumers work without walking a tree:
  `BoundsOf(cell)`/`BoundsOf(address)` scan forward and answer with the outermost cell,
  `Find(x, y)` scans backward and answers with the innermost.
- **The parent's slot is reserved before its children are arranged**
  (`placed.Add(default); ...; placed[index] = ...`) because a container only learns its size
  after arranging. Keep the reservation - it is what preserves document order.
- **Horizontal arrangement uses hanging continuation.** After a taller child, the cursor
  continues on that child's *last* line: `cursorX += w; bottom = max(bottom, cursorY + h);
  cursorY += h - lineHeight;`. Compute `bottom` **before** bumping `cursorY`; swapping those
  two lines is the bug that makes a closing brace float below its block.
- **Vertical arrangement**: an `IsIndented` container shifts children right by
  `ITextMeasure.IndentWidth` (monospace: 2); width is the widest child, height is the stack.
- **Nothing is zero width.** Empty text is measured as one space, because an unset property
  or empty slot must still be a column the caret can occupy. `PropertyCell.Text` is `""` when
  unset for the same reason. A property that declares an `EmptyText` is measured to that text
  instead - the stand-in is one token, never wrapped, and the value replaces it once there is
  one.
- **`ChildCell` lays its content out inline unless the notation asked it to stack**;
  `CollectionCell` always lays out inline. Only the surrounding container decides whether
  that line stacks, which is why a vertical list is a property of the notation -
  `Children(slot, stacked: true)` - and not something layout infers: the same language may
  want `a, b, c` in one place and a column in another.
- **A table is measured cell by cell, detached, and then translated.** `ArrangeTable` arranges
  each grid cell at the origin into a list of its own (`ArrangeDetached`), records the max width
  per column and the max height per row, computes the column lefts (one space gap) and row tops
  (no gap), and only then copies the placed cells into the real list with an offset. Never
  arrange grid cells in place: a cell's position depends on the widths of cells to its right in
  the same column, which are not known yet.
- **An empty `TableCell` lays out like an empty `CollectionCell`** - the `EmptyText`, one line -
  because `IsEmpty` is a separate case in `Arrange` before the table branch. Forget that branch
  and an empty table arranges as a zero-column grid.
- **Unknown cell kind throws `ProjectionException`** - the cell set is closed, so a new cell
  must be added to `CellLayouter` on purpose.
- **Width-independent on purpose.** `Layout(tree)` takes no width because no built cell
  rearranges with width. Viewport-only layout, width classes, line-height caching and
  device-pixel snapping are deferred, not forgotten.
- **The layout remembers what a cell cannot be asked for again, and nothing else.** Three rules, each
  worth hundreds of bytes per cell on a real document (286 ms and 134 MB per layout became 35 ms and
  43 MB on 280 011 cells; measured numbers in `docs/architecture/semantics.md`):
  `LinesOf(cell)` holds only the texts that **wrapped**, because one line is the text plus its width -
  a single-line text is answered from the measure, and `CellLayouter` asks
  `TextWrapper.SingleLineWidth` before it builds a `LaidOutText` at all. The text map is **kept, never
  copied** - a `CellLayout` is the only holder of the layouter's map, and a copy is a full pass over
  the document for nothing. The bounds map is **built on the first `BoundsOf(EditorCell)`**, because a
  painter walks `Cells` and never asks. Do not "tidy" any of the three back into building eagerly:
  each one is O(cells) work per keystroke, and the test that pins the reconstruction is
  `CellLayoutTests.ATextThatStayedOnOneLineIsAnsweredWithoutBeingStored`.
- `TextCell` is the shared base of every cell whose content is one string, so layout asks one
  type for `Text` instead of switching over five kinds. Add new text cells under it.

## How the snapshot renderer works

`Projectional.Projection/Snapshot/LayoutSnapshot` is the headless half of the editor: it
turns a model into text with no window, no font and no Avalonia, which is how notation gets
tested.

- **Two entry points.** `Render(node, notations, width)` for one node (what notation tests
  use) and `Render(model, notations, width)` for all roots. Both build with one
  `EditorTreeBuilder`, lay out with `MonospaceTextMeasure`, then draw.
- **It draws layout, it does not re-derive it.** Each `PlacedCell` is drawn at
  `round(Bounds.X/Y)`; nothing recomputes a position. A snapshot failure is therefore a
  layout failure, not an artifact of the harness. Keep it that way - the moment this file
  starts measuring strings it becomes a second layout implementation.
- **Draw only what has text**: `CellText.Of(cell)` - a `TextCell`'s own text, the `EmptyText` of an empty
  `ChildCell` or `CollectionCell`, and the stand-in of a `PropertyCell` whose value is empty. The cell's own
  text is what one line draws, because the text a property shows is not always the text layout wrapped.
  Containers carry no text, so drawing in document order can never bury
  a child under its parent, and one `Write` per cell is enough because every text cell is one
  line tall.
- **Empty slots must show their placeholder.** Layout already reserves `EmptyText`'s width;
  a renderer that skipped it would print a projectional language with invisible slots.
- **Width is a cut, not a wrap**, because the caller asked for that width. `SnapshotGrid`
  throws on overflow instead of clipping, so clipping has to happen here, deliberately.
- **Roots stack with no gap** - vertical spacing is a notation's business, not the renderer's.

