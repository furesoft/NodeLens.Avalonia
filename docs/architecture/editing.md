# Editing

Editing is the layer that turns a keystroke into a structural change. It reads the editor
tree produced by [projection](projection.md), decides which action a key means at the current
caret, applies it to the model, and asks the session to rebuild. It has no UI dependency: the
same code runs under the Avalonia control and under a test.

```text
key ──▶ KeyRouter ──▶ EditAction ──▶ EditContext ──▶ model transaction
                                          │                    │
                                          ▼                    ▼
                                    caret places        change events, undo
```

## The caret is a cell, not an address

The obvious representation of a caret is *address + offset into the cell's text*. It does
not work, and the reason is worth stating once so it is not rediscovered:

**A `CellAddress` does not identify a cell.** Only property, reference and child-element
cells get an address of their own (`OfSlot`, `OfElement`). Every constant keyword,
punctuation mark and space carries the address of the *node* it belongs to. The line

```text
rule Order as <none> {
```

therefore has seven cells sharing one address and one offset range, and `(address, 0)` is
ambiguous between `rule` and the opening brace of the next line.

`CaretPosition` carries a second, private field for this: the **document-order index** of
the cell among all caret places.

| Field | Meaning |
| --- | --- |
| `Address` | node + slot/element + index — survives rebuilds, re-layout and undo |
| `Offset` | position inside that cell's text; `0..Length` |
| `Order` | document-order index of the cell; `-1` means "named by address only" |

Resolution rules, implemented once in `CaretPlaces`:

- `Order >= 0` — take the closest place that has the same address. This is what keeps a
  caret on the brace instead of jumping to the keyword.
- `Order < 0` — take the first place with that address. This is how a fresh caret is
  created from the model side, where no cell is known yet.
- After every rebuild the session re-resolves the old position against the new place list,
  so a re-layout never moves the caret to a different cell.

`Order` is `internal`: it is an artefact of one layout generation, not part of a host's
mental model. A host addresses a caret by node, slot and offset — never by cell index.

## Which cells are caret stops

A caret stop (`CaretPlace`) is a cell the user can actually put the caret in:

- every **text cell** — property text, a reference's resolve info, a keyword or punctuation;
- an **empty child or collection slot**, because that is where a node gets created;
- **not** a container that already has content — its leaves are the stops, and offering the
  container as well would create a caret position that cannot be typed into;
