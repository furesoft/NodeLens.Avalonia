# Collapsing nodes

A body of five rules and eight steps fills the screen with detail the reader did not ask for.
Every projectional editor answers that with folding: the node stays, its inside is replaced by
one line that says what is inside, and the gutter carries the arrow that opens it again. MPS does
this from the cell that draws the node; we do it from the model, because in this editor the model,
not the drawing, says what a node *is*.

This document plans that feature. It is short on new machinery on purpose: the layout already
gives a node one address for its whole subtree, the caret already knows what to do when the cell it
stood in disappears, and the gutter already exists because line numbers needed one.

Status: **delivered**, slices S1 to S6. See [roadmap.md](../roadmap.md).

## What already exists

Almost everything folding needs was built for something else, and each piece is used here
unchanged.

| Piece | Where | What it already gives folding |
| --- | --- | --- |
| A node address covers the whole subtree | [projection.md](projection.md) | `new CellAddress(nodeId)` already names "this node", regardless of which cells its notation produced |
| The caret survives a disappearing cell | [editing.md](editing.md) §Navigation | `CaretNavigation.Nearest` falls back to the first place at or after the lost one, and reads a node address as "start here" |
| A cell that only shows text | `Projectional.Projection/Tree/TextCell.cs` | Layout, snapshot, caret places and hit test all accept one without a new case |
| A cell that stands in for something | `MissingNotationCell` | The precedent that a stand-in is a marker, not an error |
| A view-state set on the session | [ADR 0012](../adr/0012-concept-rendered-more-than-one-way.md), `EditorSession.Markers` | The pattern: never persisted, never an undo step, rebuilt on demand |
| A gutter outside the scroll transform | [line-numbers.md](line-numbers.md) | `GutterWidth`, `PaintGutter`, `CaretAt` returning `null` inside the chrome |
| "A finding is never invisible" | [markers.md](markers.md) | The doctrine that forces the one rule folding has to *add* rather than reuse |

## Decision: the permission is model data, the fold is view state

Two facts, two owners. Conflating them is the mistake this section exists to prevent.

**Whether a node may be folded is a property of the model.** It is declared by the language and
stored per node, so it survives a save, an undo and a rebuild, and a reader who opens the file
tomorrow finds the same permission. It is answered by the model, exactly like a node's name.

**Whether a node *is* folded right now is view state.** It says nothing about the document, two
readers may disagree, and it must not be written to disk. It lives on `EditorSession` next to
`NotationChoice`, keyed by `NodeId` - a stable GUID, so the set survives the rebuild that every
keystroke triggers, and an undo that moves nodes around.

Three alternatives were considered and are recorded so they are not re-derived:

| Alternative | Cost |
| --- | --- |
| A flag on the concept declaration, like `IsOfferedInCompletion` | It answers a question about a *concept* ("is a literal worth offering"), which is right for completion and wrong here: two nodes of one concept differ. One rule's body is three lines and worth hiding; its sibling's is one line. A concept-level flag also inherits into every subconcept, which blurs exactly what the property keeps sharp. |
| A flag in the notation | Same per-concept problem, plus it puts a model fact into `Projectional.Language`, where a second variant of the same concept could disagree with the first. |
| A language-level resolver, `ICollapsible`, like `ICellEmphasis` | No `Core` change and it composes with the property later, but the user asked for "a property set on the node", and a resolver answers per rebuild with no stable place in the file: nothing to save, nothing to inspect, nothing a second host can read back. |

The resolver is the cheapest one to add *later*, because the question "is this node foldable" is
one static method (`Foldable.Of`). Adding a second source means editing that method, not the
builder, the session or the control.

## The permission: a designated slot, like the name slot

Folding opt-in copies the name-slot mechanism exactly, because it is the same problem: a concept
nominates one of its own properties as having a framework-wide meaning.

| Member | What it says |
| --- | --- |
| `ConceptDeclaration(..., collapseSlot: string?)` | the language's answer to "may a node of this concept be folded", by slot name. It goes **last** in the parameter list, after `isOfferedInCompletion`; all 97 call sites pass only `key` positionally, so appending breaks none of them |
| `Concept.CollapseSlot` (`PropertySlot?`) | the resolved slot, `null` when the concept does not opt in |
| `Foldable.Of(Node)` | true when the concept nominates a slot and that node's value is `true` |

