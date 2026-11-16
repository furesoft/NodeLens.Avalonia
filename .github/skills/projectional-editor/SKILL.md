---
name: projectional-editor
description: Architecture invariants, working conventions, build commands and Windows tooling pitfalls for the Projectional repository - an MPS-style projectional editor on .NET. Use when implementing, testing, reviewing or committing anything in this repo, so that new code does not break the decisions already frozen in docs/adr.
---

# Projectional editor - repo working guide

## What this is

A projectional editor for .NET, modelled on JetBrains MPS. The abstract syntax tree *is*
the program. Notation is computed from the model by projections; there is no text buffer
and no parser. Target users are people who build their own DSLs or embed a DSL editor as
a component inside a host application. Planning is finished and lives in `docs/`;
milestone **M0 is complete** (model, change events, undo/redo, XML persistence, test
harness, CI). **M1 (projection and editing) is in progress**: the declarative notation layer
in `Projectional.Language`, the runtime editor tree in `Projectional.Projection/Tree/`, the
measure/arrange layout in `Projectional.Projection/Layout/`, the headless snapshot renderer in
`Projectional.Projection/Snapshot/`, the edit-action layer in `Projectional.Editing/`, the first
two real languages (`Projectional.Expressions`, `Projectional.Samples`), the embeddable
`Projectional.Avalonia` control (which paints real glyphs, hit-tests clicks and handles keys),
syntax highlighting from the meaning on a run to a host-swappable palette, model-defined text
decorations, cell-wise caret motion with a blinking caret, a context menu described by the
editing layer, concept- and value-driven completion (Ctrl+Space, icons as host images, an
enumerated property offering its values on arrival), and text
clipboard editing (Ctrl+A/C/X/V through a host-supplied `ITextClipboard`), line numbers, font
size, model-defined markers with hover tooltips, concept descriptions shown on hover and in the
completion list, quick fixes (Alt+Enter/Ctrl+., a painted list, a bulb in the margin), the standard
check catalogue as a package (`Projectional.Checks`), the intention model and its per-caret list, and logical
scrolling (bars via
`ILogicalScrollable`, wheel, page keys) are done. The
standalone shell (tabs, menu bar, status bar) is being delivered.

## Status and where the truth lives

- `docs/roadmap.md` - milestones M0-M7 with acceptance gates and effort estimates.
- `docs/architecture/` - model, language, projection, editing, persistence, semantics, integration, standalone, plus one document per planned feature: `completion.md`, `markers.md`, `node-moves.md`, `scrolling.md`, `import.md`, `csharp.md`.
- `docs/adr/` - 13 accepted decisions. **Frozen.** To change one, write a new ADR that supersedes it.
- `docs/open-questions.md` - open items, each with a recommendation and a decide-by milestone.

A milestone is done when its acceptance gate passes, not when the code compiles.

## Non-negotiable invariants

These are the ones that are expensive or impossible to retrofit. Violating one is a bug
even if the build stays green.

1. **The model is the only source of truth.** No text buffer, no parser, no whitespace
   semantics, no "sync text back into the tree" path. Anything the user sees is derived.
2. **Dependency arrows point downward only** (see `docs/architecture/overview.md`):
   `Core` -> `Language` -> `Projection` -> `Editing` -> `Avalonia` -> `Workbench`.
   `Projection` and `Editing` must **never** reference Avalonia. That keeps layout and
   editing testable headlessly and leaves a second frontend possible.
   The shipped languages hang off `Language`, not off each other's callers:
   `Expressions` -> `Language`, `Samples` -> `Expressions` + `Language`. `Core` must never
   reference `Language`; languages register into Core's registries from above.
3. **Only `Projectional.Core` and `Projectional.Avalonia` expose public API.** Every other
   assembly is `internal` plus `InternalsVisibleTo`.
4. **All mutation happens inside a transaction** and emits change events. Subscribers must
   be passive: mutating the model from a change handler is a bug.
5. **Language definitions are reflection-free.** Fluent builders registering into an explicit
   registry instead of attribute scanning or source generators. This keeps concepts
   deterministic, AOT-friendly and debuggable.
6. **Node identity is a stable GUID, never renumbered.** References store the target ID plus
   a `resolveInfo` fallback string, so an unresolved reference degrades instead of throwing.
7. **Serialization must be byte-stable** (ADR 0005): slot order is concept declaration order,
   attribute order is `id`, `concept`, then name; LF endings; UTF-8 without BOM; two-space
   indent; nothing derived from time, machine or hash order. The reference test is
   save -> load -> save producing identical bytes across the whole corpus.
8. **Never drop data on load.** An unknown concept, unknown property or unresolvable reference
   is preserved as-is and reported as a diagnostic; loading does not fail because of it.
   (M0 departs from this in one place - a property value the language refuses fails the load
   instead of being kept - and that exception is documented in
   `docs/architecture/persistence.md`.)
9. **`net10.0` only**, no multi-targeting (ADR 0003).
10. **A concept is concrete or abstract, and only concrete concepts can be nodes.**
    `ConceptDeclaration(..., isAbstract: true)` names a *set* that a slot can be declared over;
    `Model.NewNode` rejects it, so no code path can produce a bare base node. The single consumer
    is `ConceptRegistry.DefaultInstantiableFor(key)`, which returns the concept itself when
    concrete and otherwise the first non-abstract concept below it in **declaration order** -
    that order is the language author's answer to "what does the user most likely want here",
    and `InsertNodeAction.FillSlot` uses it to fill an empty slot (returning `EditResult.Unchanged`
    when no concept can exist). An abstract concept may therefore have no notation; every
    concrete one needs its own or an inherited one.
    A separate flag, `ConceptDeclaration(..., isOfferedInCompletion: false)`, keeps a concept out
    of the completion **list only**. `DefaultInstantiableFor` must keep ignoring it: a hidden
    concept stays a legal child reachable with Enter, which is exactly how `Expressions` keeps
    its three literals usable while leaving them out of the menu. Never "fix" the two into one
    rule; `CompletionSource.Proposals` filters on both, the default fill on neither.

## Working conventions (explicit user requirement)

- Every feature gets its own branch off `main`. Prefixes: `feat/`, `chore/`, `docs/`, `test/`.
- Every subtask is its own meaningful commit - not one giant commit per branch.
- Validate (build plus tests) **before** merging. Merge with `--no-ff` and the default
  `Merge branch '<name>'` message so branch structure stays visible in history.
- Conventional commit prefixes (`feat:`, `fix:`, `chore:`, `docs:`, `test:`); the body explains
  *why*, not *what*.
- Never mix an unrelated pre-existing fix into a commit that is about something else.

## Code layout (explicit user requirement)

- **One type per file.** A file is named after the single type it declares; nested or
  helper types get their own file rather than sharing one.
- **Group by concern, not by kind.** Files live in a folder named after the concept they
  belong to (`Identity/`, `Language/`, `Model/`, ...), and the folder is the only grouping
  mechanism - the namespace stays flat, so consumers need no extra `using` directives.
- Test files mirror this: one `<TypeUnderTest>Tests.cs` per type.

## How `Projectional.Core` records change

Understand this before touching `Model`, `Node` or anything under `Changes/`; the seams are
deliberate and easy to break.

- `Model` owns a private `ChangeJournal` (a `List<JournalEntry>`), a nesting depth `_depth`,
  and the `Changed` event. `IsRecording` is `_depth > 0 || Changed is not null`, so a model
  nobody watches allocates no rollback closures at all. Guard every `Record` call with
  `if (Model.IsRecording)`.
- Every mutation records an `IModelChange` **plus its own undo closure and, where the
  operation has a forward direction, its redo closure**. Undo reuses exactly these
  closures, so they must be true inverses.
- Journal closures are **raw**: they call the unrecorded internals (`AttachPlacement`,
  `UnlinkFromPlace`, `RestoreReference`, `UnsetReference`, `DeleteSubtree(node,
  record: false)`) and never `Record`. A replay therefore journals nothing, and
  `UndoHistory` publishes the change set itself (`Invert()` per entry in reverse for
  undo, the original records in forward order for redo) through
  `Model.PublishRestored`. When you add a mutation seam, add its redo too — a missing
  redo makes redo silently skip the step.
- A journal entry with a `null` change is rollback-only. `CreateDetached` uses one so that
  `NewChild` publishes a single `NodeAdded` at the final place instead of root-add plus
  root-remove plus child-add.
- `MutationScope` is the implicit path: `using var scope = Model.BeginMutation();` is a
  no-op unless the model is recording, and it **commits on `Dispose`**. `ModelTransaction`
  is the explicit path: `Complete()` keeps, `Dispose()` without `Complete()` rolls back.
  Use an explicit transaction whenever a composite edit must not survive a partial failure.
- Commit ordering in `EndTransaction`: decrement `_depth`, then on rollback call
  `RollbackTo(mark)` and restore `Version` to the value captured at begin, on commit with
  `_depth > 0` do nothing, and only on the outermost commit publish `ChangesFrom(mark)` and
  clear the journal. The mark is therefore always 0 at publish time.
- Rollback runs entries in reverse order. That is what makes a move work: the `NodeAdded`
  rollback unlinks, then the `NodeRemoved` rollback re-attaches at the old place. Keep the
  pair independent - do not make one closure depend on the other.
- Deleting a subtree emits **one** `NodeRemoved` whose rollback is a `SubtreeSnapshot`
  (placement plus every node in document order), then one `ReferenceChanged` per cleared
  reference in survivors. Reference restores are order-independent dictionary writes, so
  they are valid before the nodes are re-registered.
- `NodeAdded.Parent` and `Role` are nullable on purpose: `null` means "model root".
- Subscribers must be passive. Mutating from a `Changed` handler is a bug, and
  reentrancy through `Model.Changed` is not guarded against.

## How undo, redo and coalescing work

`UndoHistory` (`src/Projectional.Core/Undo/`) subscribes to `Model.Changed`; it is not
wired into the edit API, so anything that publishes a change set is undoable for free.
Read its `<remarks>` before changing the replay path.

- `UndoStep` bundles one committed transaction's `JournalEntry` list plus the
  `CoalescingKey` it was committed under.
- Undo plays the entries **in reverse** calling `Undo`; redo plays them **forward**
  calling `Redo` (skipping the nulls).
- `Replay` sets `_replaying`, opens a transaction, runs the closures, `MarkMutated()`,
  `Complete()`, and then calls `PublishRestored` **while `_replaying` is still true** —
  otherwise the history would record its own restoration and redo/undo would fight.
- `OnModelChanged` bails out on `_replaying` or on `e.Steps.Count == 0` (undo/redo
  publications carry no steps on purpose).
- Any commit clears the redo stack. Coalescing only merges into the previous step when
  the current `CoalescingKey` is non-null and equals that step's key; `null` never
  merges. There is no time window — the host must clear the key when the burst ends.