- **not** an invisible structural node. A caret always corresponds to something the user
  can see ([open questions](../open-questions.md) #3).

The place list is built in document order by one walk, and the same walk feeds hit testing,
so what is clickable and what is reachable by keyboard cannot drift apart.

## Navigation

| Move | Behaviour |
| --- | --- |
| Left / Right | previous / next **cell** in document order, arriving at the end of the previous or the start of the next one; clamps at the ends instead of wrapping |
| Ctrl+Left / Ctrl+Right | one character, crossing into the neighbour at the edge of a cell — the character step the cell move takes away |
| Home / End | start / end of the cell's line, using `LineHeight / 2` as the tolerance so a caret that is vertically centred still counts as being on the line |
| Up / Down | nearest non-empty line in that direction, then the place with the smallest distance to the old column, ties going to document order |
| Tab / Shift+Tab | next / previous slot of the same node in declaration order, then out to the parent |

Horizontal motion is **by cell**, vertical motion is **geometric**, and the difference is the
point. A projectional editor shows a structure, so one press of an arrow walks one piece of the
notation — a keyword, a property, a space — the way the language spells it, and never stops in
the gap that alignment leaves between two cells. There is no comparable structure for "above",
because lines are a result of layout: a table row above another row is not a parent, and which
cell sits on the line above depends on widths. So horizontal movement follows document order and
vertical movement follows the page.

Two rules keep vertical movement from feeling arbitrary:

1. **Empty lines are skipped**, because there is nothing to stand in them.
2. **Column wins over order** — the target is the place closest to the caret's x position,
   which is what makes arrow-key movement predictable in a table or a ragged notation.

Because a property can be many characters long, the character-wise step stays reachable as
`CaretMove.LeftCharacter` / `RightCharacter`, bound to Ctrl+Left / Ctrl+Right. It is a separate
move rather than a modifier flag on the same one, so a host that wants different keys — or a
different definition of "cell" — only has to bind another key to it.

## The action model

Every input is an `EditAction`:

```csharp
public abstract class EditAction
{
    public abstract EditResult Apply(EditContext context);
}
```

`EditContext` is a view of one apply, not a service bag. It exposes the tree and layout the
caret was resolved against, plus exactly three mutations — resolve a node, set a property,
add a child — and every one of them goes through the model's own validated mutator. That is
why undo works for free: the change journal sees every edit because the model produced it,
and the editing layer never has to remember anything for undo.

| Action | Input | Effect |
| --- | --- | --- |
| `TypeTextAction` | printable character | insert into the property cell, or reject when the type refuses the value |
| `DeleteTextAction` | Backspace, Delete | shrink the cell's text, using `DeleteDirection` to say which side |
| `InsertNodeAction` | Enter | fill the empty slot the caret is on with a new child |
| `TabAction` | Tab, Shift+Tab | move the caret to the next / previous slot |
| `MoveCaretAction` | the arrow keys, Home, End | move the caret, never the model |

The result of an apply is an `EditResult`: an `EditOutcome` (`Changed`, `Unchanged`,
`Rejected`) plus the caret to place afterwards and the **coalescing key**. Two consecutive
edits with the same key belong to one undo step, which is how a typed word becomes one undo
instead of one per character. Returning `Unchanged` is not a failure — a key that means
nothing here must leave both the model and the undo stack alone.

The router is a table from `EditorKey` to action, not a `switch`. A host can bind a key
differently or add an action without the editing layer learning about it.

## The session

`EditorSession` is the public entry point and owns the cycle:

1. resolve the caret against the current places;
2. pick the action and apply it to the context;
3. if the model changed, commit one transaction;
4. rebuild tree and layout;
5. re-resolve the caret address against the new generation;
6. report the outcome and the coalescing key.

In M1, steps 4 and 5 rebuild everything after every change. That is deliberate: incremental
invalidation is a performance feature that needs a working correctness baseline to measure
against, and a stale editor tree is the hardest class of bug in this kind of editor.
[Roadmap](../roadmap.md) M7 is where it becomes incremental.

Undo and redo are the model's, not the session's: the session only supplies the coalescing
key. This is why a keystroke, an Enter and a future refactoring are undoable by the same
mechanism without the editing layer knowing about any of them.

## Representations

A concept may have more than one notation variant, so the session carries a
`NotationChoice` next to the model ([ADR 0012](../adr/0012-concept-rendered-more-than-one-way.md)).
The choice is view state and part of the session, not of the document: it is never
serialized, it does not enter the history, and two sessions over the same document may hold
different choices.

- `Alternatives()` walks the document and returns one entry per concept that actually occurs
  and resolves to more than one variant, with the variant currently in force. Walking the
  document rather than the registry is what keeps a host's menu honest: it offers a table
  form only for a concept somebody wrote.
- `ChooseVariant(conceptKey, variant)` refuses a concept the document does not use and a
  variant that does not apply to it, so a menu cannot put the projection into a state no
  notation can answer.
- `Choose(choice)` replaces the choice and rebuilds tree and layout immediately. The model is
  untouched, so no undo step is created, and the caret address is resolved against the new
  tree — a variant that hides the cell the caret was in moves the caret to the nearest
  surviving cell instead of losing it.

The constraining rule for language authors is that a variant keeps every shown slot's
natural address, so the tables, undo, selection and hit testing layers never learn that
variants exist.

## Selection

A selection is a pair of `CaretPosition`s plus a rule for resolving it to model elements.
The lifetime rule is the important one: a selection only exists as long as the nodes
involved do. If a selected node disappears, the selection shrinks to what is left — it never
silently moves to a different node.

In M1 a selection is structural only: the text actions edit one property cell at a time, and
a multi-cell selection degrades to caret behaviour. Text replacement across cells arrives
with the substitution actions, because that is the first feature that actually needs it.

## Error handling

Mutating a node can fail — a mandatory slot cannot be deleted, a value does not fit its
type, an index is out of range. Each of those is answered as `Rejected` or `Unchanged` and
is a normal outcome of pressing a key. `EditingException` is reserved for programming
errors, such as opening a session on a node that is not in a model.

## What M1 does not do

| Missing | Consequence | Arrives with |
| --- | --- | --- |
| Insert at index | Enter appends to the end of a list, not below the caret | M2 (tables need it) |
| Property defaults are materialized | typing into a default inserts into it (`"unnamed"` + `"P"` → `"Punnamed"`) instead of replacing it | `feat/property-defaults` after M1 |
| `Ctrl+Home` / `Ctrl+End` | not routed; document start and end are reached with Home/End on the first and last line | M3 |
| Completion and substitution | a key can only type, delete, move or create | M3 |
| Multi-cell text editing | a selection behaves like a caret for text input | M3 |
| Incremental rebuild | every keystroke rebuilds tree and layout | M7 |

The default-materialization item is a real defect rather than a scoping decision: a default
must eventually be distinguishable from a value the user typed. It needs `Node.Unset` and a
placeholder concept, which is a model change, so it does not belong in the editing layer.

## Testing

Editing is tested headlessly, without a window:

- drive the public session API with `KeyRouter` actions and assert on the model;
- assert *which* cell the caret is in, not just its address, whenever a document contains
  several cells with the same address — that is the regression the order hint exists for;
- assert that a rejected key leaves both the model and the undo stack unchanged.

See [testing.md](testing.md) for how that fits the overall test strategy.