The rules are the name slot's rules, restated in [language.md](language.md):

- The named slot must be declared, and it must be a **`bool` property**. A name slot may not be a
  string, so a collapse slot has to be a boolean; both are rejected with
  `ConceptDefinitionException` at registration, where the language author still sees the message.
- **The designation inherits.** `declaration.CollapseSlot ?? first super concept that has one`, so
  a concept that is foldable stays foldable and a language may re-declare it to override. This is
  the same walk that makes a subconcept inherit its super concept's name slot.
- **The value is tested, never the presence.** Property defaults are materialised - `name` is
  `"unnamed"` and a `bool` default may be `true` - so the model cannot tell "unset" from "set".
  `Foldable.Of` therefore asks `node.GetOrNull<bool>(slot) is true` and treats everything else as
  "not folded". A slot declared with a default of `true` consequently means "every node of this
  concept folds unless it opts out", which is a legitimate language design and needs no code.
- **Removing the designation later is safe.** A model that stored values in a slot its language no
  longer nominates keeps the values as ordinary properties; the nodes simply stop folding. No
  migration, no diagnostic, because nothing about the *document* changed.

A language that never nominates a slot behaves exactly as today: no cell changes, no gutter column,
no mark. That is why this feature can land without touching a single existing test expectation.

## What a folded node draws

One new cell, and it is a text cell.

| Member | What it holds |
| --- | --- |
| `CollapsedCell : TextCell` | the node's own address, the summary string, `TextEmphasis.None` |
| `CollapsedSummary.Of(Node)` | the string, computed while the tree is built |
| `CellStyler` | `CollapsedCell` maps to `TextStyleKind.Placeholder` |

Deriving from `TextCell` is the whole trick. `CellLayouter` arranges any `TextCell` with the text
size branch it already has, `CellText.Of` reads `Text`, the snapshot renderer draws it, the caret
walk offers it as one place, and the hit test finds it - none of those files change. `CellStyler`
needs one line because an unknown cell currently falls through to `Default`; `Placeholder` is the
honest kind (the summary is not a value, it is a statement that something is missing from view),
and every existing `SyntaxPalette` already has a brush for it.

**The address is the node's own**, `new CellAddress(node.Id)` - not a synthetic one, not the slot's.
That single choice is why the rest of the editor needs no knowledge of folding:

- Pressing inside a folded node resolves to the node, so Delete removes the node and its whole
  subtree, Alt+Up/Alt+Down moves it, and a click selects it. Those actions ask
  `context.NodeOf(place.Address)`; the placeholder *is* that node.
- Typing is `Rejected`, because a placeholder is not a `PropertyCell`. A folded region cannot be
  edited, which is the point of folding it.
- Layout has one row where there were `n`, so `LineNumbers.TopsOf` counts one and every number
  below renumbers itself.

**The summary** follows the reference cell's rule, then marks the omission:

1. the value of the concept's name slot, when the concept declares one;
2. otherwise - and also when that value is empty or whitespace - `<Concept.Key>`;
3. then a space and `…`.

So a folded rule reads `check-age …` and a nameless expression reads `Expressions.Binary …`. The
summary is never the empty string: layout measures empty text as one space, and a zero-width cell
would make the caret stop invisible.

**Where it is substituted** is `EditorTreeBuilder.BuildNode`, after the notation lookup has
succeeded and before the notation is walked:

```
if (notation resolves) {
    if (collapsed?.Contains(node.Id) is true && Foldable.Of(node))
        return new CollapsedCell(node.Id, CollapsedSummary.Of(node));
    ...
}
```

Placing it after the lookup is deliberate and free - the null check is already there - and it keeps
a concept without notation showing its `<no notation for ...>` marker. A language fault must never
be tucked behind a fold.

The builder takes `IReadOnlySet<NodeId>?`, the BCL interface, so `Projectional.Projection` needs no
new type; the session's `CollapsedNodeSet` implements it.