- The `UndoHistory` API is `Undo()`, `Redo()`, `CanUndo`, `CanRedo`, `UndoDepth`,
  `RedoDepth`, `CoalescingKey`, `Clear()`, plus `Dispose()` to unsubscribe.

## How persistence works

`Projectional.Persistence` is the only place that knows the stored form. It has
`InternalsVisibleTo` from Core, which it needs for two loading seams; nothing else about
it is special.

- **Public API is two static classes.** `ModelXml.Serialize/WriteTo/Deserialize/ReadFrom`
  for strings and streams, `ModelFile.Load/Save` for files. The reader and writer are
  `internal` and reached only through these, so the determinism rules have one gate.
- **Determinism lives in `ModelXmlWriter.CreateSettings()`** - LF, two-space indent,
  UTF-8 without BOM. Change it there and nowhere else; the byte-identical corpus test
  (`Corpus/` in the test project) will fail otherwise.
- `NewLineHandling.Entitize` is deliberate: a `\r` inside a property value is written as
  `&#xD;` while indentation still uses `\n`, so values survive round trips byte-stably.
- **The `name` attribute on `node` is written but never read.** It duplicates the name
  property for greppability; the property is authoritative.
- Two loading seams in Core exist only for persistence:
  `Model.CreateDetached(NodeId, Concept)` (identity from the file) and
  `Project.DiscardModel(Model)` (remove a half-built model). Only
  `CreateDetached(Concept)` journals a rollback entry - picking the id-carrying overload
  directly skips journaling, which is correct for loading and **wrong for a mutation**.
  When you touch either overload, keep the journaling in the single-argument one.
- **A failed load leaves nothing behind**: the reader remembers the model it created,
  deletes what it built with `record: false`, calls `DiscardModel`, and throws
  `ModelFormatException`. Both catch arms (its own format errors and wrapped
  `XmlException`/`ModelInvariantException`/`FormatException`) must discard.
- `ModelXml.WriteTo` refuses to write while `model.HasTransaction`; `ModelFile.Save` turns
  that into "no target file and no temporary file created", because the temp file is
  deleted in the `finally`.
- Save writes `<name>.<guid>.tmp` next to the target, `Flush(flushToDisk: true)`, then
  `File.Move(overwrite: true)`.
- **Persistence tests live in `Projectional.Core.Tests`** (which references
  `Projectional.Persistence`) so they can reuse `TestLanguage.cs` and no fixture is
  duplicated. If a second test project ever needs that language, promote it to a
  `Projectional.TestLanguage` library rather than copying it.

## How notation is declared

`Projectional.Language` is where notation lives: declarative data that the projection engine
walks, validated once at registration against the `ConceptRegistry`. Nothing about it is code,
and nothing about it lives on `Concept` - Core must not know that projection exists.

- **The inline/block split is carried by the type hierarchy, not by validation.**
  `NotationCell` is abstract; `InlineNotationCell` runs horizontally inside a line, and
  `BlockNotationCell` occupies whole lines. `LineNotationCell` takes `ReadOnlyList<InlineNotationCell>`
  and `SequenceNotationCell`/`IndentNotationCell` take `ReadOnlyList<BlockNotationCell>`, so
  `ed.Line(ed.Indent(...))` does not compile. Do not weaken a collection type to make a
  notation convenient; the compile error is the feature.
- **`Sequence` vs `Indent`.** A sequence stacks blocks unchanged and is usually the root of a
  notation; an indent stacks them one level deeper. They are separate on purpose, so a
  notation says exactly one thing at a time.
- **The runtime editor tree is a different vocabulary.** Notation cells are `…NotationCell`
  (`ConstantNotationCell`, `PropertyNotationCell`, `SingleChildNotationCell`,
  `ChildNotationCell`, `CollectionNotationCell`, `LineNotationCell`, `SequenceNotationCell`,
  `IndentNotationCell`, `SpaceNotationCell`) precisely so they cannot be confused with the
  runtime cells `docs/architecture/projection.md` names (`ConstantCell`, `PropertyCell`, ...).
- **Every slot-bearing cell has an `EmptyText`** (`<empty>`, `<none>`). Empty is never
  invisible: the caret needs something visible to attach to, and an empty list must stay
  fillable. Do not make it empty-string to "tidy up" a rendering.
- `NotationWalker.ChildrenOf` is the **single** place that knows a cell's children. Anything
  that walks notation goes through it - adding a cell type without adding it there makes the
  cell invisible to validation and to `Notation.SelfAndDescendants()`.
- `NotationRegistry.Add` is the only validator, and it is total: unknown concept, duplicate
  notation, property/reference/child cells naming slots that do not exist, and a
  `SingleChildNotationCell` on a many-child slot (it would hide the siblings). A rejected
  notation is not half-added - the registry stays clean.
- `NotationRegistry.FindFor(concept)` inherits: own notation first, then the super concepts in
  declaration order, so a concept that only extends a language reuses its notation.
- `NotationEditor`'s members touch no instance state and therefore trip CA1822, which is an
  error here. The suppression is class-level and deliberate: the documented surface is
  `ed.Keyword("if")`, which a static class cannot express.
- **`ITypingRules` sits in `Projectional.Language/Typing/`, beside `ICellEmphasis` and for the
  same reason** - it says how the language is written, so a language package must be able to
  declare it without referencing `Projectional.Editing`. `TypingRuleRegistry` is immutable
  (`Add` returns a new one), refuses a duplicate concept key with `ArgumentException`
  (`ParamName` `"conceptKey"`), and its `ClaimFor` answers `null` for a concept that declares
  no rule. `ExpressionsLanguage.TypingRules` is the reference declaration; `RulesLanguage`
  forwards it. What the editor does with a claim is in
  `docs/architecture/language.md` §Typing rules and `editing.md` §Typing into an empty slot.
- **`ITriggerRules` sits beside it, in `Projectional.Language/Triggers/`**, and says what a typed
  character means at the *edge* of a node that already exists; `ExpressionsLanguage.Triggers` is the
  reference declaration and `RulesLanguage` forwards it the same way. Same layering reason, same
  immutability, one different naming rule: the lookup is `Find`, not `For` (CA1716). See
  `docs/architecture/triggers.md`.

## How notation variants work

A concept may be drawn more than one way - the same ruleset as a block and as a table - so
notation is registered per concept **and** variant
([ADR 0012](../../../docs/adr/0012-concept-rendered-more-than-one-way.md),
`docs/architecture/language.md` §Variants, `docs/architecture/editing.md` §Representations).

- **A variant is view state, never model state.** One variant is drawn at a time; the choice
  lives on `EditorSession.Choice` as an `INotationChoice`/`NotationChoice`, is never persisted
  and never creates an undo step. Do not move it onto `Node`, `Model` or the notation.
- **`Notation.Variant` defaults to `Notation.DefaultVariant`** (`"default"`), so the
  single-variant case is written exactly as before and every existing language keeps working.
- **Resolution is a chain walk with three fallbacks**: requested variant (own notation, then
  supers in declaration order) -> the default (own, then supers) -> the first declared variant
  of the chain -> `MissingNotationCell`. `FindFor(concept, choice)` is the only resolver
  (private `FindVariant`/`FindAny` do the walking); `VariantsOf(conceptKey)` answers "what is
  this concept declared as" from its *own* notations only. Do not add a second lookup that
  resolves differently.
- **`NotationChoice` is an immutable map from concept key to variant name** (`With`, `Without`,
  `VariantOf`, `IsEmpty`, `ToString` -> `"Rules.Ruleset=table"`). It is keyed by concept, not by
  node: per-node choice is open question 26.
- **`EditorSession.Alternatives()` walks the document, not the registry.** It returns the
  concepts the document actually uses that resolve to more than one variant, in document order,
  with the variant in force. A menu built from the registry would offer forms for concepts
  nobody wrote.
- **`ChooseVariant(conceptKey, variant)` validates twice** - the concept must occur in the
  document and the variant must apply to it - then `Choose`s and rebuilds. It throws
  `ArgumentException` with `ParamName` `"conceptKey"` or `"variant"`, because a bad menu entry
  is a programming error, not a user outcome.
- **A variant may hide slots; a shown slot keeps its natural address.** That single rule is why
  caret, selection, hit test and undo need no knowledge of variants at all. Never give a
  variant-carrying cell a synthetic address.
- **`ConceptVariants` (`ConceptKey`, `Variants`, `Selected`)** is the public, Avalonia-free shape
  a host binds a menu to. It is public because `Projectional.Editing` has no
  `InternalsVisibleTo`.

### Tables

`TableNotationCell`/`TableColumn` are the first variant that is not a rearrangement of lines
(`docs/architecture/projection.md` §Tables).

- **A column names a slot of the *row* concept**, it does not carry a cell: a property column
  becomes a property cell, a single-child column a child cell. That is what lets one table draw
  rows of different concepts that declare the same slot.
- **A header is a cell of the table's own notation** and the `TableColumn` constructor refuses
  anything but a constant or a space. A header that named a slot would silently depend on
  whichever row came first.
- **The row concept comes from the rows slot**, so `NotationRegistry.ValidateTable` checks the
  columns against the child concept of the `Slot` - not against the concept owning the table.
  A column naming no slot, a name that is neither property nor child, and a many-valued child
  are all rejected at registration.
- **An empty table has no header**: the builder passes no headers and `TableCell.Flatten` throws
  if it is handed headers with no rows. `EmptyText` is what an empty slot shows, exactly like
  `CollectionCell`.
- **Headers are projected from the node that owns the table, not from a row**, so a table with
  zero rows still knows its shape and a header can never be answered by a row.

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
  unset for the same reason.
- **`ChildCell` and `CollectionCell` both lay their content out inline**; only the surrounding
  container decides whether that line stacks. `Many`-slot children are inline by nature, so a
  vertical list needs `Sequence(...)` around the `Children(...)`.
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
- **Draw only what has text**: `TextCell.Text`, plus the `EmptyText` of an empty `ChildCell`
  or `CollectionCell`. Containers carry no text, so drawing in document order can never bury
  a child under its parent, and one `Write` per cell is enough because every text cell is one
  line tall.
- **Empty slots must show their placeholder.** Layout already reserves `EmptyText`'s width;
  a renderer that skipped it would print a projectional language with invisible slots.
- **Width is a cut, not a wrap**, because the caller asked for that width. `SnapshotGrid`
  throws on overflow instead of clipping, so clipping has to happen here, deliberately.
- **Roots stack with no gap** - vertical spacing is a notation's business, not the renderer's.

## How the edit actions work

`Projectional.Editing` is the layer between a keystroke and the model. It has no UI
dependency, so every action is testable headlessly.

- **An address does not identify a cell.** Constants, punctuation and spaces all carry the
  address of the *node* they belong to, so the `{` at the end of a line and the `rule`
  keyword at its start share one address and one offset range. `CaretPosition` therefore
  also carries the **document-order index** of the cell (`Order`, `internal`): `>= 0` means
  "the closest caret place with this address", `< 0` means "the first place with it". Never
  resolve a caret by address alone - that is exactly the bug this hint exists for.
