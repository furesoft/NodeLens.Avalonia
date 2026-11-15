# ADR 0013 — A finding is data, a marker is a drawing of it

- Status: Accepted
- Date: 2026-11-15
- Related: [0002](0002-model-as-source-of-truth.md), [0009](0009-custom-drawn-editor-control.md),
  [0010](0010-public-api-surface.md), [semantics](../architecture/semantics.md),
  [markers](../architecture/markers.md)

## Context

The editor has to show that something is wrong with a node — a name that is not unique, a
reference that resolves to nothing, a value outside its range. `docs/architecture/markers.md`
planned this as an "underline of arbitrary style" and left the vocabulary, the plumbing and the
geometry open.

Three questions have to be settled before any code is written, because each of them is
expensive to change afterwards:

1. **Where does the vocabulary live?** The check that produces a finding needs to say what it
   found without knowing anything about drawing. The thing that draws needs the finding without
   knowing anything about the check.
2. **Does the frame carry the findings?** `EditorFrame` is the immutable snapshot a paint pass
   draws from, so it is the obvious place to put marker geometry — except that `Projection` sits
   *below* `Editing` and `Semantics` and must not reference them ([ADR 0010](0010-public-api-surface.md)).
3. **Is a finding model state or view state?** A model is persisted byte-stably
   ([ADR 0005](0005-xml-persistence.md)) and every mutation is an undo step
   ([ADR 0006](0006-change-event-backbone.md)). Findings are neither obvious nor cheap to store.

## Decision

**A diagnostic is data about a model; a marker is a decoration computed from it at paint time.
The two never meet in the model, and they never meet in the frame either.**

### The vocabulary belongs to `Projectional.Semantics`

`DiagnosticSeverity`, `CharacterRange` and `Diagnostic` live in
`Projectional.Semantics/Diagnostics/`. That project references only `Core` and `Language`, so a
language's own checks and a host's checks can produce findings without either of them
referencing the projection, the editing layer or a UI toolkit — and the constraint runner of
M4, which is the main producer, already lives there.

`Diagnostic` names a node, a slot and an index, plus an optional `CharacterRange` inside that
one value. It names no cell, no line and no screen position: cells and positions are derived, so
a diagnostic that mentioned one would go stale the moment a notation changed, and a check has to
be able to run on a model nobody is drawing.

Severity is `Hint`, `Info`, `Warning`, `Error` **in that order**, and the order is the contract:
"worst wins" is a comparison on the enum, not a table somewhere. There is no `None`, because a
finding that does not matter is not a finding and belongs nowhere in the set.

### A marker set is an immutable batch, in `Projectional.Editing`

`MarkerSet` collects a batch of diagnostics and answers "what is wrong with this node" from a
lookup built in its constructor, because the drawing pass asks that question per cell and a scan
per cell would be quadratic.

It is **replaced, never appended to**. A finding describes a state of the model, so after an
edit the previous findings are about text that is no longer there; a host recomputes and
publishes a whole new set. That is also what makes it safe to hand a set to a control while a
background thread computes the next one.

`MarkerSet` is **view state**: never part of the model, never persisted, never an undo step,
exactly like the choice of notation variant ([ADR 0012](0012-concept-rendered-more-than-one-way.md))
and the wrap width. Two sessions can draw the same document with different findings.

`MarkerSet` is public because a host is the one that runs the checks and assigns the result;
`MarkerCells` is internal because resolving a diagnostic to cells is the editor's business.

### The frame does not carry markers

`EditorFrame` stays as it is. `Projection` cannot reference `Editing` or `Semantics`, so a frame
that carried markers would drag both up the dependency arrows. The geometry is instead an
`Editing` type computed **at paint time** from the current layout, which the control already
holds. The plan in `markers.md` that gave `EditorFrame.Build` an eighth parameter and a
`Markers` property is superseded by this paragraph.

That costs one recomputation per paint and buys three things: assigning a marker set needs no
rebuild of the tree, the layout or the caret (it is decoration, so the control only repaints),
and a finding whose node has since been deleted resolves to nothing instead of to a stale
rectangle.

### Which cells a diagnostic marks

| Diagnostic | Marks |
| --- | --- |
| `Role` null | the node's own visible cells — its keywords, values and punctuation — and nothing below them |
| `Role` set, `Range` null | that slot's cell and the visible text below it |
| `Role` set, `Range` set | that slot's cell alone |
| `Role` and `Index` set | that one element's cell and the visible text below it |
| `Role` that resolves to nothing | falls back to the node rule |
| a node the layout does not contain | nothing |

Two of these are worth stating as rules rather than as cases:

- **A finding about a node stops at its own slots.** Marking a parent's children from the parent
  would make one finding look like a finding on everything below it, and would double-cover the
  line whenever a child has a finding of its own.
- **A finding is never invisible.** A node whose notation draws only a child slot draws no text
  of its own once the child is filled, so the node rule falls back to the node's outermost cell.
  A container still gets a line's worth of marker instead of nothing at all.

A node that is not in the layout marks nothing. The set may have been computed before the edit
that removed the node, and dropping the marker is the only honest answer — inventing a cell would
put the mark somewhere it does not belong.

## Consequences

- A check is testable with no window: build a model, build a `MarkerSet`, ask `MarkerCells` which
  cells it means. `MarkerCellsTests` does exactly that.
- `Projectional.Editing` gains a `ProjectReference` to `Projectional.Semantics` and an
  `InternalsVisibleTo` for the test project. The arrow
  `Core -> Language -> Projection -> Editing` is unchanged; `Semantics` is a sibling that hangs
  off `Language`, and nothing above `Editing` is involved.
- Marker geometry will be recomputed on every paint. If a profile ever shows that matters, the
  place to cache it is the control, keyed on the layout and the set — not the frame.
- A host that runs no checks assigns nothing and pays nothing: the default set is empty, and
  `SeverityOf` answers `null` for every node.
- Fixes, quick-fix lists and severity thresholds are deliberately absent. They belong to whatever
  runs the checks, and adding them here would make the editor decide policy.