**Nesting needs no bookkeeping.** Folding a node hides its whole subtree, including descendants
that are themselves in the set. Unfolding the outer node brings them back still folded, because
their ids never left the set. There is no cascade and no counter.

## What folding does not disturb

Each row is a piece of the editor that could have needed a folding case and does not.

| Concern | Why it needs nothing |
| --- | --- |
| Caret | `CaretNavigation.Nearest` already defines both directions: a lost cell falls back to the first place at or after it (the placeholder), and a node address reads as "start here" (the first place inside). Folding under the caret and unfolding from the placeholder both land somewhere sensible. |
| Selection, delete, move | They resolve the address to a node, and the address is the node's. |
| Typing and completion | The placeholder is not a property cell, so both decline: typing answers `Unchanged`, the same outcome it already gives for a keyword. |
| Line numbers | Counted from the layout, so they renumber themselves. |
| Undo | The fold set is view state, so folding creates no step and undoing an edit keeps the folds. |
| Snapshot tests | A folded node renders as one line of text through the existing renderer. |
| A second representation | A variant that draws the node differently folds the same way: the permission is on the node, not on the notation. |

## The fold set on the session

`CollapsedNodeSet` is an immutable, value-like set of `NodeId` in a new `Projectional.Editing/Folding/`
folder, built the way `NotationChoice` is: a `FrozenSet` behind factories, never mutated in place.

| Member | What it answers |
| --- | --- |
| `CollapsedNodes.Empty` | nothing folded |
| `Contains(NodeId)` | is this node folded |
| `With(NodeId)` / `Without(NodeId)` / `Toggle(NodeId)` | the set with one change, a new instance |
| `Clear()` | `Empty`, spelled so a host can say "open everything" |
| `IsEmpty` / `Count` | the two questions a menu asks |
| `IReadOnlySet<NodeId>` | the interface the builder accepts |

On the session:

| Member | Behaviour |
| --- | --- |
| `Collapsed` (`get`/`set`) | assigning replaces the set, rebuilds the tree and re-settles the caret - unlike `Markers`, which only decorates |
| `ToggleCollapsed(NodeId)` | the one gesture a gutter click needs |

Three rules worth freezing:

- **One builder construction path.** The builder is built from *two* view-state values now, `Choice`
  and `Collapsed`, so `Choose` and the `Collapsed` setter both have to pass both. Put the
  construction in one private `RebuildBuilder()` and call it from both; the drift trap is exactly a
  `Choose` that later forgets the folds.
- **The set is called `CollapsedNodeSet`, not `CollapsedNodes`.** It implements `IReadOnlySet<NodeId>`, and the BCL rule that such a type ends in `Set` exists so a reader can tell from the name what question it answers. `Clear()` is not needed: `CollapsedNodeSet.Empty` says the same thing.

- **`ToggleCollapsed` does not validate the node.** `ChooseVariant` throws on a bad argument
  because a menu entry that cannot apply is a programming error. Here a stale id is harmless: the
  builder ignores any id it cannot fold, so the worst case is an entry that draws nothing, and a
  throw would turn a paint-time race - the mark was computed before the model changed - into a
  crash.
- **Folding is not undoable, and that is intended.** An undo that silently reopened a fold, or a
  fold that consumed an undo step, would each be a surprise. Two sessions over one document may
  hold different sets.

## The gutter column

[line-numbers.md](line-numbers.md) froze two rules that this section extends rather than breaks: the
gutter sits outside the scroll transform, and no press inside it moves the caret.

### Geometry and order

The twisty column is the **leftmost** gutter column, so `[twisty][numbers][content]` or, with
numbers off, `[twisty][content]`. Reasons: a node's first row is where its mark goes, nodes are
nested and indented, and a fixed left column keeps every mark at the same x - one column of arrows
rather than a diagonal staircase of click targets. It also leaves the number column byte-identical
to today when the twisty column is absent.

`GutterWidth` grows from one sum to three, with `advance = TextMeasure.Measure("0", TextEmphasis.None)`:

```
GutterWidth = (HasFoldMarks ? advance : 0)
            + (ShowLineNumbers ? LineNumbers.DigitsOf(Rows) * advance + 2 * GutterGap : 0)
            + (HasFoldMarks && !ShowLineNumbers ? 2 * GutterGap : 0)
            + (ShowBulb ? Round(LineHeight) + BulbGap : 0)
            + PageMargin
```

