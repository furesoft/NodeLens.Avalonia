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
| Up / Down | nearest **line** in that direction — a line of a multi-line value before the neighbouring cell — then the place with the smallest distance to the old column, ties going to document order |
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

## The caret

The caret is a position between two characters, so it has no width in the layout and the
renderer gives it one (`ProjectionalEditorControl.CaretThickness`). Two rules decide how it
behaves rather than where it is:

- **It blinks inside a property cell and nowhere else.** A caret standing on a keyword has
  nowhere to type, and a blinking bar there would promise an insertion point that does not
  exist. `CaretBlink.Blinks` asks the place list, so the rule cannot drift from what the
  notation actually projects.
- **It holds still while the editor does not have the focus.** A blinking caret in a window
  the user is not working in is noise, and it costs a repaint every half second for nothing.
  A steady caret is still visible, so the position is never lost.

The phase is state in the editing layer (`CaretBlink`), not a trick in the control: the control
owning a `DispatcherTimer` that calls `Advance` once per tick and repaints only when it reports
a change, and every edit, move and click calls `Reset` so the caret is never invisible at the
moment the user types. Keeping the decision out of the control is what lets a test say "the
caret is off at this moment" without a window, and what lets a host with its own renderer blink
at the same speed — `CaretBlink.Interval` is public for that reason.

## The action model

Every input is an `EditAction`:

```csharp
public abstract class EditAction
{
    public abstract EditResult Apply(EditContext context);
}
```

`EditContext` is a view of one apply, not a service bag. It exposes the tree and layout the
caret was resolved against, plus exactly four mutations — resolve a node, set a property, add
a child, move a child — and every one of them goes through the model's own validated mutator.
That is why undo works for free: the change journal sees every edit because the model
produced it, and the editing layer never has to remember anything for undo.

| Action | Input | Effect |
| --- | --- | --- |
| `TypeTextAction` | printable character | insert into the property cell, or reject when the type refuses the value |
| `DeleteTextAction` | Backspace, Delete | shrink the cell's text, using `DeleteDirection` to say which side |
| `InsertNodeAction` | Enter, outside a value of text | fill the empty slot the caret is on with a new child |
| `InsertLineBreakAction` | Enter, inside a value of text | open a line at the caret, which is a `TypeTextAction` of `"\n"` |
| `TabAction` | Tab, Shift+Tab | move the caret to the next / previous slot |
| `MoveCaretAction` | the arrow keys, Home, End | move the caret, never the model |
| `MoveNodeAction` | Alt+Up, Alt+Down | reorder the node the caret is in among its siblings |
| `OpenCompletionAction` | Ctrl+Space | offer the concepts that could fill the empty slot at the caret |
| `NarrowCompletionAction` / `WidenCompletionAction` | a character / Backspace, while a list is open | filter the open list, never the model |
| `SelectCompletionAction` | Up, Down | move the highlight in the open list |
| `AcceptCompletionAction` | Enter, Tab, while a list is open | create the highlighted concept in the slot |
| `DismissCompletionAction` | Escape | close the list and leave the model alone |
| `SelectAllAction` | Ctrl+A | widen the selection: the property, then the node, then the document |
| `CopyAction` | Ctrl+C | put the selected text on the clipboard |
| `CutAction` | Ctrl+X | copy the selected text, then delete it |
| `PasteAction` | Ctrl+V | type the clipboard text at the caret |

The result of an apply is an `EditResult`: an `EditOutcome` (`Changed`, `Unchanged`,
`Rejected`) plus the caret to place afterwards and the **coalescing key**. Two consecutive
edits with the same key belong to one undo step, which is how a typed word becomes one undo
instead of one per character. Returning `Unchanged` is not a failure — a key that means
nothing here must leave both the model and the undo stack alone.

Two of an action's members describe the moment *after* it ran rather than the model.
`CoalescingKey` says which burst a commit belongs to. `SelectsLandedValue` says that what the
action produced is something to be named, so the session widens the caret it landed on to the
whole value found there: a rule that Enter creates is already called `"unnamed"`, and a caret in
front of that name would turn the first keystroke into `"Punnamed"`. The action states the
intent, the session carries it out against the tree the edit produced — the action does not have
to predict which cell the coercion will choose — and only the creating edit sets it, because if
typing re-selected what it had just written the next keystroke would replace it and no name
longer than one character could be typed.

The router is a table from `EditorKey` to action, not a `switch`. A host can bind a key
differently or add an action without the editing layer learning about it.

Enter is the one key the table cannot decide alone, because it means two things: it fills an
empty slot with a node, and inside a value of text it opens a line — a break is text, and only
a value whose type can hold a break can take one. `KeyRouter.For(session, key)` therefore asks
the caret first and falls back to the plain table; the plain `For(key)` stays for the host that
has no document to ask. The test for "text" is the property type, not `CaretPlace.IsEditable`,
because a number and a date are editable and refuse a break.

