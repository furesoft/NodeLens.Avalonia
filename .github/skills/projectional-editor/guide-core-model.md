# Core model: change recording, undo, persistence, find usages

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

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

## How a model is copied and compared

`Model.CopyTo(Project target, string? name = null)` (`src/Projectional.Core/Model/Model.cs`) and
`ModelDiff.Of(before, after)` (`src/Projectional.Core/Diff/`) are slice V1 of the intention preview -
the sandbox and the answer the ghost is drawn from.

- **The copy keeps every node id**, and that is the contract rather than a detail: an edit action
  addresses ids and slots, so the same action value means the same thing in the copy as in the
  original. Values, reference targets and fallback text, child order and root order come with it.
- **The copy needs its own `Project`.** `Project.Register` asserts id uniqueness, so copying into the
  project the model came from throws `ModelInvariantException` - that is the correct answer, not a gap.
- **`Model.InsertCopy(source, parent, slot, index)` is the copy that stays in one model**, and it must have
  *new* identities: the `CopyTo` walk preserves ids because a preview addresses nodes by id, while a copy that
  shares a project cannot, because ids are unique per project. The two are siblings rather than one routine with
  a flag - "same ids or new ones" is the only thing either caller cares about. `InsertCopy` is `CopyTo` in
  everything else (values, reference targets and resolve info, child order, the whole subtree, attached before
  filled) and runs in one transaction, so a duplicated node is one undo step. A reference between two copied
  nodes still points at the *original*, which is the only reading that does not need to know what a language
  means by its references.
- **Attach before filling.** `CopySubtree` creates the node with `CreateDetached(id, concept)`, hangs
  it into its place, and only then writes properties, references and children. A bottom-up copy would
  leave registered nodes that nothing points at, and the failure path could not clean up. On failure
  `Abandon` deletes the built roots with `record: false` and calls `Project.DiscardModel`, because
  `CreateDetached(id, concept)` deliberately journals no rollback (it exists for loading).
- **`Node.ChildrenInDocumentOrder()` yields the *direct* children only** - despite the name. A full
  document walk recurses (see `ModelDiff.SelfAndDescendants` and `EditorSession.SelfAndDescendants`).
  Assuming it was deep cost two failing tests; do not reintroduce it.
- **The diff walks `Model.Roots`, never `Project.Nodes`**: a project holds several models, and the
  index would report documents that are not being compared.