The property to preserve is that with no foldable node in the document - every language today -
this is arithmetically the current formula. A host that turns line numbers off and has a folding
language still gets a gutter, which it did not before.

`ShowFoldTwisties` (`bool`, default `true`) lets a host turn the column off while keeping folding
reachable through the menu. Defaulting it to `true` is safe precisely because the column is
zero-width unless a language opts in.

Members that must account for the gutter, extending the table in
[line-numbers.md](line-numbers.md) §The gutter sits outside the scroll transform:

| Member | What changes |
| --- | --- |
| `GutterWidth` | the three-part sum above |
| `WrapWidth` | unchanged in form - `availableSize.Width - GutterWidth` already follows |
| `PaintGutter` | draws the twisty column first, then the numbers, i.e. left to right |
| `CaretAt` | still `null` for `point.X < GutterWidth`, but only *after* the fold hit test declined |
| `FoldHitTest` | the only new reader of gutter geometry |

### The glyph

The twisty is painted as **text**, not geometry: `CollapsedGlyph` `"+"`, `ExpandedGlyph` `"-"`, in
`LineNumberBrush`, shaped through `AvaloniaTextMeasure` like every other string. A chevron drawn
with a `StreamGeometry` would be the first non-rectangle in this control's chrome, and a `▸` glyph is
not present in every monospace face. Two ASCII characters are the safe pair; a host that wants
chevrons sets the two properties.

Placing the glyph inside the row means the row's vertical centre comes from the mark's own bounds,
never from the number of the line - the two columns stay independent.

### The press path

`OnPointerPressed` asks `FoldHitTest` before it asks `CaretAt`:

- a hit toggles and sets `Handled`;
- a miss falls through, so `CaretAt` still returns `null` and a gutter press is still "nothing".

Hovering a twisty sets `Cursor = Hand` and clears it on the way out, reusing the hover handling
that marker tooltips already need.

### The marks

Three new types in the same `Projectional.Editing/Folding/` folder, imitating `LineNumbers` and
`MarkerHitTest` rather than living next to them:

| Member | What it answers |
| --- | --- |
| `FoldMark(NodeId Node, Rect Bounds, bool IsCollapsed)` | where one arrow goes and which way it points |
| `FoldMarks.Of(session, layout, measure)` | the marks in visual order |
| `FoldHitTest.At(marks, left, width, point)` | the mark under a point in the twisty column |

`FoldMarks.Of` is **two linear passes**: one document-order walk collecting the `NodeId`s that are
foldable, then one pass over `layout.Cells` keeping the first placed cell whose address is a node
address in that set. No `BoundsOf` per node - that nested lookup is the quadratic shape
[line-numbers.md](line-numbers.md) §Counted from the whole layout, not from the viewport already
avoids.

A mark is emitted when a node is foldable **and** folding it would hide something
(`Bounds.Height > one line`) **or** the node is already in the set. The second half matters: a node
that a later edit shrank to one line would otherwise fold invisibly and lose its arrow, leaving no
way back except the menu.

## A finding inside a folded node

This is the one place where folding has to add a rule rather than reuse one.
[markers.md](markers.md) states the doctrine plainly: a finding is never invisible. But
`MarkerCells` answers with the target's own cells, falling back to its first own cell, and resolves
to **no cells** when the target is absent from the layout - which is exactly what a hidden
descendant becomes.

The rule: when a target's cells are absent from the layout, `MarkerCells` climbs the parent chain
and uses the nearest ancestor that *is* in the layout. A diagnostic on a step inside a folded rule
therefore marks the folded rule's own row.

Consequences worth stating rather than discovering:

- several findings inside one folded node land on the same row, and `MarkerRuns` already merges
  adjacent runs per row, so the row shows one squiggle;
- the tooltip shows the first of those findings in document order, so the problems panel stays the
  complete list - which it already is, because it reads the model, not the layout.

## Surface

What a host needs, and where it comes from. Everything is reachable through
`Projectional.Avalonia`, `EditorSession` or `EditorMenu`; nothing requires internals.