An open completion list adds a third answer to Enter and Tab, and it is asked first: while a
list is open those two keys accept a proposal rather than fill a slot or open a line. The same
`For(session, key)` overload carries that decision, so a host that routes keys through the
session gets it and a host that uses the plain table gets no list at all. Both cases are what
[completion.md](completion.md) describes.

## Moving nodes

Reordering is the first edit that changes neither text nor a value but the *order* of the
model, and it is the reason `EditContext` grew a fourth mutation. `MoveNodeAction` takes the
node the caret is in — resolved by address, like every action — and asks the context to put it
one place earlier or later among the children of its slot:

```csharp
context.MoveChild(parent, slot, node, target);
```

`MoveChild` calls `InsertChild`, and `InsertChild` detaches a node that is already attached
before inserting it. That is the whole identity story: the moved node keeps its id, so every
reference into its subtree keeps resolving and every caret address that names one of its cells
still names a cell. It is also why a move is one undo step — the model journals `NodeRemoved`
and `NodeAdded` inside one mutation, so the journal sees one change event — and why the caret
does not have to be corrected afterwards: `CellAddress` carries a `NodeId`, so a caret in the
moved node's `name` is in that same cell after the move, one line further up.

`RemoveChild` is the wrong tool and is not used: it deletes the subtree. A move with no sibling
to trade places with returns `Unchanged`, the same answer `TabAction` gives at the ends, so a
control can treat "nothing happened" as "the key is free".

The gesture is Alt+Up / Alt+Down. [Node moves](node-moves.md) states why that Alt combination
is taken while the rest stay with the host, and what the remaining slices are — moving across
slots, dragging with the mouse, moving a range of siblings.

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

## Completion

An open completion list is view state, exactly like the representation choice: it lives on
`EditorSession.Completion`, is never serialized, creates no undo step and dies with the caret
that justified it. `EditContext.Completion` is the handle an action edits it through, and the
session closes it after **any** action that is not a completion action — which is why no edit
has to remember to dismiss a list.

The proposals are the model's own possibilities, not a vocabulary guessed from text: the caret
is in an empty slot, the slot declares the concept its children must be, and every non-abstract
concept below it that has not opted out is an entry, in declaration order. That order is the
language author's ranking. `ConceptDeclaration(..., isOfferedInCompletion: false)` removes a
concept from the list without removing it from the language, so the top entry and the concept
Enter creates are allowed to disagree: the literals are absent from the menu and still what a
plain Enter fills an expression slot with. Hiding a concept from the list must therefore never be
implemented by making it illegal or by teaching `DefaultInstantiableFor` about the flag.

The full contract — the gestures, why text typed into a list is a filter rather than an edit,
how the list is painted and why items carry an `IImage` instead of a glyph — is in
[completion.md](completion.md). What belongs here is the split: the *decisions* (which concepts,
which key does what, when the list closes) are in this layer and testable headlessly; only the
painting is above it, and a host that renders its own list needs no editing knowledge at all.

## The context menu

`EditorMenu.For(session)` returns a list of `EditorMenuEntry` — labels, gestures, enabled
state and the command each one runs. It is a description, not a widget, and it is derived
fresh on every opening: nothing is stored, so no edit has to remember to refresh a menu.
That is what keeps it in the editing layer instead of an Avalonia one; the control only maps
entries onto toolkit items, and `EditorMenuPresenter` is the one place that does the mapping:
it turns a submenu into a toolkit submenu, an option into a radio item and a gesture string
into the toolkit's own gesture, so a host that wants the same entries elsewhere — the sample
shell's menu bar — calls it rather than binding the model to its own controls.

- An entry's command is the same action the keyboard would run, so the menu cannot drift
  from the key bindings. The gesture it displays is documentation of that agreement.
  Gestures are written as text and parsed by the toolkit, so they are spelled the way a
  toolkit parses them (`Delete`, not `Del`); a host that cannot parse one loses the
  accelerator hint, not the command.
- Availability is a question about state, answered before the entry is shown: Undo asks the
  history, Delete asks the caret whether a character lies to its right or a selection exists.
  `EditorMenuItem.Execute` also refuses a disabled entry, so a stale menu cannot edit.
- The caret follows the right button, because every other entry describes the caret: clicking
  outside a selection moves it, clicking inside one keeps the range, which is what makes a
  range right-clickable in the first place.
- A command runs the session directly, so no `EditResult` comes back to say whether the
  document changed size. The control therefore measures and repaints itself after any entry
  runs, closes the menu itself rather than waiting for the toolkit, and keeps exactly one
  menu: opening a second one closes the first, and detaching the control closes it too.
- `Representations(session)` is public because the representation choice belongs to the
  document, not to the context menu: the sample shell offers the same list from its menu bar.
  The variant in force is marked, and picking an option sets the choice through
  `ChooseVariant`.

Only entries whose actions exist are offered. Node deletion and slot-targeted insertion are
missing because `EditContext` has no remove and no insert-at-index yet — an entry that
silently did nothing would be worse than no entry.