- **Corollary: `CaretPosition.ToString()` is `"{Address}:{Offset}"`, so it cannot be used to
  dedupe cells.** Several cells print the same string. When a test needs to know *which*
  same-address cell the caret is on, ask `CaretNavigation.PlaceAt(tree, position)` and compare
  `CaretPlace.Cell`.
- **`CaretPlace` is a `struct`.** `CaretNavigation.PlaceAt(...)` returns `CaretPlace?`, but
  `if (x is null) return;` does **not** narrow it - use `x.Value` afterwards.
- **A filled slot produces no caret place of its own** - only its leaves do. So Tab past a
  body that already has a step lands on the step's `index`, not on the collection.
- `EditorSession` owns the rebuild cycle: after every change it rebuilds tree *and* layout,
  then re-resolves the caret against the new generation, and hands the coalescing key back to
  the caller so a run of typed characters stays one undoable step. Rebuilding everything on
  every keystroke is deliberate M1 policy, not an oversight.
- **Enter appends.** `Model.NewChild` adds to the end of a list; there is no insert-at-index
  API yet, so Enter on a slot in the middle of a list puts the child last.
- **Typing into an empty slot creates what you are describing.** `TypeTextAction`'s second case:
  an empty `ChildCell`/`CollectionCell` with a collapsed selection asks the language's
  `ITypingRules` which concept claims the text, creates it there and writes the text into the
  rule's property - one undo step, because `EditContext.AddChild` opens a nested
  `ModelTransaction`. It cannot be inferred from the concepts (a string property accepts `abc`
  as happily as `"abc"`), which is why the language declares it beside the notation. The caret
  afterwards is the *created node's* `CellAddress.OfElement`, so `Nearest` lands on the first
  typeable place inside it - `IntLiteral.value` (so `1` then `2` gives `12`), or
  `Parenthesized.inner` rather than the `(` the notation draws. A rule may name no property (a
  parenthesis is a gesture, not text); a rule naming a slot or a text the type refuses still
  creates the node and drops the text, because a keystroke must not be able to throw. A
  non-empty selection and a language with no rules both stay `Unchanged`.
- **The value a created node lands on arrives selected.** `EditAction.SelectsLandedValue` is the
  intent ("what I made is something to be named") and `EditorSession.Apply` carries it out against
  the tree the edit produced, widening the caret to the whole `PropertyCell` it landed in. It is a
  property of the *creating* edit only: if typing re-selected what it had just written, the next
  keystroke would replace it and no name longer than one character could be typed.
- **An empty single-child slot renders one cell** whose address is
  `CellAddress.OfSlot(ownerId, slot)` - that is the address Enter fills. **An empty collection
  slot renders one cell too**, showing its `EmptyText`; it has no `Content`, so it is a leaf
  and therefore a caret stop, which is why Enter and completion can fill it
  (`CaretPlace.IsEmptySlot` covers both `ChildCell { IsEmpty: true }` and
  `CollectionCell { IsEmpty: true }`). What produces *no* cell is a slot the notation does not
  draw - a concept with no notation, or a variant that hides the slot - and that is the only
  case where a keystroke has nothing to act on.
- **An `EditorSession` must be created after the nodes it should show exist.** It builds tree
  and layout in its constructor; nodes added to a live model afterwards need `Refresh()`. In
  tests this bites when a fixture builds the session before `model.AddRoot(...)`.
- **Typing into a defaulted property inserts, it does not replace.** Defaults are
  materialized values (`name` = `"unnamed"`, `enabled` = `"true"`), and the model cannot tell
  an unset property from a set one, so `"unnamed"` + `"P"` gives `"Punnamed"`. Accepted
  limitation until a `Node.Unset`/placeholder concept exists (future `feat/property-defaults`);
  the one path that escapes it is a node the user just created, whose value arrives selected.
- `Node.GetOrNull`/`Set` handle **properties only**. A reference slot needs `GetReference`,
  `GetReferenceTarget` or `SetReference`; asking `GetOrNull` for one throws
  `KeyNotFoundException` ("declares no property slot named ...").
- Text actions edit inside a single `PropertyCell`; a multi-cell selection degrades to caret
  behaviour. `Ctrl+Home`/`Ctrl+End` are routed as `CaretMove.DocumentStart`/`DocumentEnd`
  (`KeyRouter`), and `PageUp`/`PageDown` move the caret by `EditorSession.PageHeight`; `Ctrl+PageUp`/
  `Ctrl+PageDown` stay with the host.
- **`Ctrl+A` is a ladder, not one answer.** `SelectAllAction` widens one unit per press:
  the property at the caret, then the node under it (plus its descendants), then the
  document. `SelectionRanges` (`SelectionRanges.cs`) computes all three and returns `null`
  for a unit that does not exist, so a rung is *skipped* rather than blocking - a caret in a
  keyword has no value, so its first press selects the node. `SelectAllAction` compares
  candidate ranges with `Address`+`Offset` only, **ignoring `CaretPosition.Order`**: a
  position named by address alone has `Order == -1`, so record equality would call the same
  range different and the ladder would stall on one rung forever.
- **A double click asks for one rung of that ladder, narrower.** The control maps
  `e.ClickCount > 1` to `SelectWordAction` and `e.ClickCount > 2` to `SelectValueAction`, which
  are `SelectionRanges.OfWord` and `SelectionRanges.OfProperty` alone: the word under the
  pointer, then the whole text of the editable cell, or the selection exactly as the press left
  it when the cell is not a `PropertyCell` or is empty. Neither falls back to the node - a
  double click on a rule's `{` selecting the whole rule would answer a question nobody asked.
  A word is the maximal run of one character kind (letters/digits/underscore, whitespace, other)
  around the caret, taken from the character *after* the caret when it is a word character and
  otherwise from the one before, so clicking either half of a letter selects that letter's word
  and not the space beside it. Three things follow: the rules live in `Projectional.Editing`
  (the control owns no editing rule), the two units are two action types rather than one action
  with a width so a host can rebind them separately, and every press after the first
  deliberately does **not** set `_dragAnchor`/`_dragging` or capture the pointer, because the
  first pointer move would collapse the range onto the drag anchor and the selection the user is
  about to type over would vanish.
- **Node membership is `CellAddress.Owner`, never address equality.** A child's projection
  root carries the *parent's* element address, and a collection's gaps and separators carry
  the owning node's address, so "is this place inside that subtree" must be asked of the
  owner id or a selected list ends up with holes where its separators are.
- **`Selection.IsSingleCell` is address equality *plus* the order hint.** This is the trap the
  whole selection feature tripped over: `OfNode(rule)` for a `Rule` whose notation starts with
  the `rule` keyword and ends with `}` produces `Anchor = (rule.Id, 0)` and
  `Caret = (rule.Id, 1)` - equal addresses, different cells - so `IsSingleCell` was true,
  `HasText` was true, `SpansCells` was false, and `KeyRouter.Delete` sent the keystroke to
  `DeleteTextAction`, which refuses a `ConstantCell`. "Ctrl+A twice then Delete does nothing"
  was that. It now reads
  `Anchor.Address.Equals(Caret.Address) && (Anchor.Order < 0 || Caret.Order < 0 || Anchor.Order == Caret.Order)`,
  so a position named by address alone still compares by address. Do not "simplify" it back to
  an address comparison.
- **`KeyRouter.RemovesText` checks for a `PropertyCell` first, then `HasText`.** The order
  matters: a `HasText` selection on a non-editable cell must not reach a text action that is
  guaranteed to refuse it.
- **Delete acts on a cross-cell selection, through `DeleteSelectionAction`.** It asks
  `SelectionRanges.NodesIn` for the nodes the range *fully* covers (outermost only, never the
  document root) and removes them in one `Model.BeginTransaction()`, because `EditorSession.Apply`
  opens none and each `DeleteChild` journals its own step. A partly covered node removes
  nothing. `DeleteNodeAction.Landing` is `internal static` and shared, so both deletes land the
  caret in the same place. Cut/Copy on a cross-cell selection are still not implemented - a
  structural clipboard needs a payload that can hold a subtree (M3).
- **`Model.DeleteSubtree` unlinks the top node only.** Descendants stay attached to each other
  and are merely unregistered from the project index, which is exactly what lets one
  `SubtreeSnapshot` undo restore the whole thing. So `body.GetChildren("steps")` still answers
  after `body` was deleted - a test asserting a deleted parent's children are gone is wrong.
- A cross-cell selection reports `IsSingleCell == false` and `HasText == false`, so no text
  action mistakes it for one. `SelectionRects.Of` already draws it correctly (both ends
  trimmed, intermediate cells whole, one rect per line) - the rendering needed no change.

## How checks and quick fixes work

A finding is a `Diagnostic` (in `Projectional.Semantics`); the repair is a `DiagnosticFix`
(`Projectional.Editing/Fixes/`). The contract is that a **fix produces an `EditAction`** and never mutates:
`EditorSession.Apply(DiagnosticFix)` joins the pick on `(Diagnostic, Id)` against the **current** `FixSet`,
runs the current object (never the caller's copy), and applies it with the coalescing key cleared so it is
one undo step. A pick that no longer resolves is `EditResult.Rejected` with a reason - the ordinary case,
not an error. A repair that takes several edits answers an `EditProgram`, which the session runs inside one
transaction and rolls back completely if a step is rejected.

- **`FixSet` is keyed by the `Diagnostic` record** and rejects two entries for one finding.
  `QuickFixes.At(session)` answers the caret's line (resolved geometrically, `MarkerGeometry` tops against
  `CaretBounds`), worst finding first; `QuickFixes.Of(session, diagnostic)` answers one finding. Both are
  derived per call and never cached.
- **`EditSafety` decides whether a lone offer may run.** `EditorSession.ApplyOffers()` applies a lone
  offer only when it is `EditSafety.Plain` *and* not disabled; anything else opens a `OfferList`. A list
  of one is therefore legal **only** because a destructive or disabled repair is asking rather than being
  offered - do not restore the old "at least two" rule.
- **A fix's label names the result** ("Rename to \"Check2\"") and `Description` is part of the contract; a
  menu falls back to the finding's message. The id is stable and never translated.
- **Safety is on the offer, not on the fix type**, because the intention catalogue carries the same enum.

`Projectional.Checks` is the standard catalogue a language includes by naming it: `ICheck.Find(Model)`
(the member is `Find` because CA1716 forbids `Of` on an interface member), `CheckSet` (ordered, immutable,
`With` to extend), and `StandardChecks` (`Names`, `Structure`, `Expressions`, `All`). A language publishes
one `CheckSet`; the host runs it and derives `MarkerSet`/`FixSet` from the fixes. Because a check produces
diagnostics *and* edits, a checks package is a **behaviour** package: it references `Editing`, `Semantics`
and (for the expression group) `Expressions`, which is exactly why it cannot live inside a language package.

Shipped checks and the rules they encode:

- `STD001` (`PlaceholderNameCheck`, `Hint`) - name still equals the concept's declared default; the repair
  names it `<Concept>1` with the first free suffix, computed as a group so two unnamed rules get `Rule1`
  and `Rule2`. A concept with no default is not reported.
- `STD003` (`SurroundingWhitespaceCheck`, `Warning`) - surrounding whitespace; `Trim to "..."`. A name
  that is only whitespace is skipped, because trimming it would produce the empty name.
- `STD010` (`EmptyChildSlotCheck`, `Hint`) - a `Cardinality.One` slot with no child; the repair is
  `InsertChildAction`, which is the same creation path `InsertNodeAction` uses, so the fix and Enter cannot
  disagree. A slot whose whole set is abstract has no repair and therefore no finding.
- `STD040` (`BinaryGroupingCheck`, `Warning`) - an operand drawn without the parentheses its grouping
  needs. **The finding names the operand, not its parent**, and the repair wraps that operand: wrapping the
  parent leaves the same finding, and a repair that survives itself is broken. Precedence lives in the
  check, not in `ExpressionOperators`, which says analysis owns it.

Two structural primitives arrived with these: `Model.Wrap(node, wrapperConcept, wrapperSlot)` detaches,
creates and re-attaches inside one transaction (a single-valued slot cannot hold the node and its wrapper
at once), so identity and undo survive; `WrapInAction` is that as an edit and `InsertChildAction` is the
address-based half of Enter (`InsertNodeAction.FillSlot` delegates to it). Both decline with a reason
rather than throwing when the document moved on.

## How intentions work

An intention is a *way to find* an `EditAction` at the caret, in `Projectional.Editing/Intentions/`, and it
is not a new kind of edit. The list is derived per call, never cached, and applies through the same session
path a keystroke takes.