| Member | Why a host needs it |
| --- | --- |
| `ProjectionalEditorControl.ShowFoldTwisties` | turn the column off in a dense panel |
| `ProjectionalEditorControl.CollapsedGlyph` / `ExpandedGlyph` | match the host's own look |
| `ProjectionalEditorControl.Collapsed` | read and set the set, forwarded to the session like `Markers` |
| `CollapsedNodeSet` (public in `Projectional.Editing`) | the value a host stores if it wants to restore a set |
| one `EditorMenuItem.Option` entry in `EditorMenu.For` | fold and unfold from the keyboard or a menu, with no geometry knowledge |

The menu entry is a single checkable `Option("Collapsed", isChecked, command)` shown when the caret
stands on a foldable node: `IsChecked` reads the set, `Execute` toggles it. A host that cannot host
a gutter - an embedded editor in a table cell, a reader view - can still fold.

## What this plan does not do

| Not doing | Why |
| --- | --- |
| Folding a slot on its own (the body but not the condition) | That is view state keyed by `(node, slot)`, and the permission would have to be declared per slot. Folding whole nodes is the smaller step and the one MPS users reach for first. Open question 57. |
| A row count in the summary (`… 12 lines`) | The cell's text is fixed when the tree is built, which is before layout runs, and a count is only known after. Making the text depend on a second pass breaks the invariant that every box was measured from the text inside it - the same invariant that keeps emphasis on the cell rather than in the painter. |
| A hover preview of the hidden content | It projects the same node a second time inside a popup; that is the preview idea from [markers.md](markers.md), not a fold mechanic. Open question 54. |
| Persisting the folds with the document | View state stays session-local, per [ADR 0013](../adr/0013-markers-are-view-state.md)'s precedent. Persisting it needs a format decision and a rule for who owns the set. Open question 56. |
| A keyboard chord for fold/unfold | `EditorKeyMap` gives the host every Control and Alt combination but two, and picking a fold chord takes one from a host without it being asked for. The menu entry covers the keyboard case meanwhile. |
| Fold-all on open | A policy for a shell, not a mechanic for a component, and `Collapsed` already lets a host fold whatever it wants. |
| Double-click or ellipsis-click to unfold | One gesture, in the gutter. A click on the placeholder has to stay a caret placement, or the caret could not enter a folded node at all. |
| Animating the fold | The layout is rebuilt on every view-state change; an animation would need a stable geometry across two generations. |

## A defect the probe found

The runtime probe rendered a folded rules document and showed the fold working - one line reading
`Approved …` where the body used to be - but **no arrow in the margin**, and a gutter exactly as wide as
before folding.

The cause is an assumption in `FoldMarks.Of`: it looks for a placed cell whose address is the node's own
address (`new CellAddress(nodeId)`, slot null). That is true only for the **document root**. Every other
node is projected under its parent's *element* address - `CellAddress.OfElement(parentId, slot, index)` -
because the address names where the cell sits in the parent, not which node it draws. So the owner of a
child's cell is its parent, the `Slot` is not null, and the filter rejects every candidate.

The fix has two parts and is the next slice (S7):

- **Resolve a node's address through the model**: `new CellAddress(node.Id)` for the root, and
  `CellAddress.OfElement(parent.Id, slotName, index)` otherwise, found from the parent's child slot.
- **Look it up in one pass, not per node**: build an `address -> bounds` map from `layout.Cells` once,
  then read each foldable node's entry. Asking `BoundsOf` per node instead would reintroduce the nested
  scan this document warns about.

`FoldHitTest`, `GutterWidth`, the glyph painting and the press path are all correct as written; only the
mark list is empty, which is why the feature looks like it does nothing.

## Slices

- **S1 - the permission.** `ConceptDeclaration(collapseSlot:)`, `Concept.CollapseSlot`, the
  registry resolution with the name slot's validation and inheritance walk, and `Foldable` in
  `Projectional.Projection/Tree/`. Tests: a `bool` slot is accepted, a `string` slot and an unknown
  slot name are rejected with `ConceptDefinitionException`, a subconcept inherits the designation,
  and a node whose value is `false` is not foldable. Nothing reads the flag yet, so no existing
  test can move.