- **A diff is structural, matched by id**: `Added`, `Removed` (with the node's name in `OldValue`),
  `Moved` (with `NodePlace` before and after), `PropertyChanged`, `ReferenceChanged`. One entry per
  change, in document order of the later model, then the removals. Same id on a *different concept* is
  `Removed` + `Added`, because ids are unique per project, not per universe.
- **`Model.Nodes` is the walk a diff, a budget or a find wants.** `Project.Nodes` is the whole project
  (several models); `Model.Nodes` is this document, depth first.
- **`PreviewSandbox` (in `Projectional.Editing/Preview/`) is the applied path**: `Open(session)` copies
  the *model* into a fresh `Project`, opens a second `EditorSession` over the copy with the live
  session's notations, measure, emphasis, choice, clipboard, typing rules, triggers and caret, and
  `Apply(action)` runs the real action there, answering the `ModelDiff` against the live model.
- **`null` means "cannot be previewed", an empty list means "it ran and changed nothing".**
  `Open`/`Apply` catch broadly on purpose - best effort is the contract (ADR 0017), and an action that
  captured live `Node` objects instead of addresses is exactly the failure that has to become "no
  preview". Do not narrow those catches to "tidy up" the analyser; there is nothing to rethrow to.
- **The sandbox never touches the live session**, so a preview is not undoable and produces no undo
  step; `Open` also refuses a document above `NodeBudget` (20 000, overridable for tests) rather than
  paying for the copy.
- **The hover ladder is a state machine, not three timers** (`PreviewDelay`, `PreviewStage`,
  `PreviewState` in the same folder; slice V3). `PreviewDelay` holds highlight/description/preview/grace
  as one immutable value whose rungs may not be out of order (`IsValid`/`Validate`; a host builds it
  from settings, so construction does not throw). `PreviewState` takes `Hover(entryId)`, `Leave()`,
  `LeaveList()`, `Invalidate()` and one `Tick(elapsed)` per timer tick and answers `Stage` - the control
  owns the timer, exactly like `CaretBlink`. The entry key is the `Intention.Id`, so the machine knows
  nothing about intentions or a toolkit.
- **`PreviewMarks.Of(diff, ghost)` is the only place a diff becomes cells** (`CellChange`/`CellChangeKind`
  beside it). A node difference marks every cell whose `CellAddress.Owner` is that node; a slot difference
  marks the one cell that shows the slot. A removal has no cells left, so it marks the parent's slot cell
  with the removed text in `OldValue` - only the outermost node of a removed subtree has such a place, so
  the diff may name three removals and the ghost show one mark. **Counts count nodes, marks count cells**:
  a badge saying "seven added" because a rule is drawn with seven cells would count the renderer. A
  difference whose node is not drawn is counted and not marked. `CellBudget` (2 000) truncates and
  `Truncated` says how many were left out; `PreviewBadge.Of(label, marks)` owns the sentence, because it is
  user-facing grammar: one kind reads as a total ("4 changes"), several are broken down ("2 added, 3
  removed"), one is singular, and truncation is appended.
- **`GhostFrame.Build(sandbox, marks, label, viewport, offsetX, offsetY)` is the frame a ghost paints**
  (`Preview/`): the copy's `TextRun`s, the `PreviewMarks` to look up per run, and the badge sentence. It takes
  the **live page's offsets** - that is what anchors the preview to the line the user is looking at - and it
  carries **no caret and no selection**, because a preview is not editable and must not look as if the document
  had a caret somewhere else. `EditorFrame.TextRunsOf` is `internal` for this: the ghost is a frame of the same
  vocabulary, not a second renderer. `PreviewSandbox.Measure` exists for it (drawing with another measure would
  put glyphs outside the boxes the layout reserved).
- **"Unchanged" is not a mark, it is the absence of one**: `GhostFrame.IsDimmed(run)` is exactly "no mark names
  this cell", which is "the marks read first" written as code rather than as a palette decision.
  `PreviewMarks.ChangeAt(address)` is the indexed lookup behind it, and the **first** mark for an address wins -
  every removal out of one slot names that slot's cell.
- **`ProjectionalEditorControl.Ghost` is host-set, and while it is set the control paints only that** - tints,
  dimmed text, badge, and none of the page's furniture (gutter numbers, bulb, selection, caret, lists, tips). The
  host decides *when*, so the hover ladder stays out of the control; `GhostPresenter` does the drawing and
  `PreviewPalette` (in `SyntaxTheme`, like `Tip`/`Markers`) names the colours. **`PreviewPalette` is a theme
  member for the same reason the other overlays are**: a themed page with an unthemed ghost is a light sheet on a
  dark background.
- **Ghost runs are layout coordinates.** `GhostPresenter` converts with `ghost.Viewport.ToViewport(...)` plus the
  gutter; passing layout coordinates straight to `DrawingContext` puts every tint in the corner (that happened,
  and only the rendered picture showed it).
- **`EditorSession.ActionFor(Intention)` is how a preview gets its edit without applying it** - the same
  `Intention.Create(CreateIntentionContext())` call the apply path makes, answering `null` for "not offered" and
  for "nothing to do" alike. Never add a second way to ask an intention for its action: a preview that ran a
  different action would be a preview of something else.
- **The ladder's value is in its interruptions, so do not "simplify" them away**: a different entry
  starts from nothing (a preview is an answer about *that* edit); the *same* entry again continues the
  clock (that is the bulb-to-list gap, and restarting there means a preview is never reached); leaving an
  entry cancels at once but leaving the *list* survives `Grace` with the clock **paused**; and
  `Invalidate` remembers the entry so the ladder does not restart on the next tick - only leaving and
  returning asks again. The stage follows elapsed hover time, so a late tick reaches the rung it passed.
- **`OfferPreview` (`Projectional.Editing/Preview/`) is the only join of the three**, because a preview is a
  property of the *list*: the bulb, the popup and a submenu all show one list and must not each write their own.
  It opens the copy **lazily on the preview rung** (a pointer crossing a row must not copy a document), keeps a
  frame until its viewport/offsets/styler change (a repaint per caret blink must not re-project), and on every
  failure - no action, an edit that throws on the copy, over the copy budget - silences the ladder through
  `PreviewState.Invalidate` instead of retrying each tick.
- **The control owns the clock and nothing else about the ladder.** A 30 ms `DispatcherTimer` runs while an entry
  is hovered and stops when none is; `ShowPreview()` is the one place `Ghost` is set or cleared, and every
  dismissal path (an edit, a caret move, a closed list, a republished `Markers`, a detached control, the pointer
  leaving) reaches it. Hovering a row also moves the list highlight, because rung zero *is* the list's hover
  state; a pointer between rows gets `LeaveList` (the grace), never `Leave`. Only the ghost the control set is
  taken back, so a host's own `Ghost` survives the ladder ending. The description rung is painted under the list
  box rather than under a row - a row's screen position depends on the window the list scrolled to.

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

## How find usages works

`ReferenceIndex` (`Projectional.Core/References/`) is the reverse of a reference: `UsagesOf(node)` answers every
slot that points at it, across every model of the project, as `ReferenceUsage(Owner, Slot, Target)` in document
order - models in project order, nodes depth first, reference slots in declaration order. It is public because it
is a model fact: a rename must rewrite every usage before it changes a name. Its invariants are the name index's:

- **Per project, not per model**, because a reference may cross a model boundary but never a project one.
- **Invalidated by `Model.Version`, never by a change handler** - subscribing would switch change journalling on
  for every model - and held in a weak table, so a closed project takes its index with it.
- **An unset reference slot is not a usage.** A dangling reference is indexed under the id it stores; no live node
  answers that id, so a question about a node never returns it.

`FindUsages` (`Projectional.Editing/Find/`) is the caret half: **a caret on a `ReferenceCell` asks about its
target, anything else about the node whose cell address the caret carries** (an empty child slot therefore asks
about its parent, as the intention list does). `UsageReport.HasTarget` separates "the caret names no node" (an
unset or unresolved reference) from "nothing points here" (a clean node) - a panel must say different things. A
usage's place is computed as `CellAddress.OfSlot(owner, slot)`, so it is right in any document; a hidden reference
is still reported, and a caller tests `EditorTree.Find(place)` before selecting.

The shell's panel is wiring: `UsageList.Of(report)` derives the rows (`Owner label - slot`, prefixed with the
model name when the usage is in another model), `MainWindow` fills a list, keeps `_usageTarget` so an edit
re-asks about the same node rather than the moved caret, closes the panel when the target is gone, and falls
back to the owner node when a row's reference has no cell. A Close button in the header is the deliberate way to
give the page its height back. `Alt+F7` is the window's, tunnelled like F5, because the answer is a panel and not
a list the control paints.

`RenameNodeAction` is the consumer that index was built for. A reference resolves by id, so a rename only has to
refresh the *fallback* text: it sets the name and rewrites `resolveInfo` through `EditContext.SetReference` (still
the only writer) in one transaction, so one undo press takes both back. **It is model-scoped**: a fallback in
another model belongs to that model's history, so it is left alone, and a node in another model is refused rather
than renamed outside the history that would undo it. Renaming to the same name is `Unchanged` and refreshes
nothing. The shell's **Edit ▸ Rename...** (`Shift+F6`) opens a prefilled bar and applies the action; `INT001` stays
the caret jump into the name slot, because an intention cannot ask for a value.

`GoToDefinitionAction` is the other consumer: **Ctrl+B**, **Ctrl+click** and the context menu's `Go to Definition`
move the caret to the declaration a reference names. `GoToDefinition.TargetOf` answers only for a reference cell -
unlike `FindUsages.TargetOf`, which falls back to the node the caret is in - because there is nothing to go to
otherwise. `PlaceOf` requires the target to be **drawn in this tree**: a session projects one root, so a sibling
root, another model, or a concept without notation is a rejection with a reason and a disabled menu entry, never a
caret dropped somewhere unrelated. The name cell is selected through `SelectSlotAction`; navigation creates no undo
step. Ctrl+B is one more Control exception in `EditorKeyMap` (no host can navigate a model it cannot see), and
Ctrl+click is the pointer's spelling of it in the control.