- **`Intention`** carries `Id` (stable, never translated), `Label`, a **required** `Description` (a fix can
  fall back to the finding's message; an intention has nothing behind it), an optional `Gesture`, an
  `Availability`, an `EditSafety`, and `Create: Func<IntentionContext, EditAction?>`. That function is how
  deciding and applying stay separate: an intention never mutates, it produces an action.
- **`IntentionAvailability` has three states, unlike `FixAvailability`'s two**: `Applies`,
  `Disabled(reason)` and `Absent`. A fix is built per finding, so "does not apply" is simply "not built"; an
  intention is *declared once* and asked per caret, so it needs "not my caret" (`Absent`, dropped from the
  list) as distinct from "mine, but not now" (`Disabled`, listed with its reason).
- **A declaration is made per-caret with `With(availability)`**, which answers a copy. That is what lets a
  language hand back its whole catalogue and let the list filter it - do not mutate a declared intention or
  make availability a stored field of the aspect.
- **`IntentionContext` is read-only**: model, tree, layout, measure, selection, the resolved `Place` and the
  `Node` that owns the caret's cell. `Node` is the address *owner*, which is why an empty slot answers with
  the parent ("insert a rule here" is about the parent). It deliberately has none of `EditContext`'s
  mutation methods.
- **`IntentionList.For(session)` is the only answer to "what is here"**: host providers, then language
  providers, then `QuickFixes.At(session)`; each entry's `Id` de-duplicated first-wins, `Absent` dropped,
  `Disabled` kept. Order is fixed by `IntentionSource`, not by registration order; `AddIntentions` defaults
  the source to `Host`, so a language package says `Language` explicitly.
- **A fix enters the list as an intention** (`Intention.FromFix`) and keeps the fix on the entry
  (`Intention.Fix`). `Apply(Intention)` routes such an entry back through `Apply(DiagnosticFix)` so the
  repair is re-resolved against the *current* `FixSet`; everything else is inherited.
- **`Apply(Intention)`** refuses an absent or disabled offer with a reason, builds the action *when it runs*,
  clears the coalescing key (one undo step), runs an `EditProgram` in one transaction that rolls back whole,
  and closes any open quick-fix list. Do not move the "build the action at apply time" rule to list time.

Intentions are *behaviour*, so they cannot live in a language package (which must stay dependency-free of
the editor). The shipped shape is a package beside the language, exactly like `Projectional.Samples.Checks`:
`Projectional.Expressions.Intentions` (wrap in parentheses, negate) and `Projectional.Samples.Intentions`
(accept this rule). `docs/architecture/context-actions.md` carries the plan and the remaining slices
(F onwards: preview, standard catalogue).

- **Some wrappers only mean something with a value, and the wrap has to carry it.** "Negate" is a `Unary`
  whose `operator` is `!`, and the caller cannot write that afterwards because the wrapper's id does not
  exist until the wrap runs. `Model.Wrap`/`WrapInAction` therefore take an optional
  `IReadOnlyDictionary<string, string> presets`, validated against the wrapper's declarations *before*
  anything is detached (a bad preset must not leave the node outside the document). Do not build a bespoke
  action per wrapper-with-a-value.
- **An intention that would change nothing is `Absent`, not `Disabled`.** Both expression intentions decline
  on a node that is already that wrapper, and "accept this rule" declines on a rule that already accepts: an
  entry that reproduces the current drawing (or writes the value it holds) is noise, and a *disabled* one
  would light the bulb for an operation nobody needs.
- **The app registers the providers once, at open**, in `MainWindow.RegisterIntentions`; registering them in
  `PublishFindings` (which runs on every change) would list every entry twice.

**The gesture and the list are named differently on purpose.** `EditorKey.QuickFix`, `QuickFixAction` and
`QuickFixBulb` keep the quick-fix name because they are the *affordance* the docs and ADR 0016 describe;
everything about the *list* is an offer: `OfferList` (of `Intention` entries), `EditorSession.Offered`,
`SelectOfferAction`, `AcceptOfferAction`, `OfferPresenter` (in `Projectional.Avalonia/Offers/`) and
`EditorSession.ApplyOffers()`. Do not "tidy" one half into the other's vocabulary - the split is what keeps
"a quick fix is an intention carrying its `DiagnosticFix`" readable.

- **`ApplyOffers()` asks `IntentionList.For(session)`**, so the popup lists host, language and fixes
  together. The lone-plain rule therefore covers both catalogues; an entry that is `Disabled` or
  `Destructive` opens the list (including a one-entry list).
- **`AcceptOffer` calls `Apply(Intention)`**, which routes an entry carrying `Intention.Fix` through
  `Apply(DiagnosticFix)` - so accepting a repair keeps its re-resolution, and accepting an intention builds
  its action at that moment.
- **`OfferList` refuses an `Absent` entry** in its constructor: the list is what the user chooses from, and
  a row that says "not this caret" would be a row that cannot be chosen.
- **The bulb asks the same list** (`IntentionList.For(session).Count > 0`) and keeps that answer keyed on
  `(Layout, Caret, Markers, Fixes)`; a source registered *after* the document opened catches up on the next
  caret move, which is why hosts register at open.
- **Two offers with one id collapse to the first**, which is why the shell's `SHELL001` and the standard
  `STD001` must not both be published: the user would read one sentence twice with nowhere to see the
  duplicate. That is the same rule that lets a standard repair and its intention share an id.
- **`EditorMenu` derives the list once** and splits it: entries with `Fix` go to the "Quick fixes" submenu,
  the rest to "Intentions", fixes first, one separator after the group, and a submenu that would be empty is
  not added. Reading `QuickFixes.At` for the menu instead would let a fix reappear there after the merged
  list de-duplicated it. A disabled entry's `Description` is `Availability.Reason` (why it cannot run), not
  the offer's own sentence.

## How concept triggers work

Text entry has two halves, and both are declared by the language.
[`ITypingRules`](../../../docs/architecture/language.md#typing-rules) answers for an **empty** slot;
`ITriggerRules` (`Projectional.Language/Triggers/`) answers for the edge of a node that already
exists: `5` then `+` gives `5 + <empty>`. `docs/architecture/triggers.md` is the design of record.

- **A trigger names** the concept to create, the anchor concept it applies to (`Concept.IsKindOf`
  decides, so declaring it over an abstract concept covers every concrete one), the `TriggerSide`, the
  single-child `AnchorSlot` that receives the node, and - both optional - the `PropertySlot` the typed
  text goes into and the `CaretSlot` the caret continues in. `TriggerRegistry.Find(Concept)` answers in
  **declaration order**, which is the only tie-break; `Add` returns a new registry like
  `TypingRuleRegistry`. `For` is not a legal member name here (CA1716, a VB keyword) - it is `Find`,
  matching `ICheck.Find`.
- **A trigger is asked only where typing would otherwise do nothing.** In `TypeTextAction.Apply`: a
  property that *accepts* the text wins outright (`-` in front of `5` writes `-5`, the negation is never
  reached), a refused property falls through to the trigger, an empty slot stays the typing rules'
  business, and any other leaf asks the trigger. This is why the trigger lives *inside*
  `TypeTextAction` instead of being a second action under `KeyRouter`: asking first would duplicate the
  property validation the model owns.
- **`TriggerAnchors.Of(context, place, offset)` is the only answer to "what could this be an edge of"**,
  and it reads ownership, not containment: the cells whose `Address.Owner` is the node itself. Interior
  property offset - no anchor. First place the node owns - `Left`. Last - `Right`. A node drawn as a
  single cell - both, `Right` first. A keyword in the middle of a notation - nothing.
- **The wrap is `Model.Wrap` with the typed text as a preset** (the wrapper's id does not exist before
  the wrap), and the trigger's `CaretSlot` is where the caret lands, so `5` `+` `3` gives `5 + 3` with
  no arrow keys in between. Without a `CaretSlot` the caret stays inside the wrapped node, which is what
  a prefix operator wants.
- **`TypeTextAction` guards everything `Model.Wrap` would throw over** (`Fits`): no parent (a document
  root is never wrapped), unknown or abstract concept, wrapper not conforming to the slot the anchor
  stands in, `AnchorSlot` not a single child, anchor not conforming to it. A declared-but-impossible
  trigger does nothing; a keystroke must never throw. A `PropertySlot` whose type refuses the text drops
  the text and still wraps - the same rule `AddChild(parent, slot, concept, propertySlot, text)` follows.
- **A structural keystroke gets no coalescing key.** `TypeTextAction.CoalescingKey` returns `null` when
  `WouldRestructure` - the property would refuse *and* a trigger claims the text - so `5`, `+`, undo
  gives back `5` and a second undo gives back nothing. `WouldRestructure` re-asks the property
  (`Compose` + `Type.IsValid`) because a key is read before the action runs; that duplication is
  deliberate and cheaper than threading a plan through the session.
- **A selection is never a trigger.** Wrapping a range needs a wrapper that can hold one, which is the
  intention list's job; typing over a selection stays "replace the text".
- The shipped catalogue is `ExpressionsLanguage.Triggers`, forwarded by `RulesLanguage.Triggers`:
  operators on either side become `Binary` (caret in the empty operand), and `-`/`!` on the left become
  `Unary` - declared **first**, because the negation is the narrower reading of a minus.

## How the control paints and takes input

`Projectional.Avalonia` is one `Control` (`ProjectionalEditorControl`) plus one measure
(`AvaloniaTextMeasure`). Everything that decides *what* is on screen lives in
`Projectional.Editing`, which is why the control is small enough to trust without a UI test.

- **The control owns no editing rule.** Pointer → `HitTest.Caret`, key → `EditorKeyMap` →
  `KeyRouter` → `EditorSession.Apply`. If you are about to write `if (key == ...)` in the
  control, the rule belongs in `KeyRouter` instead.
- **Input arrives in two streams.** Characters come from `OnTextInput` (one edit for a whole
  typed string, and the place IME will hook in later); navigation and deletion come from
  `OnKeyDown`. A letter therefore never appears in `EditorKeyMap` - it is not a key the
  editor routes.
- **Keys held with Control/Alt/Meta are the host's**, except for a short allow-list. `EditorKeyMap.Map`
  returns `null` for them on purpose: Ctrl+Left is a word jump in the surrounding app,
  Ctrl+Delete deletes a file in a file manager. The list is Ctrl+Left/Ctrl+Right, which
  map to `EditorKey.ControlLeft`/`ControlRight` because the plain arrows move a whole cell,
  Alt+Up/Alt+Down, which reorder the node under the caret - the vertical arrows do nothing
  else, so the host loses no gesture it had - Ctrl+Space, which opens completion,
  Ctrl+Home/Ctrl+End, which jump to the document ends, and Alt+Enter/Ctrl+., which are one
  `EditorKey.QuickFix` because no host can repair a model it cannot see. Ctrl+PageUp/Ctrl+PageDown
  are *not* in it: a page jump that also changes tabs stays the host's. Shift is not in that list
  because Shift+Tab is the editor's.
- **Escape always reaches the editor and is always consumed**, with or without a list open:
  `EditorKey.Escape` routes to `DismissCompletionAction`, which closes an open list and is a
  no-op otherwise. A host that wanted "Escape leaves the editor" therefore has to check
  `session.Completion` itself; `args.Handled` cannot tell the two cases apart, because the
  control marks every mapped key handled.
- **The arrows move by cell, Ctrl+arrows by character.** `CaretMove.Left`/`Right` land on the
  neighbouring caret place at the end/start of its text, so a press never leaves the caret in
  the gap between two cells and never creeps through a seven-character property one letter at
  a time. `CaretMove.LeftCharacter`/`RightCharacter` are separate members (not a flag) so a
  host can rebind them. Vertical moves stay geometric - see `CaretNavigation.Move`.
- **A refused edit stays unconsumed.** `e.Handled` is set from the outcome, not
  unconditionally: `EditOutcome.Rejected` (a letter typed into a number) leaves the event
  open so the host can beep or reroute it.
- **The caret blinks only inside a property cell and only while focused.** `CaretBlink` (in
  `Projectional.Editing`) holds the phase and the predicate; the control owns a
  `DispatcherTimer` that calls `Advance(blinking)` each tick and repaints only when it returns
  `true`. `Render` skips the caret bar while `IsVisible` is false. Every edit/move/click path
  goes through `Show()`, which calls `Reset()` - do not invalidate the control directly from a
  new input path or the caret can be dark at the moment of typing.
- **The pointer is an I-beam over the page and a hand over anything a press acts on.**
  `UpdatePointer(session, point)` runs from `OnPointerMoved` and resolves three shapes from the
  same geometry the paint and the press use: `Hand` over the quick-fix bulb (`BulbOnScreen`) and
  over a fold twisty (`FoldAt`), `Arrow` over the gutter, `Ibeam` over the page. The constructor's
  `Cursor = new Cursor(StandardCursorType.Ibeam)` is only the resting shape. Two rules: the shape
  is kept as a `StandardCursorType` so a move that changes nothing sets no property and allocates
  no `Cursor`, and holding a `Cursor` in a field trips CA1001 (it is disposable, and the control is
  not) - allocate on the transition instead. `Cursor` exposes no public getter for the standard
  type (only `ToString`, which prints `Hand`/`Arrow`/`Ibeam`), and the control cannot be
  constructed headlessly at all - its completion palette builds `StreamGeometry` from platform
  services - so this is verified through a real-window probe, not a unit test. Do not add a test
  that compares `Cursor.ToString()`; it would fail in the constructor before reaching the
  assertion.
- **Undo/redo report whether they did anything.** `Ctrl+Z`, `Ctrl+Shift+Z`/`Ctrl+Y` call
  `session.Undo()`/`Redo()` and only set `Handled` when the history had a step.
- **The context menu is a description, not a widget.** `EditorMenu.For(session)` (in
  `Projectional.Editing/Menu/`) is rebuilt on every opening, so no edit has to refresh it; it
  stores nothing and holds no reference to a toolkit. Its commands *are* the keyboard actions,
  so the gesture it prints cannot drift from the binding. Availability is decided before the
  entry is shown (`History.CanUndo`, "is there a character right of the caret"), and
  `EditorMenuItem.Execute` refuses a disabled entry as well, so a stale menu cannot edit.
  A host that renders this needs no editing knowledge - it maps labels, gestures, check marks
  and commands onto its own items and calls `Execute`. `EditorMenuPresenter`
  (`Projectional.Avalonia/Menu/`) is that mapping for Avalonia and is public, so the shell's
  menu bar and the control's context menu share one implementation. Two rules the control
  keeps: a right click moves the caret unless it lands inside a selection (otherwise the
  entries would describe a caret the user is not looking at), and after an entry runs the
  control repaints *and* closes the menu itself - the toolkit's own close only happens on a
  pointer release, so a keyboard-activated entry would leave it open.
- **`EditorSession` raises no event.** The host invalidates: `InvalidateMeasure()` when
  `result.Changed` (the document's size changed), `InvalidateVisual()` plus
  `ScrollToCaret()` always. There is no observable collection to subscribe to, by design.
- **`Refresh()` collapses a range selection** onto the caret - documented in
  `EditorSession.Refresh` and easy to misread as "highlights are broken" while debugging.
  Re-select after calling it.
- **Text, not glyph runs.** Each run is shaped by `AvaloniaTextMeasure.Shaped(text)` and
  cached per string; the cache is emptied wholesale at 8192 entries. `FormattedText` is used
  rather than `TextLayout` because one run is one *cell's* text, not a line.
- **`Refresh`-free layout.** A changed model rebuilds tree *and* layout in
  `EditorSession.Apply`; the control only schedules a measure/visual invalidation.
- **Only some pointer input can be synthesised in a probe.** `PointerPressedEventArgs` has no
  public constructor, so click/drag is verified through `HitTest` directly. The **wheel** is
  different: `PointerWheelEventArgs` is constructible, so `OnPointerWheelChanged` - and with it
  "the wheel moves the offset" and "a fitting document hands the event back" - can be driven
  end to end with `control.RaiseEvent(args)` and asserted on `args.Handled`. See the
  verification habits below.
- **A pointer *move* is synthesizable too, but through the base class.** Avalonia 12.1.6 has
  **no** `PointerMovedEventArgs` type; a move is a `PointerEventArgs` raised with
  `InputElement.PointerMovedEvent`, whose public constructor is
  `(RoutedEvent routedEvent, object source, IPointer pointer, Visual rootVisual, Point rootVisualPosition, ulong timestamp, PointerPointProperties properties, KeyModifiers modifiers)`.
  Passing the control as `rootVisual` makes `e.GetPosition(control)` answer `rootVisualPosition`,
  which is how "hovering the bulb gives a hand" was proven: raise the move, then read
  `control.Cursor.ToString()`. Raising the real event rather than invoking the private method
  proves the `OnPointerMoved` wiring as well as the decision. `Pointer` is ambiguous with
  `System.Reflection.Pointer` once `System.Reflection` is imported - qualify it.

## How highlighting works

Three layers, and the seam between them is the whole design:

| Layer | Owns | Types |
| --- | --- | --- |
| `Projectional.Projection` | what a cell's text *means* | `TextStyleKind`, `ICellStyler`, `CellStyler` (in `Tree/`) |
| `Projectional.Editing` | which run carries which kind | `TextRun.Style`, `EditorFrame.Build(..., ICellStyler?)` |
| `Projectional.Avalonia` | what a kind *looks like* | `SyntaxPalette` (in `Text/`), `ProjectionalEditorControl.Palette` |

Invariants that are cheap now and expensive later:

- **No colour ever enters the model, the language or `Projection`.** `TextStyleKind` is a
  vocabulary of meanings; a saved model carries no formatting, so it cannot go out of sync
  with the concept that produced it. `Projection` must not reference Avalonia (ADR 0010) and
  colour is an Avalonia type, so the layering enforces this by itself - keep it that way.
- **`ICellStyler` must be pure.** Layout measures a cell's text *before* the frame is built,
  so a styler that consulted the model, or answered differently on a second call, would
  produce a frame whose colours describe text other than what its bounds were computed from.
  Read the cell, never the node.
- **`SyntaxPalette` varies the brush only - never weight, style or size.** A bold keyword
  would be wider than the box the layout handed it, and the caret, hit test and selection
  rectangles would all sit a few pixels off. Colour is the one property that changes no number.
  Emphasis that *does* change metrics lives on the cell instead (see below).
- **Swapping a palette is a repaint, not a re-projection.** `Palette` only invalidates the
  visual; the model, tree, layout and every bound stay as they were. A change that needed a
  re-projection would mean the palette had leaked into meaning.
- **A theme carries the overlays too, not only the text.** `SyntaxTheme` is the one object a host
  passes for "make this dark", so it holds `Palette`, `Background`, `Chrome`, `Caret`, `Highlight`,
  `Gutter`, `LineNumber`, `Markers`, `Lists` (the completion and quick-fix lists), `Tip` (the marker
  tooltip) and `Bulb`. Anything painted *over* the document is read against it: a host that themes
  the page and forgets an overlay ships a white box on a dark page, which is what happened before
  `Lists`, `Tip` and `Bulb` were added. `Lists` is built lazily (`field ??=`) because a
  `CompletionPalette` carries vector icons and a `StreamGeometry` needs a rendering platform -
  constructing it eagerly made `SyntaxTheme`'s static initializer require a window and broke every
  headless test that compares themes. Keep `SyntaxTheme` free of anything that needs a platform.

### Two tooltips, one pointer

The control can show two boxes: the finding's message over a squiggle (`MarkerTip`) and a concept's
description over any node (`ConceptTip`). They share `MarkerTipPresenter` and `MarkerTip.Place`, and
they differ in exactly two ways that must not be "tidied" away:

- **The marker wins.** A finding is specific and asks for an action; a description is ambient. If both
  would apply under one pointer, only the finding's box is painted. Never paint both.
- **Only the concept tip waits.** A squiggle is a shape a user aims at, so its tip is immediate; text
  is what the pointer crosses, so a description appears after 600 ms. A delay on the marker tip would
  make a hover feel broken.

Both are dismissed through the one path that already existed (`ClearHover`, reached by pointer exit,
any edit and any caret move). The concept tip is anchored to the node's own address
(`new CellAddress(nodeId)`), not to the caret place the pointer resolved to, or it jumps while the
pointer crosses a line of a node that spans several. `ConceptTip.Of(session, nodeId)` answers `null`
for an id that no longer exists - a pointer can outlive its node, and a tooltip must never throw.

### Emphasis (bold, italic, underline, strikethrough)

A second, orthogonal axis: `TextEmphasis` is a `[Flags]` enum in
`Projectional.Language/Style/TextEmphasis.cs`. Both axes travel together on a run
(`TextRun.Style` + `TextRun.Emphasis`), but only one of them may be decided late.

- **Emphasis must be known when the tree is built, not when it is painted.** Bold and italic
  change glyph advances; a paint-time-only decoration would desynchronise drawing from the
  rectangles the caret and hit test were computed from. So it sits on `TextCell.Emphasis` and
  is threaded through every `TextCell` subclass, `CellLayouter.TextSize` and
  `ITextMeasure.Measure(string, TextEmphasis)`.
- **`ITextMeasure.Measure` has no default for the emphasis parameter, on purpose.** A host
  must not be able to forget that a cell's width depends on how it is set. Adding a default
  would silence the compiler precisely where it is useful.
- **Two sources, composed at build time.** The notation declares permanent emphasis
  (`NotationEditor.Keyword("rule", TextEmphasis.Bold)`); a host-supplied `ICellEmphasis`
  resolves per node per rebuild. `EditorTreeBuilder.BuildNode` computes
  `inherited | (resolver.EmphasisOf(node) ?? None)`, and a leaf ORs the notation's own
  emphasis on top - hence a struck-through rule still shows a bold `rule` keyword
  (`Bold | Strikethrough`).
- **A resolver's answer inherits into the whole subtree.** Name the node you care about, not
  every descendant.
- **`ICellEmphasis` is not `ICellStyler`, and lives in `Projectional.Language`, not
  `Projection`.** It is a statement about how a language reads, like the notation, so a
  language package (`Projectional.Samples`) must be able to declare one without referencing
  the projection layer. `Projectional.Samples` references only `Expressions` and `Language` -
  that is the whole reason for the file's location; do not "tidy" it back into `Projection`.
- **Nothing is stored or persisted.** The sample decorates a rule whose `outcome` is
  `Reject`; changing the outcome back removes the decoration on the next rebuild. Verified
  with a rendered probe: every word keeps its exact x and width, only the replaced outcome
  word differs.
- `MonospaceTextMeasure` ignores emphasis deliberately (a monospaced face keeps its advance
  when bold) so headless snapshots stay in agreement with the real editor. Tests use
  `WideBoldTextMeasure` (bold char = 2 units) to prove geometry actually follows emphasis.

`CellStyler.Default` reads only the cell: `ConstantCell` uses the notation's `ConstantKind`,
`PropertyCell` switches on `PropertyType.Name` (`"int"`/`"date"` -> `Number`, `"string"` ->
`Text`, bool and every enumeration -> `Keyword`), references use `ReferenceState`, an empty
child/collection is `Placeholder`, `MissingNotationCell` is `Missing`. It never emits
`Identifier` - that kind exists for a host styler that knows a cell is a name, and the
palettes are expected to be complete anyway.

Two performance facts worth keeping:

- **The shape cache is keyed on `(text, brush, emphasis)`** (`ShapedTextKey`), not on text
  alone. The same word is a keyword in one place and an identifier in another, and the same
  word set bold is a different shape again, so text alone stopped being a correct key the
  moment either axis existed.
- **Measuring shapes at the default brush.** `ProjectionalEditorControl.Palette` also points
  `TextMeasure.Foreground` at the palette's `Default` brush, so a `Default` run is painted
  from text that has already been shaped for measuring. Only genuinely coloured runs pay for
  a second shape. Do not drop that line as redundant.

`SyntaxPalette` is `FrozenDictionary`-backed with `ImmutableSolidColorBrush` values (cheap
reference equality for the cache key), `Light`/`Dark` presets, `Create` (requires `Default`,
rejects a null brush and a kind mentioned twice - checked by hand, because
`ToFrozenDictionary` will not tell you which of two brushes won), `BrushOf` (falls back to
`Default`) and `With` (returns a new palette, never mutates).

## How completion works

Ctrl+Space offers the concepts that could fill the slot at the caret, and an enumerated property
offers its values the moment the caret arrives. The list is **view state**, like the
representation choice: it lives on `EditorSession.Completion`, is never persisted and never
becomes its own undo step. `docs/architecture/completion.md` is the contract; the invariants
that are easy to break are these.

- **Proposals come from the model, not from text.** For a slot, the caret must sit in an
  **empty** one; the slot declares the concept its children must conform to; every non-abstract
  concept below it in the registry, in declaration order, is one entry. There is no word list,
  no frequency ranking and no learned order - declaration order is the language author's
  ranking, and the first entry is what Enter would have created anyway.
- **`CompletionSlot.At` is the only place that decides "can this caret be completed"**, and it
  accepts exactly three cell shapes: an empty `ChildCell`, an empty `CollectionCell`, and a
  `PropertyCell` whose type names its values. A filled slot or a constant gets nothing.
  Substitution (replacing a filled child) is deliberately out of this slice.
- **A type that names its values is the second kind of proposal.** `PropertyType.Symbols` is
  empty by default and an enumeration answers with its symbols in declaration order, so
  `CompletionSource.Values` needs no registry walk. `CompletionKind.Value` tells the host to
  label the item with the symbol verbatim instead of shortening it like a concept key, and
  accepting one is `EditContext.SetProperty` - the caret does not move, because a value replaces
  the text it stands for.
- **The value list opens on arrival, and only on arrival.** `EditorSession.Apply` compares the
  caret before and after a non-completion action and offers when it moved; `Select` offers
  unconditionally. The constructor and `Refresh` never offer, so opening a document and undoing
  stay quiet. This is not "auto-open while typing" - a request to type does not open anything.
- **Any non-completion action closes the list.** `EditorSession.Apply` hands the list to the
  action through `EditContext.Completion` only when the action *is* a `CompletionAction`, and
  drops it afterwards. So moving the caret, typing somewhere else and undoing all dismiss the
  list without any of them knowing completion exists. Do not add a second dismissal path.
- **Typing while a list is open filters, it does not edit.** `KeyRouter.Type(session, text)`
  routes to `NarrowCompletionAction` instead of inserting; Backspace with a non-empty prefix
  is `WidenCompletionAction`, and with an empty prefix it falls through, which closes the list.
  This is why the control must call the **session-aware** `Type` overload - the plain one would
  type into the model behind an open list. Over a value list this is not a nicety but the
  feature: every prefix of a symbol is an illegal value, so a filter is the only way a user can
  reach `Accept` from `Review`.
- **An empty match keeps the list open.** `EditorSession.Completion` stays non-null so Backspace
  can widen it again; the host hides a zero-row list instead. Hiding and closing are different
  states and the distinction is what makes backspacing past a wrong prefix feel right.
- **Icons are host `IImage`s, and the built-in set is vector geometry.** `CompletionItem`
  carries no palette and no colour, exactly like `TextStyleKind`: `CompletionKind` is a
  vocabulary of meanings and `CompletionPalette` maps it to a `CompletionIcon(IImage, size)`.
  The bundled icons are paths drawn by `GeometryImage` - a ~30-line `IImage` that applies its
  own translate/scale/clip - **not** `DrawingImage`.
- **Never go back to `DrawingImage` for these.** Measured on Avalonia 12.1.6: consecutive
  `DrawImage` calls with a `DrawingImage` came out translated a few pixels further right each
  time and then blank, and the icon column of every row after the first was empty on screen.
  `GeometryImage` and `RenderTargetBitmap` both paint correctly. A host that supplies a
  `Bitmap` (a real PNG) is fine - the bug is specific to `DrawingImage` reused across draws.
- **The list is painted in the editor's own surface, not in a toolkit popup.** The control is a
  single `Control`, and `Popup.Open()` needs a visual-tree parent it does not have.
  `CompletionPresenter` is a plain painter (`Measure`, `Render`, `RowAt`) and the control
  positions it from `CaretBounds` minus the scroll offset, flipping above the caret when it would
  overflow the bottom and clamping both axes. Pointer presses are tested against `RowAt`
  *before* the document hit test, and a clicked row runs `SelectCompletionAction` then
  `AcceptCompletionAction` - the same actions the keyboard would run.

## How the clipboard works

`Ctrl+A`/`Ctrl+C`/`Ctrl+X`/`Ctrl+V` select all, copy, cut and paste. Copy, cut and paste are
**text inside one property cell**; select-all widens. `docs/architecture/clipboard.md` is the
contract. The clipboard is text and not structure on purpose: a structural copy (a subtree, a
slot's children) is a different feature with a different payload and a different paste rule,
and it is scheduled for M3.

- **The seam is `ITextClipboard`**: one `string? Text` property and one default
  `Task RefreshAsync() => Task.CompletedTask`. Nothing else. `null` means "the clipboard holds
  no text", so an empty clipboard is not an empty string.
- **Read and write are synchronous, and must stay synchronous.** A key arrives synchronously and
  an action applies synchronously; an asynchronous contract would put an `await` into every
  action that touches text. A platform whose clipboard can only be read asynchronously caches
  instead, and `RefreshAsync` is the hook that lets it pull the platform's text at a moment
  where waiting costs nothing - focus, or opening the context menu.
- **`RefreshAsync` returns `Task`, not `ValueTask`.** The callers that cannot await fire and
  forget, and a discarded `ValueTask` is an analyzer error because it may be pooled. A `Task`
  can be dropped without pretending anything was consumed. Do not "modernise" this signature.
- **`InMemoryClipboard` is what a session gets when the host supplies none.** It keeps a string
  and nothing else; its `RefreshAsync` is the inherited default. Tests use it. A host with a
  real clipboard adapts it - `AvaloniaClipboard` in `Projectional.Avalonia/Input/` is that
  adapter, and it caches the platform's text so a synchronous `Ctrl+V` sees what another
  application put there.
- **`SelectedText.Of(context)` is the single answer to "what does the selection say".** It is
  `internal`, clamps to the cell's length, and returns `null` unless the selection is inside one
  `PropertyCell`. Every text action goes through it; a second reading of the selection is a bug
  waiting to happen. Because `EditorSession.Select` already clamps, the clamp is unreachable
  through the public API - cover it through `CopyAction`/`CutAction`, not by calling it.
- **The four actions are in `Projectional.Editing/Clipboard/`.** `SelectAllAction` climbs the
  ladder (see the editing section above) rather than selecting one cell. `CopyAction` writes the
  text and returns `EditResult.Unchanged` - a copy never changes the document. `CutAction`
  writes, then applies `DeleteTextAction(DeleteDirection.Forward)`. `PasteAction` hands the text
  to `TypeTextAction`, so a paste gets exactly the same validation and the same refusal as
  typing; an empty or null clipboard is `Unchanged` ("nothing to do"), never a rejection.
- **A paste never coalesces.** `PasteAction.CoalescingKey` is `null` (and `CutAction`'s too), so
  a paste is its own undo step and never merges into the typing around it. `ToString()` returns
  `"paste"`, which is what the undo label shows.
- **The keys are a third exception to "Control belongs to the host".** `EditorKeyMap.Map` returns
  `null` for Control combinations except the documented ones, and `A`, `C`, `X`, `V` join
  `Left`/`Right`/`Space` in that list. Shift does not change them: `Ctrl+Shift+C` is still Copy.
  A bare letter is never in the key map - it is text input, not a routed key.
- **The menu decides availability from state, not from a flag.** `EditorMenu.For` lists
  `Select All`, `Copy`, `Cut`, `Paste` after the edit entries. `Select All` has **no** predicate
  - the ladder always has a rung left until the document is selected, and greying it out would
  make the node rung unreachable. `CanCopyText(session)` is true only when the selection has
  text *and* the caret resolves to a `PropertyCell`, so Copy and Cut go grey while a node
  selection is in force; they share one predicate because they are the same read.
  `CanPasteText(session)` is true only when `session.Clipboard.Text` has length.
- **The Avalonia adapter resolves the platform clipboard lazily and swallows its failures.** A
  control that is not yet in a visual tree has no `TopLevel`, and a platform clipboard that
  refuses must not take the editor down with it - a copy that cannot reach the platform leaves
  the cached text alone. The control refreshes the clipboard on focus and before opening the
  context menu, and intercepts the platform's own paste so `Ctrl+V` goes through
  `PasteAction` (one undo step, one validation path) instead of the toolkit's default.

Deliberate M1 limits, so nobody "fixes" them into the wrong milestone:

- **Text only, one cell.** Copying across cells, copying a subtree, and pasting a structural
  fragment all wait for M3. A multi-cell selection degrades to caret behaviour for every text
  action, exactly as it does for typing. `Delete` is the exception: `DeleteSelectionAction`
  removes the nodes a range fully covers in one transaction, so `Ctrl+A` twice then `Delete`
  removes the node - it used to do nothing, which is what made the editor feel broken.
- **No rich payloads.** No HTML, no images, no custom MIME types. The model is the source of
  truth and a formatted payload would be a second representation to keep in sync.
- **Cross-editor fragment paste is not supported.** Two editors in one process share the text
  through the platform clipboard, but not their identity or their references.

## How the standalone app fits

The repository ships two front ends and **one** public surface:

- `Projectional.Avalonia` is the embeddable control. Hosts reference it (which brings `Core`).
- `Projectional.Workbench` is the standalone desktop editor. It references **only**
  `Projectional.Avalonia` and has **no** `InternalsVisibleTo` entry, on purpose
  ([ADR 0011](../../../docs/adr/0011-standalone-editor.md), `docs/architecture/standalone.md`).

Rules that follow from that, and are easy to break by accident:

1. **Never add a `ProjectReference` from `Projectional.Workbench` to `Core`-adjacent internal
   projects** (`Language`, `Projection`, `Editing`, `Semantics`, `Persistence`). It used to
   reference `Semantics`; that was removed when ADR 0011 was written. If the app seems to need
   an internal type, the public API is missing something — widen `Projectional.Avalonia`'s
   surface instead.
   **Transitive types are not a reference.** The app names `MarkerSet` and `Diagnostic`, which
   reach it through `Projectional.Avalonia`'s own public `Markers` property - a public surface
   has to mention the types it is written in, so `Avalonia` must reference `Editing`, which
   must reference `Semantics`. The layer diagram puts `Semantics` in the same band as
   `Projection`/`Editing`, i.e. *below* Avalonia and Workbench, so the arrow is downward and
   legal. What is forbidden is the app *reaching past* the control into a layer it does not
   sit above; using a type that a public member is typed with is what a reference consumer is
   for. Rule of thumb: if the app can express the feature with only members Avalonia makes
   public, it is allowed; if it needs a `ProjectReference` to see a type, it is not.
2. **Never grant the workbench `InternalsVisibleTo`.** It is the reference consumer; an
   exemption makes it prove nothing.
3. **Never put a feature in the app that a host cannot reach.** Shell concerns that legitimately
   belong to the app: windows, tabs, menus and dialogs, recent files, settings, file watching,
   dirty prompting, command registry and shortcut table, command-line parsing.
4. **The shell is plain classes plus wiring.** Documents, `CanExecute` and lifecycle live in
   non-Avalonia types so they are testable headlessly; only `App`, `MainWindow` and the two
   controls touch Avalonia. A UI test harness (`Avalonia.Headless.XUnit`) is added when there is
   wiring worth asserting on, not before.
5. **The dirty flag is derived from the change journal**, never a boolean the app sets on edit and
   clears on save — otherwise undo back to the saved state still claims "modified".
6. **Undo belongs to the session, not to the tab.** A document keeps its session while open and
   disposes it when the document closes; forgetting the dispose leaks undo state.
7. **View ▸ Representation is built from the active document, not from the registry.** It binds
   `EditorSession.Alternatives()` (concepts the document uses that have more than one variant) to
   one submenu per concept with radio items, and clicking one calls `ChooseVariant` + `Refresh()`
   on that tab's control. The menu is refreshed from `UpdateStatus()`, holds no state of its own
   and must stay disabled when there is nothing to switch.

8. **The shell is a library, not only an app.** Its content is `ShellView` (a `Control`),
   `ShellWindow` wraps it, and the standalone app is that window with every capability and no
   language lock. A host embeds the shell locked to one language, with its own `IShellStorage`
   and `IShellDialogs` ([embedded-shell.md](../../../docs/architecture/embedded-shell.md),
   [ADR 0015](../../../docs/adr/0015-shell-is-a-library.md)). So write shell code as if a host
   already owns it: no statics holding documents or preferences, no `Application.Current`, no
   settings file, and never a capability enforced only by hiding a menu entry - the command
   registry refuses it.

Scope by milestone, so the app does not compete with the libraries: M1 = open project, tabs,
edit, save, dirty prompt. M2 = model browser, problems panel, single-file build. M4 = output
panel. M5 = language browser, live preview, language creation (the MPS moment). M6 = the shell
as an embeddable, language-locked component. M7 = installers and file association.

Work that touches any of this also touches a doc; the app's plan lives in
`docs/architecture/standalone.md`, and the milestone scope in `docs/roadmap.md` M1/M2/M4/M5/M6/M7.

## How the tests are organised

`tests/Projectional.Core.Tests` is currently the only test project. It references `Core`,
`Language`, `Persistence`, `Projection`, `Editing` and - for the one pure type in it -
`Avalonia`. `TestLanguage.cs` (five concepts: `Test.Tag`,
`Test.Rule`, `Test.Body`, `Test.Step`, `Test.Note`) and `TestNotations.cs` (their notation, in
the same style a language author would write it) exist once. If a second test project
ever needs that language, promote it to a `Projectional.TestLanguage` library - do not copy
it. **Baseline is 1280 tests**; anything below that means tests were deleted.

Two *real* languages sit next to the fixture and are the executable proof that the language
layer composes:

- `Projectional.Expressions` - an abstract `Expression` plus seven concrete concepts
  (`IntLiteral`, `BoolLiteral`, `StringLiteral`, `Name`, `Binary`, `Unary`, `Parenthesized`),
  the three literals declared `isOfferedInCompletion: false`.
  Four of the seven are what completion offers for an empty `condition`, and four is what a
  test asserting the list length should expect.
  A host borrows its declarations with `[.. ExpressionsLanguage.ConceptDeclarations, .. own]`;
  it never inherits from them. Operators are `PropertyType.Enum` symbols so the typed value
  *is* the notation.
- `Projectional.Samples` - the toy rules language (`Ruleset`, `Rule`, condition over
  `Expressions.Expression`) and `PayrollSample`, the shared document used by notation tests
  and (later) the standalone app.

Every expression notation must stay a **single `Line`**, because `ChildInline(slot)` refuses
anything else - a two-line expression concept silently breaks inline children.

- **The corpus is the file format.** `tests/Projectional.Core.Tests/Corpus/*.mps` are
  documents the writer produced once and that now have to load and save back to the same
  bytes. They are copied to the output directory by a `<None Update="Corpus\**\*.mps"
  CopyToOutputDirectory="PreserveNewest" />` item. **Never regenerate a corpus file to make
  a test pass** - a byte that moves is a wire-format change and needs an ADR. The files must
  stay LF, BOM-free and without a trailing newline.
- **Notation is tested as text, not pixels.** `Projectional.Projection/Snapshot/` holds
  `SnapshotGrid` (a character grid that refuses to draw past its edge rather than clipping)
  and `LayoutSnapshot.Render(model|node, notations, width)`, which lays the cells out and
  draws every text-bearing one where layout put it. So a notation test asserts a whole
  drawing: layout bugs show up as text. Both types are `internal` per ADR 0010, reached from
  tests through `InternalsVisibleTo`.
- **CI runs both platforms.** `.github/workflows/ci.yml` builds Release and tests with
  `--no-build` on `windows-latest` and `ubuntu-latest` (`fail-fast: false`), plus a
  Linux-only job that runs just the corpus so a format regression names the contract that
  broke. The SDK comes from `global.json`, so CI and this machine compile identically.
- **xunit.v3 on Microsoft.Testing.Platform**, so a test project is an executable.
  `--filter-class "*Name"` works; a comma-separated list does **not** ("wildcards may only
  be at the beginning and/or end"). Exit code 8 means no tests ran, 2 means failures - and
  on this console the run summary is printed in German.
- One type per file applies to tests too: `TemporaryDirectory.cs` is its own file, not a
  nested class.
- See `docs/architecture/testing.md` for what each layer is expected to prove.

## Language level (explicit user requirement)

**Always use the newest C# that the pinned SDK offers; never settle for the syntax the
project already happens to use.** `Directory.Build.props` sets `<LangVersion>latest</LangVersion>`,
which on SDK 10.0.403 resolves to **C# 14** (confirm with `dotnet build -v:diag` and look for
`_MaxAvailableLangVersion`). Do not pin a numbered `LangVersion` and do not enable `preview`
features - `latest` already means "the newest shipped version", and `preview` would make the
build depend on unreleased behaviour.

Features that are in active use and should be the default choice:

- **Collection expressions** `[]`, `[a, b]` for every list, array, span and dictionary creation.
- **`params ReadOnlySpan<T>`** instead of `params T[]` for variadic APIs that only read their
  arguments. See `PropertyType.Enum` and `EnumPropertyType` - the compiler passes string
  literals without allocating an array.
- **The `field` keyword** to remove a hand-written backing field, most often for a lazily built
  read-only view: `public IReadOnlyList<Node> Roots => field ??= _roots.AsReadOnly();`
  (`Model`, `Project`). Do not reach for `field` when the property is set from a constructor
  and the compiler would then report CS8618 - keep an explicit field there.
- **Primary constructors** on small types that only capture their arguments.
- **Pattern matching** (`is { } x`, property patterns, list patterns) instead of chains of
  null checks and casts.
- **`ArgumentNullException.ThrowIfNull`** and the other `ArgumentException` throw helpers.
- **`readonly record struct`** for small value types; `sealed class` otherwise.

If a newer compiler ships a feature that replaces something here, add it to this list in the
same commit that starts using it.

## Build and verify

`global.json` pins SDK **10.0.403** with `rollForward: latestFeature` because SDK 11 is the
machine default. Do not remove it.

```powershell
dotnet build
dotnet test
```

- All shared properties live in `Directory.Build.props`; all package versions in
  `Directory.Packages.props`. A `.csproj` should not repeat `TargetFramework` and should not
  carry a `Version`.
- Warnings are errors, code style is enforced during build, and analysis runs at
  `latest-recommended`. Fix the cause; do not add `NoWarn` to get a green build.
- Tests use xunit.v3 on Microsoft.Testing.Platform, so a test project is an executable.

## Windows and PowerShell pitfalls in this environment

- **No heredocs.** `git commit -F -` with `<<'EOF'` produces a parser error. Use one `-m` per
  paragraph instead, or a single-quoted here-string:
  `@'` newline, content, newline, `'@ | ...`.
- **PowerShell 5.1 semantics.** No `&&`, `||`, `??`, `?.`. Chain with `;` and gate with
  `if ($?) { ... }`.
- **The repo path contains spaces.** Quote every path.
- **Line endings drift.** `core.autocrlf=true` is set globally, and the file-writing tools plus
  `dotnet new` emit CRLF. `.gitattributes` forces `eol=lf` in the index, so commits are
  correct even when the working tree is not. To renormalize a dirty working tree, **delete the
  tracked files and run `git checkout -- .`** - `git reset --hard` and `git checkout-index -a -f`
  both skip files whose stat looks clean. Diagnose with `git ls-files --eol`.
- **Write LF text explicitly** when authoring files:
  ```powershell
  [IO.File]::WriteAllText($path, ($text -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
  ```
- **A member named like an inherited Avalonia member is a compile error, not a shadow.**
  `Theme` on a `Control` collides with `StyledElement.Theme` (CS0108), so the palette property
  had to be called `Palette` and the type `SyntaxPalette` rather than `SyntaxTheme`. Before
  naming a new public property, check the base class first - renaming later touches the tests
  and the prose.
- **Non-ASCII in tool output is unreliable.** The console mangles UTF-8, so an em dash typed
  into a file can be silently stored as three characters (U+00E2, U+20AC, U+201D) instead of
  the single U+2014 that was meant, and nothing shows the difference. Never diagnose encoding
  by eye: read the character codes
  (`$t.Substring($i, $n).ToCharArray() | ForEach-Object { '{0:X4}' -f [int]$_ }`) and expect
  `2014` for an em dash. After authoring prose, scan the tree for that triple, building the
  pattern from code points so the scan never matches itself:
  ```powershell
  $bad = [string][char]0x00E2 + [char]0x20AC
  Get-ChildItem -Recurse -File -Include *.cs,*.md,*.csproj,*.axaml,*.yml src,tests,docs,.github |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
    Where-Object { [IO.File]::ReadAllText($_.FullName).Contains($bad) }
  ```
- **`git show file > out` writes UTF-16 in PS 5.1**, so byte-level checks on committed files
  are useless through it. Use `runInTerminal` for a clean `git diff`, or check the working
  tree instead.
- **A local function named after a type shadows it.** A probe helper called `Key(...)` broke
  every later `Key.Right` with CS0119. Name helpers `Combo`, `Press`, `Chord`.
- **`Stop-Process -Id` needs a literal PID** in this environment; a `$pid` variable makes the
  whole command be refused. Design one-shot probes so they close their own window and exit.


## Do not

- Do not modify `LICENSE`. It must stay byte-identical to the canonical Apache-2.0 text
  (SHA-256 `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`).
- Do not "improve" the XML format without a new ADR and a migration - existing `.mps` files
  would stop round-tripping byte-identically.
- Do not put Avalonia types, `Dispatcher`, or `Control` references into `Projection` or `Editing`.
- Do not introduce reflection or source generators for language definition.
- Do not reorder children or renumber IDs during save.
- Do not make `ITextClipboard` asynchronous. A synchronous key needs a synchronous clipboard;
  `RefreshAsync` is the only async seam and it stays fire-and-forget.
- Do not preview an edit by applying it to the live model and rolling back, and do not give each
  action a "describe" method. A preview applies the real action to a **discarded copy in its own
  project** and projects that ([ADR 0017](../../../docs/adr/0017-preview-is-a-discarded-edit.md),
  [intention-preview.md](../../../docs/architecture/intention-preview.md)). Node ids are unique per
  project, which is why the copy cannot share the live one.

## Verification habits

- Markdown links **and anchors** must both resolve. Anchors break silently when a heading is
  renamed, so validate `#fragment` targets against generated heading slugs, not just file paths.
- The persistence test is byte equality after a full round trip, not "it deserialized".
- Whenever a change touches the model, check the six invariants in
  `docs/architecture/model.md` still hold: unique ID per project, exactly one parent or the
  root, child slot conforms to the concept, no reference into the referencing node's own
  subtree unless allowed, removing a node clears incoming references, and every property value
  parses. They are enforced on every mutation (`ModelInvariantException`), not merely asserted
  in debug builds, so a green test run means the checked ones hold - the structural ones
  (invariants 1 and 2) still need a thought.

### Runtime probe: how UI code is verified

There is no `Avalonia.Headless` package in the cache and no test harness for the control, but
**a real Avalonia window runs on this machine**. So the verification technique is a throwaway
project *outside* the repository:

1. Create `%TEMP%\probe` with a `WinExe` `net10.0` project referencing `Projectional.Avalonia`
   and `Projectional.Samples` by absolute path (never add the probe to the solution).
2. Host the control in a `Window`, and in `Opened` run an `async` sequence: raise inputs, then
   `await Task.Delay(40)` a few times to let layout and render settle between steps.
3. Read state back through the public surface (`EditorSession.Selection`, `.Layout.Size`,
   `EditorFrame.Build(...)`, `OffsetX/Y`) and rasterise proof with
   `new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96))` + `bitmap.Render(control)`
   + `bitmap.Save(stream)` (the `Save(string)` overload is obsolete: CS0618).
4. Append findings to a `List<string>`, write it to a log file, then `Close()` the window so the
   process exits on its own - a probe that stays open blocks the shell call.
5. Look at the PNGs. Geometry lines can look right while the picture is wrong.

Facts that came out of doing this, and cost time to rediscover:

- **Key and text events are public and raisable**: `new KeyEventArgs { RoutedEvent =
  InputElement.KeyDownEvent, Key = ..., KeyModifiers = ..., Source = control }` and
  `new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "..." }`, then
  `control.RaiseEvent(args)`, then read `args.Handled`.
- **The wheel is raisable too**, even though a press is not. `PointerPressedEventArgs` has no
  public constructor, but `PointerWheelEventArgs` does:
  `new PointerWheelEventArgs(control, new Pointer(1, PointerType.Mouse, true), control,
  new Point(20, 20), 0, new PointerPointProperties(), KeyModifiers.None,
  new Vector(0, -1))` - `-1` is one notch down, `Shift` makes it horizontal, and `Handled`
  afterwards is the assertion. That is how `ScrollTo`'s "did the offset move" answer is tested
  from the outside.
- **`RaiseEvent` bubbles to ancestors**, so `Handled` can be `true` because `TopLevel` or a
  parent handled it, not because the control did. To isolate the control, raise the same event
  at a control that is in **no** visual tree.
- **A pointer move is raisable as a plain `PointerEventArgs`** raised with
  `InputElement.PointerMovedEvent` - there is no `PointerMovedEventArgs` in 12.1.6 - passing the
  control as `rootVisual` so `GetPosition` answers the point you gave. Reading
  `control.Cursor.ToString()` afterwards (`"Hand"`, `"Arrow"`, `"Ibeam"`) is how the hover shapes
  are asserted.
- **A local method named after a type breaks the file**: `void Key(Key key, ...)` makes every
  later `Key.Left` a method group (CS0119). Name the helper `Combo` or `Press`.
- **Widgets are laid out lazily**, so a probe must `await Task.Delay` *after* the window opens
  before `Bounds` has a real size; `InvalidateVisual()` alone does not run a render pass in a
  probe - `RenderTargetBitmap.Render` forces one.
- **Attach the `Opened` handler before `base.OnFrameworkInitializationCompleted()`.**
  `Application.OnFrameworkInitializationCompleted` is what shows `MainWindow`, so a handler
  subscribed after the base call never fires and the probe exits having written nothing. The
  shape is `Application.Prepare(...)` before the base call, window content assigned there too,
  and the whole sequence wrapped in `try`/`catch` that logs the exception - a silent empty log
  means the handler did not run, not that the assertion failed.
- **Sampling a glyph colour needs the extreme pixel.** The centre of a glyph is
  antialiased against the background, so a mid-glyph sample returns a blend that matches
  neither the palette nor the background. Per run, take the pixel **farthest from the
  background colour** in that run's rect; that pixel is the brush. This is also how the
  "enumeration symbols are keywords" consequence was found and confirmed: `>` and `Accept`
  came back blue, not punctuation-grey and not identifier-black.


### Avalonia 12.1.6 facts worth having

- `DrawingContext.DrawText(FormattedText, Point)`, `FillRectangle(IBrush, Rect, float)`,
  `FormattedText(string, CultureInfo, FlowDirection, Typeface, double, IBrush)` with
  `WidthIncludingTrailingWhitespace`, `Height`, `MaxTextWidth`, `Trimming`.
- `Control` has **no** `Background`/`Foreground` (only `TemplatedControl`/`TextBlock` do), so
  declaring them on the editor control is legal and does not hide anything.
- **Namespace trap**: inside `namespace Projectional.Avalonia`, an unqualified
  `Avalonia.Media.X` resolves `Avalonia` to `Projectional.Avalonia` and fails to compile. Keep
  `using` directives at the top of the file and use unqualified names in the body.
- `FontFamily.Default`, `Typeface(FontFamily, FontStyle, FontWeight, FontStretch)`,
  `Brushes.Black/White`, `Color.FromArgb`.
- **`FormattedText` has no `textDecorations` constructor parameter** in 12.1.6. Underline and
  strikethrough come from `formattedText.SetTextDecorations(TextDecorationCollection)` after
  construction, which is what `AvaloniaTextMeasure.DecorationsOf` does. Weight and style, by
  contrast, *are* constructor state (`Typeface`) - which is exactly why emphasis has to reach
  the measure and cannot be a paint-time add-on.