- **S2 - the cell.** `CollapsedSummary`, `CollapsedCell`, one line in `CellStyler`, and
  `EditorTreeBuilder` accepting `IReadOnlySet<NodeId>?`, substituting after the notation lookup.
  Snapshot tests: a folded node is one line, a folded outer node hides a folded inner one, and a
  node without permission is untouched.
- **S3 - the session.** `CollapsedNodeSet`, `EditorSession.Collapsed`, `ToggleCollapsed`, and the
  single `RebuildBuilder()` that both `Choose` and the setter call. Session tests: folding under the
  caret lands on the placeholder, unfolding lands inside the node, no undo step is created, and the
  layout shrinks.
- **S4 - the gutter.** `FoldMark`, `FoldMarks`, `FoldHitTest`, `ShowFoldTwisties`, the two glyphs,
  the `GutterWidth` composition, the `PaintGutter` order, the press path ahead of `CaretAt`, and the
  `Hand` cursor. [line-numbers.md](line-numbers.md) gains the twisty clause and the new formula.
- **S5 - the menu.** One checkable `EditorMenuItem.Option` in `EditorMenu.For`, enabled from the
  caret's node. Purely additive, and it is what makes folding reachable without a gutter.
- **S6 - the roll-up.** `MarkerCells` climbs the parent chain for a target absent from the layout,
  plus a test that a diagnostic on a hidden descendant marks the folded ancestor's row.

## Testing

| What | How |
| --- | --- |
| The designation, its inheritance and its two rejections | Unit tests on `ConceptDeclaration`/`ConceptRegistry` in `Projectional.Core.Tests`, mirroring the name-slot tests |
| `Foldable` reads the value, not the presence | A node with the slot defaulted to `true` and one set to `false` |
| A folded node is one line with the node's address | Snapshot and layout tests: `layout.Find(new CellAddress(id))` is the `CollapsedCell`, and `BoundsOf` gives one row |
| The caret survives both directions | `EditorSession` tests through `Apply`: `CaretNavigation.PlaceAt(...).Value.Cell` is the placeholder, then a cell inside the node |
| Folding is not an edit | `History.UndoDepth` and `Model.Version` unchanged by `ToggleCollapsed` |
| Line numbers renumber around a fold | `LineNumbers.Of` over a folded layout |
| One mark per foldable node, in visual order | `FoldMarks.Of` over a hand-built layout; count, order and `IsCollapsed` |
| A node folded to nothing keeps its arrow | A one-line foldable node that is in the set still yields a mark |
| A twisty press toggles and a number press does not | `FoldHitTest.At` and `CaretAt` called directly - pointer events cannot be constructed in a test |
| The columns do not overlap | Probe: rasterise a folding document with line numbers on and off, sample both columns |
| A hidden finding marks the folded row | `MarkerCells` test with a diagnostic on a grandchild |
| The gutter stays zero for a language that does not opt in | `FoldMarks.Of` empty, plus the existing line-number expectations unchanged |

## Open questions

1. **Chevron or plus/minus?** Recommend: `+`/`-` for M1, since only a monospace face guarantees a
   symbol glyph keeps one advance, and the two properties let a host choose.
2. **Does the fold set survive a reload?** Recommend: no. It survives rebuilds and undo because
   `NodeId` is stable, but a reload builds a new session, and a restored fold set belongs with
   project settings, which is question 56.
3. **Is the menu entry shown for a node that folds to nothing?** Recommend: yes whenever the
   permission is set. The gutter does not offer an arrow there, so the menu is the only way to
   reach a stale fold.
4. **Which finding does the tooltip show when several collapse onto one row?** Recommend: the first
   in document order, with the problems panel as the complete list.
5. **Per tab or per document?** Recommend: per session, like `NotationChoice`, including its
   consequence that closing and reopening a document starts unfolded.

The framework-wide questions this plan raises are 53-57 in
[open-questions.md](../open-questions.md). Question 3 there - whether the caret may sit inside a
collapsed node - is answered by the design above: it may not, because the placeholder is a visible
leaf and the caret attaches to it.