The menu is one of the two places the editor asks "what can I do at this caret". The other is
the light bulb, which offers what no key binding can express; it is planned in
[context-actions.md](context-actions.md), and its entries are meant to appear in this menu as
well, from the same list.

## Selection

A selection is a pair of `CaretPosition`s plus a rule for resolving it to model elements.
The lifetime rule is the important one: a selection only exists as long as the nodes
involved do. If a selected node disappears, the selection shrinks to what is left — it never
silently moves to a different node.

A selection comes in two shapes, and the shape is what the actions downstream key on:

- **inside one cell** — anchor and caret name the same `CellAddress`, so `IsSingleCell` is
  true and `HasText` is true. This is the shape every text action edits: typing replaces the
  range, Backspace and Delete shorten it, Copy takes it.
- **across cells** — anchor and caret name different cells, so the range covers every caret
  place between them in document order. `IsSingleCell` and `HasText` are both false, so no
  text action mistakes it for one. It is the shape a node is selected in, and
  `SelectionRects` has always drawn it: the two end cells are trimmed to their offsets and
  every cell between them is highlighted whole, one rectangle per line.

**An equal address is not the same cell.** Every keyword and every space of a node carries
that node's address, so a selection from a rule's `rule` keyword to its closing brace has two
ends that name one address and two different cells. `IsSingleCell` therefore compares the
document-order hint `CaretPosition.Order` as well, and falls back to address equality when a
position carries none (`< 0`), which is what a position named by address alone gets. Without
that, "select the whole rule, press Delete" routed to the text action — a constant is not
editable, so the key did nothing. The same hint is why a caret is never resolved by address
alone; see [the caret is a cell, not an address](#the-caret-is-a-cell-not-an-address).

`SelectionRanges` (`Projectional.Editing/SelectionRanges.cs`) is the only place that computes
those ranges. It answers four questions — the value at the caret (`OfProperty`), the cells
one node shows together with its descendants (`OfNode`), the whole document (`OfDocument`),
and the inverse of a range, the nodes it covers (`NodesIn`) — and returns `null` when the unit
does not exist, which is how the `Ctrl+A` ladder knows to skip a rung instead of stalling on
it. [clipboard.md](clipboard.md) explains the ladder itself.

Node membership is decided by `CellAddress.Owner`, not by address equality. A child's
projection root carries the *parent's* element address, and a collection's gaps and separators
carry the owning node's address, so the test "is this caret place inside that subtree" has to
be asked of the owner id — otherwise a selected list would have holes where its separators are.

**Delete acts on a cross-cell selection; the other actions do not yet.** `NodesIn` returns the
*outermost* nodes a range fully covers, and `DeleteSelectionAction` removes them in one
transaction, so a multi-node delete is a single undo step. A partly covered node is left alone:
a selection that starts halfway through a rule deletes nothing rather than guessing which half
was meant. Cut, Copy and Paste still fall back to caret behaviour, because a structural copy
needs a clipboard format that can hold a subtree — that is M3, and
[clipboard.md](clipboard.md) records why.

## The clipboard

Copy, Cut, Paste and Select All are ordinary `EditAction`s, which is what keeps them testable
without a window. `ITextClipboard` is the only platform seam: one `string? Text` and a default
`RefreshAsync`. A host without a clipboard gets `InMemoryClipboard`; the Avalonia control
supplies `AvaloniaClipboard`, which caches the platform clipboard so a synchronous key press can
read it. [clipboard.md](clipboard.md) is the full account — what each action means, why copy
produces no undo step, and why a refused cut leaves the text on the clipboard.

## Error handling

Mutating a node can fail — a mandatory slot cannot be deleted, a value does not fit its
type, an index is out of range. Each of those is answered as `Rejected` or `Unchanged` and
is a normal outcome of pressing a key. `EditingException` is reserved for programming
errors, such as opening a session on a node that is not in a model.

## What M1 does not do

| Missing | Consequence | Arrives with |
| --- | --- | --- |
| Insert at index | Enter appends to the end of a list, not below the caret | M2 (tables need it) |
| Property defaults are materialized | typing into a default inserts into it (`"unnamed"` + `"P"` → `"Punnamed"`) instead of replacing it, except in a node an edit has just created, whose value arrives selected | `feat/property-defaults` after M1 |
| `Ctrl+Home` / `Ctrl+End` | not routed; document start and end are reached with Home/End on the first and last line | M3 |
| Completion of a filled slot (substitution) | a list only offers concepts for an empty slot; replacing an existing child arrives with the language's substitution rules | M3 ([completion.md](completion.md)) |
| Multi-cell text editing | a selection behaves like a caret for text input | M3 |
| Copying a node | the clipboard carries text, not a structural fragment, so a subtree cannot be pasted into another slot | M3 |
| Clipboard text carries no concept | pasting into a cell parses the text against that cell's type, so cross-concept paste is the type's business | M3 |
| Intentions (light bulb) | the menu offers only what a key binding could already do | M3 ([context-actions.md](context-actions.md)) |
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
