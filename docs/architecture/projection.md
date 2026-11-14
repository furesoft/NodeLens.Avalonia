# Projection

Projection turns a model into something visible and editable. It is the part with the
highest technical risk, so it is specified most carefully and is the first thing to
prototype (spike) before committing to the full implementation.

## Pipeline

```text
Node ──(concept notation)──▶ EditorTree ──(measure)──▶ LayoutBox tree ──(render)──▶ GPU
                                   ▲                          ▲
                             cached per node           cached per node + width
```

Three distinct structures, each with its own cache and invalidation rules:

| Structure | Depends on | Invalidated by |
| --- | --- | --- |
| `EditorTree` | node data + concept notation + visibility aspects | any change on that node |
| `LayoutBox` | editor tree + available width class | editor tree change or width class change |
| Canvas | layout boxes + scroll offset | scroll, selection, focus, error markers |

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
| `ConstantCell` | literal text (keyword, punctuation), with its `ConstantKind` |
| `SpaceCell` | one column of blank the notation asked for |
| `PropertyCell` | property text, its `PropertyType` and whether the slot is set |
| `ChildCell` | one child slot: the child's projection, or its empty text |
| `CollectionCell` | a list slot: items interleaved with separators |
| `ReferenceCell` | target name, `<unresolved>`, or the notation's unset text |
| `MissingNotationCell` | marker for a concept whose notation is not written yet |

Three notes on this shape, all of which were decided while building it:

- **There is no inline/block split at runtime.** A layout container decides whether its
  content runs horizontally or vertically, so the same `ChildCell` can be stacked in one
  notation and run on a line in another. The inline/block distinction lives in the
  notation types, where it is checked once at registration.
- **Orientation is on the container, not on the child.** An inline child whose notation
  root is vertical breaks the line, exactly as `[ ... ]` does in MPS. Deciding this per
  child at runtime cannot work, because the child's notation is not visible from the
  parent's tree.
- **A concept without notation is a marker, not an exception.** A language is written
  concept by concept, and a model that uses a concept whose notation is missing must
  still open, save and show the rest of itself.

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

Layout is a two-pass measure/arrange over the box tree, restricted to the viewport.

- **Line-oriented for text-like notation.** A line is the unit of vertical layout;
  wrapping is only produced by cells that opt in (e.g. a long list). This matches how
  MPS feels and keeps vertical position computation O(1).
- **Virtualization by page.** Only visible lines plus a small overscan are laid out.
  Line heights are cached and reused from the previous layout, so scrolling does not
  re-measure.
- **Width classes.** If a `LayoutBox` depends on available width (few notations do),
  the cache is keyed by a quantised width class, not by exact pixel width. This
  prevents cache thrash on window resize.
- **Baseline snapping.** Text is drawn on integer device pixels to avoid blurred
  glyphs; the caret and selection rectangles snap to the same grid.

Budget (targets, checked by a benchmark suite from M1):

| Operation | Target |
| --- | --- |
| Full layout of a 1000-line model | < 150 ms |
| Incremental layout after one keystroke | < 8 ms |
| Hit test / caret placement | < 0.2 ms |
| Frame render of the viewport | < 8 ms |

## Caret and selection

The caret is a **structural address**, never a character index:

```text
CaretPosition(nodeId, role, index, offsetInCellText)
```

Rules:

- Multiple positions with the same address are equivalent, and adjacent cells
  collapse — so a caret between `then` and `Accept` is one position, not two.
- When bounds are missing (empty list, abstract node), the caret attaches to the
  nearest *visible* leaf cell, not to an invisible structural node. A caret must
  always correspond to something the user can see.
- Selection is a pair of positions plus a resolution rule; a selection only lasts as
  long as the nodes involved exist. If a selected node is deleted, the selection
  shrinks to what is left — it never silently jumps to a different node.
- The caret address survives re-layout, so scroll, resize, undo and language changes
  do not move it.

## Hit testing and input

Input is routed through the touched cell:

| Input | Routing |
| --- | --- |
| Printable character | the cell decides: replace selection, append to property text, filter completion, or trigger substitution |
| Arrow keys | structural movement between cells along the logical line, then lines |
| Tab | next slot in the current concept, in declaration order |
| Enter | newline/sibling per the list strategy of the enclosing cell |
| Space | same as a printable character; there are no whitespace semantics |
| Delete/Backspace | per cell: shrink text, remove child, delete node, or nothing when the slot is mandatory |
| Escape | close completion, clear selection |

Every action is expressed as an **edit action** object, not as a direct mutation. This
keeps the input layer testable headlessly and gives the substitution menu the same
entry point as the keyboard.

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

## Tables

Tables are a first-class cell, not a hack, because the pilot DSL needs them.

- Rows come from a child list; columns are fixed (declared) or taken from each row's
  roles.
- Column widths are computed per table and cached; a cell edit invalidates the column,
  not the whole model.
- The caret moves cell-wise with Tab and row-wise with Up/Down.
- A table with zero rows must render its header plus an "add row" affordance, so empty
  tables are always recoverable.

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

### Known hard parts

These are called out because they are the parts that get deferred and then hurt:

| Area | Requirement |
| --- | --- |
| **IME / dead keys** | implement `TextInputMethodClient`; composition text must be shown at the caret and committed key by key. Required for CJK and accented input. |
| **Accessibility** | custom `AutomationPeer` exposing the model as a text-like tree; screen readers need line/cell granularity. |
| **Clipboard** | two formats at once: structural (XML fragment, preserves node data) and plain text (the projection). Paste prefers structural, falls back to text. |
| **Bidirectional text** | out of scope before 1.0, but caret math must not assume left-to-right only. |
| **Fonts** | fallback chains per language for symbol keywords (e.g. arrows, `≤`). |

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
string ascii = LayoutSnapshot.Render(model, width: 80);
```

This one function carries most of the notation regression tests and is a hard
requirement for M1, not a nice-to-have.
