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
milestones **M0, M1, M2 and M3 are complete** (model, change events, undo/redo, XML persistence, test
harness, CI; projection and editing; declarative languages and the standalone editor; scopes, completion,
triggers, intentions with preview, and the text-or-fragment clipboard). The declarative notation layer
in `Projectional.Language`, the runtime editor tree in `Projectional.Projection/Tree/`, the
measure/arrange layout in `Projectional.Projection/Layout/`, the headless snapshot renderer in
`Projectional.Projection/Snapshot/`, the edit-action layer in `Projectional.Editing/`, the first
two real languages (`Projectional.Expressions`, `Projectional.Samples`), the embeddable
`Projectional.Avalonia` control (which paints real glyphs, hit-tests clicks and handles keys),
syntax highlighting from the meaning on a run to a host-swappable palette, model-defined text
decorations, cell-wise caret motion with a blinking caret, a context menu described by the
editing layer, concept- and value-driven completion (Ctrl+Space, icons as host images, an
enumerated property offering its values on arrival), and text
clipboard editing (Ctrl+A/C/X/V through a host-supplied `IEditorClipboard`, text inside one cell and a
structural fragment with new identities), line numbers, font
size, model-defined markers with hover tooltips, concept descriptions shown on hover and in the
completion list, quick fixes (Alt+Enter/Ctrl+., a painted list, a bulb in the margin), the standard
check catalogue as a package (`Projectional.Checks`), the intention model and its per-caret list, the hover
preview of an offer (its description, then the result of its edit shown in place as a ghost of a discarded
copy), the framework's own intention catalogue (rename, wrap in X, reset to the default, in
`Projectional.Intentions`), the constraint language (a language's own rules written as models, in
`Projectional.ConstraintRules`), logical
scrolling (bars via
`ILogicalScrollable`, wheel, page keys), and text import (the `ITextImporter` contract in
`Projectional.Language/Import/` with `Projectional.Json` as its first reference language) are done. The
standalone shell (tabs, menu bar, status bar, a problems panel) is being delivered.

**The app is `MainWindow`, not a `ShellView`.** `standalone.md` and ADR 0015 describe a shell that is a
library (`ShellView`, `ShellWindow`, `IShellStorage`, `IShellDialogs`, `ProjectDriver`, `CommandRegistry`);
**none of those types exist yet** - ADR 0015 puts that extraction at M6. Read those documents as the target
shape, not as the tree, and add M2 features to `MainWindow` rather than inventing a `ShellView` on the way.

## Status and where the truth lives

- `docs/roadmap.md` - milestones M0-M8 with acceptance gates and effort estimates (M8 is after 1.0). It holds the **scope** of each milestone and what is left, not a status report; a finished milestone is removed from it.
- `docs/implemented.md` - the record of delivery: every finished feature, where it lives and the test that proves it, milestone by milestone.
- `docs/architecture/` - model, language, projection, editing, persistence, semantics, integration, standalone, plus one document per planned feature: `completion.md`, `markers.md`, `node-moves.md`, `scrolling.md`, `import.md`, `csharp.md`, `llm-language-description.md`.
- `docs/adr/` - 22 accepted decisions. **Frozen.** To change one, write a new ADR that supersedes it.
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
   `Toolkit` sits *beside* `Language` (it references `Core` + `Language` + `Projection` + `Persistence`) and is
   what a language package may reference instead; nothing references `Toolkit` from below.
3. **Three assemblies expose public API: `Projectional.Core`, `Projectional.Avalonia` and
   `Projectional.Toolkit`** (the last is what a language package is written against - one reference plus
   `LanguageSnapshot`, [ADR 0019](../../../docs/adr/0019-toolkit-is-the-language-surface.md)). Every other
   assembly is `internal` plus `InternalsVisibleTo`, and `Language`/`Projection`/`Persistence` are public
   only because the toolkit aggregates them. **Never aggregate `Editing` or `Semantics` into the toolkit**:
   keeping them out is what makes "a language is headless" a property of the build instead of a rule.
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
10. **A concept is concrete, abstract or an interface, and only concrete concepts can be nodes.**
    `ConceptDeclaration(..., isAbstract: true)` names a *set* that a slot can be declared over;
    `Model.NewNode` rejects it, so no code path can produce a bare base node. The single consumer
    is `ConceptRegistry.DefaultInstantiableFor(key)`, which returns the concept itself when
    concrete and otherwise the first non-abstract concept below it in **declaration order** -
    that order is the language author's answer to "what does the user most likely want here",
    and `InsertNodeAction.FillSlot` uses it to fill an empty slot (returning `EditResult.Unchanged`
    when no concept can exist). An abstract concept may therefore have no notation; every
    concrete one needs its own or an inherited one.
    An **interface** (`isInterface: true`, which requires `isAbstract`) is the third kind: abstract by
    definition, never instantiated, contributing its properties and references to the concepts that
    `implements` it. Conformance is still `IsKindOf` and storage is still one flat slot map, so no layer
    learned a second relation; `Concept.SuperConcepts` is the ordered union (extensions first, then
    interfaces), and `Concept.Extensions`/`.Interfaces` give a tool the difference back. `ConceptRegistry.Create`
    injects `Framework.Named` into every registry, so a language cannot ship without it and a host can rely on
    that key meaning the same thing in every document (`docs/architecture/interfaces.md`).
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
- **A finished feature moves into `docs/implemented.md` in the same commit that finishes it** - a row
  saying what it is, where it lives and the test that proves it. `docs/roadmap.md` keeps only the scope
  of each milestone and what is left, so it never becomes a second status report.

## Code layout (explicit user requirement)

- **One type per file.** A file is named after the single type it declares; nested or
  helper types get their own file rather than sharing one.
- **Group by concern, not by kind.** Files live in a folder named after the concept they
  belong to (`Identity/`, `Language/`, `Model/`, ...), and the folder is the only grouping
  mechanism - the namespace stays flat, so consumers need no extra `using` directives.
- **One project per language** ([ADR 0021](../../../docs/adr/0021-one-project-per-language.md)): a language's
  concepts, notation, importer, generator, types, constraints, scopes, checks and intentions are one assembly, with
  `Checks/` and `Intentions/` as folders. Language packages live under `src/Languages/`, the sample languages in
  `src/Languages/Samples/`, and `src/` itself keeps only the framework and the host - so the repository's own
  `samples/` (the host application) is the only top-level one. Their namespaces keep the names they had when they
  were separate packages (`Projectional.Json.Checks`), so a consumer's `using` never has to change - do not "tidy" a
  namespace to match a folder here.
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

## How a language is declared as a model

`Projectional.Structures` is slice P1 of M2: the structure language, nine concepts whose nodes *are* a language
definition, plus `StructureLowering` into the `ConceptDeclaration`s every other layer understands.

- **A definition is a model like any other** - same editor, same writer, same undo - because that is what makes
  the two authoring paths of ADR 0014 equal. `StructureLanguage.ConceptDeclarations` is the C# twin of the
  definition language itself.
- **The language says what a concept *is*, never how it looks.** Presentation is the editor language (P2), in a
  separate model, for the same reason MPS splits structure documents from editor documents.
- **The cardinality is data, spelled as a range**: a `cardinality` names one of the four MPS offers in one cell -
  `0..1`, `0..n`, `1`, `1..n` - and `typeName` is a name an author types, so the *lowering* validates them. A model
  that refused them would have to know the concept registry. The range is stored as `ChildSlot.Cardinality` (the
  storage shape) plus `ChildSlot.Minimum` (the lower end), and the two checks read it: `STD010` for a single child
  whose minimum is one, `STD011` for a list whose minimum is at least one. A minimum above one is representable in
  the model but has no spelling yet - do not invent one until a language asks.
- **`StructureLowering` checks what a compiler would have checked** (every property names a known type, an
  enumeration has symbols and a unique key, a slot is one of three kinds) and deliberately **not the graph**:
  unknown super concepts, child slots naming nothing and duplicate keys are `ConceptRegistry.Create`'s answers.
  **Do not add a second graph validator here** - the two would drift, and `CreateRegistry` is the single answer to
  "is this a language".
- **The namespace is `Projectional.Structures`, not `Projectional.Structure`**: CA1716 is an error here and
  `Structure` is a VB keyword. The class names keep `Structure`.
- **`Projectional.Editors` is the other half (slice P2)**: the editor language, nineteen concepts whose nodes are a
  notation, and `EditorLowering` into real `Notation`s. It is a *separate* language from the structure one, because
  a definition that mixes the domain with the presentation cannot be reviewed. The **inline/block split is carried
  by slot declarations** here - `Editor.Line.cells` holds `Editor.InlineCell` - so `ed.Line(ed.Indent(...))`'s
  compile error becomes `ModelInvariantException` at the edit, and the lowering has no such check of its own.
  **The lowering goes through `NotationEditor`, not around it**, so a declared notation *is* a builder call; it
  checks only that a notation names a declared concept, and leaves undeclared slots and duplicate variants to
  `NotationRegistry`. The namespace is `Projectional.Editors` (plural) because `Projectional.Editor` would shadow
  every `Editor*` type of `Projectional.Editing`, and the notation of the notation lives in `…Editors.Rendering`
  because `…Notation` would shadow the `Notation` type.
- **Both definition languages are drawn in MPS' shape** (`StructureNotations`, `Editors.Rendering.EditorNotations`):
  a header, a block of options, then one section per kind of slot, with `[/ ... /]` / `[> ... <]` / `[ ... ]` /
  `[\| ... \|]` for the editor language's cell model and a glyph per cell - `-word-` (keyword), `%slot%` (property),
  `( slot )` (single child), `{ slot }` (list), `< slot >` (reference), `;` (gap). The editor language shows an
  empty list as `<< ... >>`; the structure language names the kind instead (see below). Neither replaces its
  placeholder with prose such as `<no notations>`. A line is `[` beside its cells beside `]`, which is what
  `HorizontalNotationCell` exists for.
- **A structure concept is a header and a brace block** (`StructureNotations`). The header says what the concept
  *is* and what it inherits - `abstract concept Rules.Rule extends Rules.Element, implements Framework.Named {` -
  and a list that is empty is **not drawn at all** (`concept Rules.Rule {`). The body is one block of options plus
  one section per kind of slot, closed by `}`. The comma between the two lists sits inside the `implements` run
  and is conditioned on `extends`, so it never dangles.
- **`WhenFilledNotationCell` (`ed.WhenFilled`) is the cell that hides an empty slot**, and hiding one is a pair:
  the empty slot has no caret stop, so the language owes an intention that creates the first child.
  `Projectional.Structures.Intentions` pays with `Inherit from…`/`Implement interface…`: `InsertChildAction` for
  an empty list, `InsertSiblingAction` for one that already has entries - Enter does not extend an inline list -
  and both land the caret on the new key. Do not add the cell without the entry: an unreachable slot is worse
  than a placeholder. Its declared twin is `Editor.WhileFilled` (ADR 0014: anything C# can say, a model must be
  able to say).
- **A declaration says its own kind and closes where it ends** - `<<property name : string ... >>`,
  `<<child steps : Rules.Step[1..n]>>`, `<<reference condition : Rules.Condition ... >>` - and every collection in
  that notation **stacks**, because a definition is read one declaration per line. Layout draws a collection inline
  unless the notation asks otherwise, so this is `stacked: true` on every collection here, not a renderer default; a
  section with four slots used to be drawn side by side. The marker is what makes a property's three option lines
  readable: without a visible end nobody can tell where one declaration stops.
- **An empty list names its kind in the structure language**: `<<property>>`, `<<child>>`, `<<reference>>`,
  `<<symbol>>`. Three empty sections that shared one glyph could not tell an author which slot Enter would fill -
  do not collapse them back into a single `<< ... >>`. The two header lists are the exception: `extends` and
  `implements` already say which list they are, so an empty one is hidden rather than given a glyph. The editor
  language keeps `<< ... >>` for its empty cell lists.
- **`Structure.Concept` holds `properties`, `children` and `references` as three `Many` slots, not one `slots`
  list.** The kind of a slot is a fact of the model, not something the renderer infers from the child's concept -
  which is why MPS splits the sections too. The consequence is a **fixed rank**: lowering concatenates properties,
  then children, then references, and that rank *is* the concept's declaration order, which decides the default
  fill and the completion ranking. A definition that creates a reference before a property still declares the
  property first; do not sort by creation order or by name.
- **`abstract concept Rule`, not `is abstract: true`** - MPS' spelling, and a `ConditionalNotationCell` is what
  makes it work: the word exists in the notation always and in the drawing only while the flag is true. A concrete
  concept draws *nothing* there, not an empty cell, so the flag has no cell to type into - which is why toggling it
  is an intention (`Projectional.Structures.Intentions`, `Make abstract`/`Make concrete`) and why the action behind
  it is `SetNodePropertyAction` (node + slot) rather than the cell-addressed `SetPropertyAction`.
- **One part of MPS' look is still out of reach and must not be faked**: `instance can be root` and `alias` exist
  in MPS but lower nowhere here, so ADR 0014's "nothing declared-only" keeps them out.

### The parity rule is executed by three tests

`LanguageParityTests` + `ParityHarness` compare `TestLanguage` (C#) with `DeclaredTestLanguage` (the same language
as a model, lowered by the shipping code): **registry dump** (ordered text - order decides default fill and
completion ranking, so never compare sets), **snapshot** (one document drawn through both notation registries) and
**findings** (a `CheckSet` over two documents built the same way). Findings are compared **by place in document
order, never by node id** - two documents built the same way have different identities, and a finding promises
*where*. The harness returns text rather than booleans so a failure reads as a diff.

**A parity gap is a defect, not a test problem.** Writing the twin immediately found two: `collapseSlot` and a
collection's separator were expressible in C# and not as data. Both are data now. When a declared language cannot
express something C# can, fix the language - ADR 0014 rule 2.

### The workbench loads the definition languages

`WorkbenchLanguage` (`Projectional.Workbench/Documents/`) is the app's **one** registry and **one** notation
registry: it composes the sample rules language with `Projectional.Structures` and `Projectional.Editors`, and
`DocumentFile.TryLoad` loads against it. A concept document therefore opens in the app like any other document - a
language definition is a model.

- **Include the transitive closure.** `RulesLanguage.ConceptDeclarations` is that language's *own* concepts; it
  borrows `Expressions.Expression`, so a host composing it must bring `ExpressionsLanguage.ConceptDeclarations`
  too. Missing it is a `ConceptDefinitionException` at startup, naming the concept.
- **`EveryConceptOfAllThreeHasANotation`** walks the composed registry and demands a notation per concrete concept,
  because a registered concept without one renders as `MissingNotationCell` - the one state a user cannot work
  around. It already caught `Editor.Collection` being added without its notation: **add the notation in the same
  commit**.
- The extra `ProjectReference`s are **language packages** (`Structures`, `Editors`), the same category as
  `Samples`, not Core-adjacent internals.

### A language project is a folder

`Projectional.Languages` holds `LanguageProject`: a directory with a `structure` folder (required) and optional
`editor`, `types`, `generator` and `constraints` folders becomes a `ConceptRegistry` + `NotationRegistry` +
`TypeRegistry` + generators + `ConstraintSet`.
**`Open(directory, out error)` answers a usable
language or a message, never an exception** - opening is a user action, and a user action reports. The notation is
lowered *against the concepts the structure documents just declared*, so a notation for an undeclared concept is
caught at the earliest moment both halves exist. **A language without an `editor` folder loads on purpose**: every
node draws as a missing-notation cell, which is the state a language being written is in.

**One document holds one definition**, and the folder is read in ordinal file-name order - that order is the
declaration order, which decides default fill and completion ranking. A structure document's root *is* a
`Structure.Concept` or a `Structure.Enumeration`; an editor document's root is one `Editor.Notation`; a types
document's root is one `TypeRule.Rule` or one `TypeRule.Named`; a generator document's root is one
`TextGen.Generator`; a constraints document's root is one `Constraint.Rule`. **There is no container concept**
(`Structure.Language`, `Editor.Notations`, `TypeRule.Rules`,
`TextGen.Generators` were deleted): a container with one child would be a second place to say which definitions
exist. Do not reintroduce one to "simplify" a fixture - a document with two roots is refused by `LanguageProject.Read`
and by the rules of MPS' file layout.

**`samples/declared-pipeline` is the M2 gate made concrete**: a DSL that exists only as data - a `structure` folder,
an `editor` folder, a `constraints` folder and a document - with no C# anywhere. `DeclaredSampleLanguageTests` copies the folder next to the test
assembly (a `<None Include="..\..\samples\...">` item) and loads exactly the files a user opens, so the sample cannot
drift from what the code accepts without a test failing. It is the place to look when asking "can a language really
be written without C#", and the place to extend when the declared path grows. The definition files there are the
*writer's* output - `EveryDefinitionDocumentSavesBackByteIdentically` re-saves each one and demands the same bytes -
so generate them with `ModelFile.Save` rather than by hand.

**`WorkbenchLanguage` is per shell, not static** - it grows with **File ▸ Open language…**, and `TryAdd` builds both
candidate registries first so a clash changes nothing (a half-added language gives documents a concept registry that
disagrees with the notation registry in force). Growing the set re-declares every concept already in it
(`Redeclare`), because `ConceptRegistry` is immutable; the parity harness's dump comparison covers that path. A
document of an opened language starts at the first **rootable** concept: concrete and `Applicability.IsAnywhere` - a
concept that names places can never be a root.

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
- **`Horizontal` is the one cell where the parent decides a direction.** It takes blocks like a
  sequence and lays them out in one row, so a notation whose cells are each a small diagram can put a
  bracket beside them (`[ cells ]` in the editor language). Everywhere else the orientation is inherited -
  an inline child whose notation root is vertical breaks the line - because the parent cannot see the
  child's notation. It adds no gap of its own; a blank is a `Space` cell the notation asks for.
- **The runtime editor tree is a different vocabulary.** Notation cells are `…NotationCell`
  (`ConstantNotationCell`, `PropertyNotationCell`, `SingleChildNotationCell`,
  `ChildNotationCell`, `CollectionNotationCell`, `ConditionalNotationCell`, `WhenFilledNotationCell`,
  `LineNotationCell`, `SequenceNotationCell`, `IndentNotationCell`, `HorizontalNotationCell`,
  `SpaceNotationCell`) precisely so they cannot be
  confused with the runtime cells `docs/architecture/projection.md` names (`ConstantCell`,
  `PropertyCell`, ...).
- **`ConditionalNotationCell` is the only cell that may draw nothing.** It holds a property name, the
  text that property must equal, and inline cells; when the condition does not hold the builder emits
  *no cells at all* for it. Never "fix" that into an empty or zero-width cell - a zero-width cell is a
  caret stop on an address with no text. Two rules follow, and both bit on the first use:
  **the condition is a property + value, never a delegate** (a delegate cannot be written into a notation
  model, so the declared path would become weaker than C#), and **the gap belongs inside the conditional**
  (`ed.Conditional(..., ed.Keyword("abstract"), ed.Space)`, or every drawing without the word starts with a
  blank). `NotationRegistry` validates the slot is a property and the value is one its type accepts.
  Inline only - a block conditional is the same idea one level up, when something needs it.
- **Every slot-bearing cell has an `EmptyText`** (`<empty>`, `<none>`, and for a property
  whatever the notation wants to say - `<<operator>>`). Empty is never invisible: the caret
  needs something visible to attach to, and an empty list must stay fillable. Do not make it
  empty-string to "tidy up" a rendering. A property's stand-in is display only: the caret
  stands in the empty `Text`, so typing still starts from nothing.
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
- **`Applicability` is the "conditional concept"**: a concept may name the places it may stand in
  (`Applicability.Only(new ConceptPlace(Class, "attributes"), ...)`), because some concepts are not valid
  everywhere their type is accepted - a C# attribute, an XML comment. It is consulted by completion, by
  Enter's default fill and by the typing rules, and **enforced** in `Model.InsertChild`/`InsertChildAt`/
  `InsertRootAt`, which is why a filter bug cannot produce an illegal document. A concept that names places
  can never be a root (no parent, no slot). Do not merge it with `isOfferedInCompletion`: that one silences
  the list only, this one is legality. `ConceptRegistry.Create` validates every place at registration.
- **A trigger is declared on the concept it creates**, in `ConceptDeclaration.triggers`, and says what a typed
  character means at the *edge* of a node that already exists. Two reasons and both still hold: every slot it
  names is a slot of that concept, and a language that borrows another's `ConceptDeclarations` gets the gestures
  with them - there is no registry to forward (there was one, and it was deleted). The concept key is absent
  because the trigger creates its declaring concept, and triggers are **not inherited** (a subtype reusing the
  gesture would create the super concept). `ConceptRegistry.TriggersFor(Concept)` answers in concept declaration
  order, which is the only tie-break; `ConceptRegistry.Create` validates every trigger, so a typo is a language
  error at load. See `docs/architecture/triggers.md`.

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
  becomes a property cell, a single-child column a child cell, a reference column a reference
  cell. That is what lets one table draw rows of different concepts that declare the same slot,
  and it is how a foreign key is drawn.
- **A header is a cell of the table's own notation** and the `TableColumn` constructor refuses
  anything but a constant or a space. A header that named a slot would silently depend on
  whichever row came first.
- **The row concept comes from the rows slot**, so `NotationRegistry.ValidateTable` checks the
  columns against the child concept of the `Slot` - not against the concept owning the table.
  A column naming no slot, a name that is no property, child or reference, and a many-valued
  child are all rejected at registration.
- **A reference column is projected by the same builder call a notation's reference cell is**
  (`EditorTreeBuilder.BuildReferenceCell`), with the column's `EmptyText` standing in for the
  notation's `UnsetText` - so unset, unresolved and resolved mean one thing wherever they are
  drawn. Do not give a table its own reference state machine.
- **An empty table draws its header and one `AddRowCell` row.** The header is what says what the
  columns are, and the affordance row is the only place the caret can stand to create the first
  row: `CaretPlace.IsEmptySlot` counts it and `InsertNodeAction` fills the slot through the same
  `InsertChildAction` an empty child slot uses. `TableCell.Flatten` refuses a table with **no
  columns** instead. The affordance's text is the notation's `EmptyText`, so
  `ed.Table("rules", "+ rule", ...)` is how a language says what an empty table looks like.
- **The affordance carries the slot's address, not the table's.** A table that is the root of its
  node's notation carries the node's address, which its headers carry too, so an affordance
  addressed that way is indistinguishable from the first header and the caret lands on "rule".
  `TableCell` therefore takes the slot address separately and the builder always passes it.
- **Layout reads the drawn grid from `Content`, not from `Rows`**, because an empty table draws a
  row the model does not have. `Rows` stays the model's rows; `IsEmpty` is `Rows.Count == 0`.
- **Headers are projected from the node that owns the table, not from a row**, so a table with
  zero rows still knows its shape and a header can never be answered by a row.
- **A column width is view state and a floor.** `IColumnWidths` (in `Projection/Layout`) is the
  question layout asks, `ColumnWidths` (in `Editing/Tables`) the answer; `EditorSession.SetColumnWidth`
  re-lays out without a transaction, so it is never saved and never an undo step. A width narrower
  than the content is ignored - clipping a value would hide what the model says.
  `ColumnResize.HandleAt` finds the boundary (the right edge of a column, **inside the header row
  only** - the body is where the reader selects and types), and the control only turns a drag into
  `SetColumnWidth`. The tolerance is horizontal only: a vertical one is a whole line in a monospaced
  measure and would make the first body row a handle.
- **`Editor.Table`/`Editor.Column` make a table declarable**, so a language defined in project files
  can draw one. The lowering goes through the same `TableNotationCell`/`TableColumn` the C# path
  uses; a column's `header` is lowered as an inline cell, so a header naming a slot is refused by
  `TableColumn` rather than answered by whichever row came first.

## How scopes work

`Projectional.Semantics/Scopes/` answers "what may this reference point at", and the design is
`docs/architecture/language.md` §Scope model.

- **`IScope` is asked two questions and they are the same question**: `Entries` is "what may I
  reference here" (the completion list) and `Resolve(name)` is "what does this name mean" (a typed
  name). One implementation, two consumers - a second lookup that answered differently would let the
  list offer something the name cannot mean.
- **A level does not deduplicate.** Two entries with one name inside a level are exactly what makes a
  name ambiguous; collapsing them would turn a decision the user has to make into a silent pick.
- **`ScopeChain` does not fall through an ambiguity.** A nearer level that knows the name twice makes
  the name ambiguous; looking further for a unique answer would make a nearer declaration lose to a
  farther one, which is the opposite of what a chain is for.
- **`DefaultScopeProvider` is local, ancestors, model, project** - nearest first. Only named nodes
  contribute (no name slot or an empty name is not something a name could mean), and **the node being
  resolved is excluded everywhere**: a reference that could point at the thing holding it is a cycle
  the model already refuses. The "imported models" step of the design is not implemented - a project
  has no import list yet, so the project level stands in for it.
- **`EditorSession.Scopes` is the aspect**, defaulting to `DefaultScopeProvider.Instance`. It is a
  property rather than a constructor argument because a language package cannot reference the editing
  layer, so the host installs a replacement.
- **A language replaces a level with `ScopeProviderRegistry`**, which is itself an `IScopeProvider`: it
  maps a concept key to a provider, walks the super concepts in declaration order (the same order
  notation and triggers resolve in) and falls through to the default. A host installs one by assigning it
  to `EditorSession.Scopes` - one place decides which provider wins, not a second kind of provider.
- **A reference completion carries a *name*, not an id.** `AcceptCompletionAction` re-resolves it
  through the scope at the moment the key is pressed, so a target renamed or removed after the list
  opened is refused instead of linked, and a name that has become ambiguous is refused rather than
  picked. That is the same "re-resolve at apply time" rule a `DiagnosticFix` follows.
- **`EditContext.NodeOf` searches the document, not the model.** A reference target is often a
  *sibling root*, so resolving one through `NodeOf` returns null and the edit silently does nothing -
  use `node.Model.Project.FindNode(id)`. This cost a debugging round; do not "simplify" it back.
- **`SelectCompletionAction` takes a delta, not an index**, and `CompletionList.Move(0)` throws. A
  freshly opened list already selects index 0, so a test that wants the first entry accepts directly.
- **Typing into a reference cell resolves a name, it does not store text.** `TypeTextAction` asks the
  scope for the pending run plus this character: `Found` links the slot, `Missing` keeps the run (a name
  is spelt over several keystrokes), `Ambiguous` is `Rejected` with a reason. Nothing is written while
  the name is incomplete, so a half-typed name costs no undo step. **`Select` clears `PendingText`**, so
  a test that spells a name over several keystrokes must select once and then apply the keystrokes -
  selecting before each one wipes the run and the name never completes.

## How constraints work

`Projectional.Semantics/Constraints/` is the language's own validation, and the design is
`docs/architecture/semantics.md` §Constraints.

- **A constraint reports; it never repairs and never mutates.** The repair is a `DiagnosticFix` built by
  whoever knows how to fix the finding, which is why a language author can write a rule without knowing
  what the editor can do about it. A finding with no repair reaches the squiggles and the problems panel
  and not the quick-fix list - the right answer for "a rule needs a condition", where inventing a
  condition would be worse than saying one is missing.
- **`ConstraintContext` is deliberately small**: the node, `ScopeOf(slot)` and the collector. It cannot
  mutate, cannot reach the editor and cannot see a cell, so a rule that runs on a model nobody is drawing
  (a load, a batch check) works the same as one on the open document.
- **A concept inherits its super concepts' constraints**, in declaration order - the same rule notation,
  triggers and scope providers follow.
- **A rule about *any* node is `ConstraintSet.Everywhere`, not a concept key**, and runs before the
  concept's own. A language's concepts need not share a root, so a rule declared over one root silently
  skips every concept that does not inherit from it - that is the mistake this seam exists to prevent.
  `With(set)` composes two sets in a predictable order (a host adds `ScopeConstraints.All` to its own).
- **A constraint is not a check.** `ICheck.Find` returns `DiagnosticFix` and its contract says every
  finding carries the repair that removes it, so a rule with no repair *cannot* be a check. If it only
  reports, it is a constraint - which is why ambiguity diagnostics are one.
- **`Check(Node)` is the incremental seam**: it answers that subtree's findings alone, so a caller that
  keeps the rest merges them itself. The dependency-set machinery of the design is **not** implemented -
  a caller rechecks the subtree that changed, which is correct and cheaper than the document, but it does
  not yet know that a *referencing* node's rule may have changed too. Do not claim incrementality beyond
  that.
- **The app merges constraint findings with the check catalogue's in one list** (`PublishFindings`),
  because the panel and the markers must not be two answers to "what is wrong". A constraint finding has
  no fix, so it is a marker and a panel row only. Which rules it runs is the document's: `DocumentTab.Constraints`
  carries the language's own set, and `SampleConstraints.All` stands in where a caller passed none.
- **A rule can be written down.** `Projectional.ConstraintRules` is the constraint language: a `constraints` folder
  holds one document per rule, `ConstraintLowering` turns each into the same `IConstraint` a C# language declares,
  and `LanguageProject.Constraints` is what a host runs. The body vocabulary is closed on purpose (`EmptySlot`,
  `DefaultValue`, `SurroundingWhitespace`, `EmptyReference`) - a general expression body would be an interpreter for a
  language whose only job is to say what is wrong, the same line the type rules draw with three bodies. An empty
  concept key means "every node", drawn as the property cell's `every node` placeholder; a rule naming a concept or
  slot the language does not have (or declares as another kind), or without a message or a body, is a load error.
  Parity with the catalogue is a test rather than a claim: `ConstraintLoweringTests` declares the catalogue's rules as
  models and demands the same findings, at the same places, with the same severities.

## How types work

`Projectional.Language/Types/` computes what a node evaluates to; the design is
`docs/architecture/semantics.md` §Types.

- **`expected.Accepts(actual)` is the only question**, and the direction matters: a caller that got it
  wrong would be comparing two types for equality, which is a different and much weaker question.
- **`UnknownType` accepts and is accepted**, which is what suppresses follow-on errors - one mistake in
  a condition must not produce a finding on every expression that reads it. It is **not** a wildcard to
  rely on: a caller that must not proceed on a guess (a generator) asks `type is UnknownType` explicitly.
  That is the "cannot leak into generation unnoticed" rule - the type does not stop the caller, it makes
  the caller say what it does about not knowing.
- **A concept with no rule is `Unknown`**, not an error. Nobody said, so nothing is known.
- **A record is compared by name, not by order**; the order is kept for display only. **An optional
  accepts its inner type, and a plain type does not accept an optional** - "may be absent" is a promise
  about what a reader has to handle, so a slot that promised a value must not be handed one that might
  not be there. Nesting is not collapsed: `int??` says the absence may itself be absent. **Every
  structural type accepts `Unknown`**, for the same reason `PrimitiveType` does.
- **`ITypeRule.TypeFor`, not `TypeOf`**: CA1716 forbids a reserved word as an interface member, the same
  reason `ICheck.Find` is not called `Of`. The class member `TypeContext.TypeOf(Node)` keeps the natural
  name because the rule does not apply to it.
- **`TypeContext.TypeOfChild(slot)` answers `Unknown` for an empty slot**, not an error: a half-written
  expression is the ordinary state of a document being edited.
- **A rule is inherited by the concepts below it**, the same lookup notation, triggers, scope providers
  and constraints use.
- **`ConstraintContext` carries the `TypeRegistry` beside the `IScopeProvider`**, and `ConstraintEngine`
  takes both - a rule that needs a type and a rule that needs a scope are the same kind of rule.
- **Type rules live *in* the language package, beside the notation and the typing rules** -
  `ExpressionsLanguage.Types` is part of the language. That is why the type model sits in
  `Projectional.Language` and not in `Projectional.Semantics`: the layer diagram puts `Semantics`
  above `Language`, so a language package could not declare a rule from there. The line is what the
  aspect *does*: a type rule only answers a question, so it belongs to the language like notation
  does; intentions and checks produce edits, so they must live beside it (their packages reference
  `Editing`). Do not move the types back into `Semantics` - it looked tidier and it is wrong.
- **`Projectional.TypeRules` is the declared form** - the first declared aspect: a rule is a model
  (`TypeRule.Rule` with a `Fixed`/`Child`/`Switch` body) and `TypeRuleLowering` turns it into the
  same `ITypeRule` a C# author writes. **Three bodies, not a statement list**: MPS' typesystem is
  imperative, and copying that would mean shipping an interpreter for a language whose only job is to
  answer one question per node. A body that cannot be lowered (no body, an unknown type name, a value
  declared twice, a switch with no `otherwise`) is a `ConceptDefinitionException` at load, not a rule
  that silently answers `Unknown`. `TypeRule.Primitive` names only the four framework primitives - an
  enumeration would have to be resolved against the language's own registry, which is the declared
  stage's job; a structural type is written with `Record`/`List`/`Optional` instead of being named. The
  parity rule applies: `TypeRuleLoweringTests` asserts the declared registry and
  `ExpressionsLanguage.Types` answer the same type for every node of the sample document.
- **A type expression is a family of its own, not a body.** `TypeExpr` (abstract) + `Primitive` /
  `Record` / `Field` / `List` / `Optional`, and `Fixed` holds one instead of a type name. The split is
  load-bearing: a body may read the node (`type of left`), a type may not - a record field's type cannot
  depend on the node that holds the rule, because a field is part of a *type* and there is no node there
  to ask. Nested arbitrarily; **the drawing of a complete type expression is exactly `IType.Name`**, so
  the rule an author reads and the sentence a finding quotes are the same text
  (`ATypeExpressionDrawsExactlyWhatTheTypeIsCalled`). The one exception is an empty record, which draws
  the shared `<< ... >>` placeholder because an empty slot must stay fillable.
- **A record needs an inline list, which is why `InlineCollectionNotationCell` exists.** `Collection`
  and `Children` are *block* cells: they start their own line, so `(amount: int, currency: string)`
  written inside a `Line` used to be inexpressible - the declared language was weaker than C# for a
  purely notational reason. Both list cells now build the same runtime `CollectionCell` (which arranges
  inline either way); the difference is only whether the list may stand inside a line.
- **A declared name is transparent, and the *accepter* unwraps it.** `NamedType(name, expansion)` keeps the
  name for the message and the expansion for the meaning; every `IType.Accepts` starts with
  `NamedType.Unwrap(actual)`, because a primitive that did not look through would refuse an `Amount` that
  *is* an `int`. `NamedType.Unwrap` is also what a generator calls before deciding what kind of type it has
  (the same rule as `type is UnknownType`). A named type is a **root of the types model** (not a second
  list inside the rules document), `TypeRule.Ref` names it by a string the lowering resolves like
  `conceptKey`, and undeclared/duplicate/self-referential names are load-time errors - the last one needs
  the chain of names being expanded threaded through `LowerType`, which is exactly the bug a fresh set per
  call would reintroduce (infinite recursion, a StackOverflow crash rather than a failing test).

### The framework ships one: ambiguity

`ScopeConstraints.All` is `AmbiguousReferenceConstraint` declared for every node. It closes the scope
model: a typed name and a completion pick both refuse an ambiguous name, so a document cannot be *made*
ambiguous by typing - but renaming one element can make an existing reference share its name with
another, and that reference keeps drawing as resolved because it points at a node id.

Two decisions inside it, both load-bearing:

- **It resolves the target's *current* name, not the stored `resolveInfo`.** A rename does not rewrite
  the stored text (`SetReference` is its only writer), so resolving `resolveInfo` reports names no user
  ever sees - a false positive on every renamed target. `resolveInfo` is the fallback for *re-linking*,
  not a description of now.
- **It stays quiet on an unresolved reference.** That one is already visibly wrong (`<unresolved>`) and
  the text it fell back to may be stale, so reporting ambiguity there would blame the wrong problem.

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
- **Enter inserts after the node the caret is in.** `InsertNodeAction.AddSibling` asks for
  `node.IndexInParent + 1` through `EditContext.AddChild(parent, slot, index, conceptKey)`, which is
  `Model.NewChildAt` - one `NodeAdded`, so undo takes the node out of the place it went into. Appending
  (`Model.NewChild`) is what the *slot-filling* half uses, where there is nothing to be after. It used to
  append everywhere, which made Enter in the middle of a body put the new step at the far end and drag the
  caret there; `EnterInTheMiddleOfAListDoesNotJumpToTheEnd` pins the fix.
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
  caret in the same place. `DeleteNodeAction(NodeId)` is the same removal addressed by node id, which is
  what a repair needs - the caret may be anywhere when a fix runs - and it refuses the document root with
  a reason where the gesture is simply `Unchanged`. Cut and Copy do act on a cross-cell selection: they
  copy the covered nodes as a fragment (`CopySelectionAction`/`CutSelectionAction`) instead of text, which
  is what the structural clipboard is for.
- **`Model.DeleteSubtree` unlinks the top node only.** Descendants stay attached to each other
  and are merely unregistered from the project index, which is exactly what lets one
  `SubtreeSnapshot` undo restore the whole thing. So `body.GetChildren("steps")` still answers
  after `body` was deleted - a test asserting a deleted parent's children are gone is wrong.
- A cross-cell selection reports `IsSingleCell == false` and `HasText == false`, so no text
  action mistakes it for one. `SelectionRects.Of` already draws it correctly (both ends
  trimmed, intermediate cells whole, one rect per line) - the rendering needed no change.

## How text generation works

A language turns a model into text through a template per concept. The template is **data** in
`Projectional.Language/Generation/` (a language package declares one without referencing the layer that walks it);
the walk is `Generator` in `Projectional.Semantics/Generation/`. `docs/architecture/semantics.md` §Generation is the
design of record.

- **Eleven parts, and the set is closed.** `TextPart`, `PropertyPart`, `ReferencePart`, `ChildPart`, `ChildrenPart`,
  `LoopPart`, `IndentPart`, `LineBreakPart`, `ConditionalPart`, `WhenFilledPart`, `SelfPart`. There is deliberately
  no "compute", no
  variable and no counter: a part emits, reads a slot, expands a child or chooses, and anything else is a generator
  this framework does not have. `SelfPart` exists for one reason - inside a `LoopPart` body "this node" is the child
  being visited.
- **`WhenFilledPart` is the part that asks a slot, not a value.** `ConditionalPart` compares a property with a text;
  this one asks whether the slot holds anything, and it accepts all four kinds - so brackets can close around a list
  only when it has an entry, a missing value can become a stand-in, and a tree can expand a node's children only once
  they exist. It is a separate part because the two are validated differently (a property, versus a slot of any kind),
  and a defaulted property counts as filled, exactly as it draws and as `PropertyPart` emits it. The declared twin is
  `TextGen.WhenFilled` (`conceptKey` = the slot, `body`, `otherwise`).
- **`ReferencePart` reads the model's three reference states, not a cell's.** A target's name, the `unsetText` the
  language names, the `unresolvedText` for a dangling target - so a generated file and the editor cannot disagree
  about what a reference says. It arrived with the SQL schema example, whose DDL is unreadable without the name of the
  column a key points at; before it, no language could generate a reference at all. A target that declares no name is
  emitted as `<Concept.Key>`, exactly as a reference cell draws it.
- **`TextTemplate.Of(...)` takes `params ReadOnlySpan<TemplatePart>`**, so a C# generator passes its parts without
  allocating an array.
- **A concept with no template is a failure; an empty template means "emits nothing".** Never collapse the two: the
  first is "nobody said", the second is a decision, and a silent hole in the output is the worst of both.
- **An entry that emits nothing takes no separator with it.** `ChildrenPart` and `LoopPart` write each entry into its
  own buffer (`Generator.EmitEntries`) and skip it when the buffer stays empty, because a separator already written
  cannot be taken back. Without this, "ignored" would mean "nothing" at a single-child slot and "a stray comma" in a
  list - a syntax error dressed up as a feature. Regions written into a buffer are shifted when the buffer is appended,
  so "go to source" still lands on the right character.
- **`TextGen.Ignore` is the declared way to say it** (a mapping body beside `TextGen.Template`, both conforming to the
  abstract `TextGen.Body` the mapping's `template` slot holds - the slot name is persisted, so it keeps its name). A
  concept rather than an empty template node because an empty box reads as unfinished work.
- **`GenerationCoverage.Of(model, generator)` is the editor's hint**: one diagnostic per concept the chosen generator
  cannot print, at its first node in document order, carrying no repair (writing a template is the author's decision).
  The run still stops with a `GenerationFailure` - a report is what lets a user find out before pressing Generate.
  **The shell shows it**: the `Of(model, IEnumerable<TextGenerator>)` overload is asked once per generator the document
  offers, and the findings go into `DocumentFindings.Of(..., reports:)` - so coverage reaches the squiggles and the
  problems panel like any other finding, as a marker and a row and never as a repair.
- **Indentation is the template's, never counted from the text.** `LineBreakPart` writes the newline *and* the current
  indentation; `IndentPart` raises the level for the lines inside it. A separator is a `TextTemplate` too, which is why
  a list on separate lines is `ChildrenPart(slot, Template(LineBreakPart))` inside an `IndentPart` - an "indent" flag
  on the list would have made the drawing depend on what the child happened to emit.
- **Every emitted stretch is a `GeneratedRegion(Node, Slot, Start, Length)`.** The map is the half that is impossible
  to retrofit, and it costs one list while the text is built. Edits get no region (they have no node); a literal gets
  one with `Slot` null, because "go to source" on a keyword must find the node that wrote it. `GeneratedText.RegionAt`
  is a first-match scan - do not build a per-character index for a lookup nobody asked for.
- **A template that expands itself is stopped by `MaxDepth`, not by the stack.** A `SelfPart` outside a loop body
  recurses for ever; a stack overflow cannot be reported to a user at all.
- **The engine recognises only the parts it knows and fails on any other**, exactly like `CellLayouter` with cells: the
  set is closed, so a new part is added on purpose rather than silently ignored.
- **Generation is pure.** No file system, no clock, no culture, no model place - two runs produce the same bytes, which
  is what a host's build and the golden tests both stand on. **The framework does not write files**: it returns text and
  the host decides where it goes (open question 12), so writing is the shell's job and a language component can never
  guess a path or an encoding.

**A language has as many generators as it declares.** `TextGenerator` is a *name* plus a `TemplateRegistry` - not an
aspect on the concept - so the host picks one, the way it picks a notation variant, and nothing about the model
changes. `RulesTextGenerators` in `Projectional.Samples` ships two (JSON and a C# fragment) precisely so that is the
proven case rather than a claim in a comment. The shell builds its Generate submenu from the active document's
generators; there is no fixed "the generator" command, and a document whose language declares none simply has no
entries.

- **The declared form is `Projectional.TextGen`**, and it is a `generator` folder in a language project beside
  the `structure`, `editor` and `types` folders, one generator per document. A generator holds one mapping
  per concept; a mapping holds a template; a template is a list of parts.
- **`TextGenLowering` is the only lowering, and it validates at load**: unique generator names, a concept the language
  actually has, and every part's slot existing on the mapped concept *as the kind it is* (a `Child` part on a list slot
  is a load error). That is the notation registry's rule applied to templates - the earliest moment the author is
  looking at the file.
- **ADR 0014's parity rule is executed by `TextGenLoweringTests`**: a generator written as a model must produce exactly
  the text of its hand-written twin, compared as text because that is what a consumer sees. A declared construct that
  could not be lowered would be a defect in the language, not in the test.
- **A binary expression is always parenthesised by the pilot's generator.** A template cannot compute precedence - no
  part asks how an operand binds - so omitting the parentheses would be right only for the tree it saw. The pretty
  version belongs to the C# base language's generator (`docs/architecture/csharp.md`).
- **Strings are emitted verbatim, and there is deliberately no "as JSON" part.** A template that put a value inside
  quotes does not escape it. Spelling a format belongs to the language that owns the format, in its own generator, so
  converting a model into JSON, XML, TOML or HOCON means producing a model of *that* language and letting its generator
  print it - never a per-value escaping part, which would be a second answer to "how is this value written" in every
  language that prints, and would escape before the target is even chosen. Design:
  `docs/architecture/import.md` §Converting a model into one of these languages.

**The output panel is shared with the interpreter**, and that is deliberate: a run report and a generated file are both
"what this document produced", so one panel and one row type serve both. A generation shows one row per line and each
row points at the node that wrote the beginning of that line, which turns the traceability map into a gesture - clicking
a line of the generated file moves the caret to what produced it. A run's rows are its trace. An edit drops both, because
neither can be derived again from the new model.

## How text import works

Import is the only place a parser exists, and it runs once: the host passes a string, the importer fills a model the
host created, and the text is thrown away - so ADR 0002 is intact. `docs/architecture/import.md` is the design of
record, and `Projectional.Json` is the first reference language behind the contract.

- **The contract is in `Projectional.Language/Import/`** (`ITextImporter`, `ImportResult`, `ImportDiagnostic`,
  `ImportSeverity`), beside `ICellEmphasis` and for the same reason: a language package must be able to declare an
  importer without referencing `Editing`. An `ImportResult` is either refused (no roots, one error) or imported (roots
  plus warnings), so a host writes one branch.
- **One transaction is the whole failure path.** `using var transaction = model.BeginTransaction();` ... `Complete()`
  and nothing else: a refusal or a throw disposes it, the model is byte-identical again, and a success is one undo step
  and one change event. Import needs no new Core seam - `NewNode`/`NewChild`/`AddRoot` are public and journal
  themselves, which is exactly what makes that true.
- **An import diagnostic is not a `Diagnostic`.** That one names a `NodeId`, and a syntax error has no node - that is
  the whole problem. The place is a 1-based line and column counted in *characters* of the text the host passed, plus a
  code that is never localized (`json.unexpected-token`, `json.duplicate-name`).
- **A reference language holds the file's spelling, not the decoded value.** `Json.String.text` is what sat between the
  quotes, escapes included; a number keeps its literal (`1.0`, `1e3` and `1` are three files); a duplicate member is
  kept and reported. That is what makes `Import` -> `Generate` byte-exact **without** an escaping cell in a notation or
  an escaping part in generation.
- **Whitespace is not a value**, so the round-trip corpus is the generator's own output. Never rewrite a corpus file to
  make a test pass; a changed byte is a behaviour change.
- **The tokenizer is the platform's, the mapping is ours.** `Utf8JsonReader` does the lexing, so escapes, number
  grammar and UTF-8 are not our problem. The tokenizer reports byte offsets (`TokenStartIndex`, a `JsonException`'s
  `LineNumber`/`BytePositionInLine`); `SourcePositions` translates them into the line and character column an editor
  shows, and the exception's message has its appended position cut off because the diagnostic carries the place as a
  field.
- **A bracket's inner space is a `WhenFilled` cell in the notation and a `WhenFilledPart` in the generator; the brackets
  themselves are unconditional.** So an empty object prints `{}` while it draws `{ }` - an empty slot must stay visible
  to the caret, and a drawing is not the file.
- **Each reference language ships its own intentions and checks** (explicit user requirement, not a nicety), and since
  [ADR 0021](../../../docs/adr/0021-one-project-per-language.md) they are **folders inside the language project**:
  `Projectional.Json/Checks/` (namespace `Projectional.Json.Checks`), `Projectional.Json/Intentions/`,
  `Projectional.Xml/Checks/`, `Projectional.Xml/Intentions/`. TOML and HOCON get the same two folders when they land -
  **never one shared "syntax languages" package**, which would have to ask which language it is looking at.
- **XML's two intentions are mandatory, not a convenience.** Its notation hides both collections while they are empty -
  an empty XML slot cannot be drawn as a blank, because whitespace inside an element *is* content and a placeholder would
  make `<a></a>` and `<a> </a>` indistinguishable - so "add attribute" and "add child element" are the only way into
  them. Hiding a slot is a pair; this is the other half, and a reference language would otherwise be uneditable.
- **The checks are the format's own rules, each with its repair**: `JSON001` a name or string that is no longer legal
  JSON (escape it), `JSON002` a number cell whose text stopped being a number (restore the declared default), `JSON003`
  one member name used twice (rename to the first free suffix), `XML001` text or an attribute value that is not the
  canonical spelling (escape it), `XML002` a comment containing `--` (separate the dashes). Both answer a `CheckSet`, so
  a host composes them with `StandardChecks.All`.
- **`XmlEscaping` is public on purpose**: the checks package must spell a repaired value exactly as the importer did, and
  a second escaper in a behaviour package would be a second answer to "how is this written".
- **Planned, not built: a declared importer.** Today `ITextImporter` is C#-only, so a language written as a model can
  print its files and not read them. The plan (`docs/architecture/import.md` section *Import as a declaration*) is
  `Projectional.ImportRules` - an `import` folder beside `generator`/`constraints`/..., two layers (tokens, then rules),
  a closed vocabulary that mirrors the template's verbs, `Import.Raw` for tier 2, and a load-time `ImportLowering` into
  the same `ITextImporter`. The recommended default is the *inverse of the generator's templates* (literal -> anchor,
  property -> capture, child -> nest, children -> repeat, when-filled -> optional, conditional -> alternative), with a
  declared rule only where the inverse does not exist - resolved **per slot** from each candidate's first anchor, so the
  parser has no state but the model. Six token concepts (`Literal`, `Word`, `Number`, `Quoted`, `Fixed`, `Skipped`),
  ten rule words including `Bind`/`Matches` (XML's closing tag) and `Fail` (a rule's own message), and
  `Import.Importer.target`: **an importer inverts one named generator**, because a language may print two formats and
  their inverses are different readings - so a faithful language's importer is `default inverse` plus its exceptions.
  Open questions 100-108 hold the decisions.
- **A reference language may hold a note the format cannot express.** `Json.Comment` is a `Json.Member` beside
  `Json.Property`, drawn as `// why` and mapped to `TextTemplate.Empty`, so generation omits it - and the "an entry that
  emits nothing takes no separator" rule is what keeps a note between two members from producing a stray comma. It is
  never imported (a JSONC file stays refused, because the round trip would drop the notes), `JsonDuplicateMemberCheck`
  skips it (a note has no name to collide with), and `GenerationCoverage` counts it as covered - a decided silence is
  not a missing template. A note-only object prints `{  }`: the inner spaces are chosen by whether the slot holds a
  child, which is the price of not having a second answer to "is this object empty".

## How interpretation works

A language says what its nodes *mean* through one function per concept, and the framework runs it. The design is
`docs/architecture/semantics.md` §Interpretation; the invariants that are expensive to retrofit are these.

- **The aspect is in `Projectional.Language/Evaluation/`, the runtime in `Projectional.Semantics/Interpretation/`.**
  Same split as the type rules: a language package must declare meanings without referencing the layer that
  consumes them, so `IEvaluationRule`, `EvaluationRegistry`, `EvaluationContext`, `Value` and its subclasses live
  beside the notation; `Interpreter`, `InterpretationResult`, `TraceEntry` and `InterpretationFailure` live above
  it. `DelegateEvaluationRule` exists because most meanings are one expression long and C# has no lambda-to-
  interface conversion - a language writes `.Add(Key, new DelegateEvaluationRule(ctx => ...))`.
- **A concept with no rule has no value.** Unlike `IType`, there is no `UnknownValue`: `Interpreter` fails with
  "declares no evaluation rule". Do not invent a fallback value - a half-written document must stop the run
  where it stopped being meaningful, not hand out a guess that a generator would then trust.
- **`EvaluationContext` is read-only and fails through `Error(message)`.** It offers `Eval(node)`,
  `EvalChild(slot)` (exactly one child, so an empty slot is a failure naming the slot), `Variable(name)`,
  `PropertyValue(slot)` and `Error(message)`. `PropertyValue` maps through the property's declared
  `PropertyType.TryParse` - the same authority the model validates writes with - so a literal cannot mean one
  thing in the interpreter and another in the document. Never add a mutation method here.
- **The interpreter owns the budget, the stack and the trace; a rule owns only its node.** Every `Eval` is one
  step, checked against `DefaultBudget` (10 000) and the caller's `CancellationToken` *between* nodes; the trace
  is post-order (a child's entry precedes the line of the node that asked for it), which is the order a reader
  follows the answer back. **Nothing is cached**: asking for a child twice costs two steps and appears twice.
  A failure records the *innermost* node once - the first `catch` wins, and ancestors add no line of their own.
- **A failure is a result, not an exception escaping the host.** `EvaluationException` is what a rule throws;
  `Run` catches it and answers `InterpretationResult` with an `InterpretationFailure` that carries the `NodeId`
  (not the node: the result outlives the call). Budget and cancellation are failures with their own `Kind` and
  are raised through the internal `EvaluationStoppedException` sentinel, so a rule's own `catch` cannot mistake
  them for model problems.
- **A run resets the previous one.** `Run` clears trace, steps and variables, so one `Interpreter` serves a
  session that runs the same table against different inputs. Creating a new interpreter per run is also fine.
- **A ruleset is a decision list.** `RulesEvaluation` makes a rule the truth of its condition and a ruleset the
  outcome of the **first** rule that applies; no match is a failure, because inventing `Review` would hide a
  table whose conditions leave a gap. Its `Registry` composes `ExpressionsEvaluation.Registry`, so a condition
  runs without a host bolting two registries together.
- **Values are content-compared records** (`IntValue`, `BoolValue`, `StringValue`, `DateValue`), with `Display`
  for the trace. A trace is read by a user, so `Display` is a rendering and never an identity. A record or list
  value arrives with the first language that evaluates one - do not add kinds speculatively.
- **Tests pass the test token.** Any call to `Interpreter.Run` in a test passes
  `cancellationToken: TestContext.Current.CancellationToken`, or xUnit1051 is an error.

**Running a document is a language capability, not a command.** A registry *is* the interpreter, so "can this
document be run" is answered by the language it is written in - never by which menu opened it, and never by
falling back to another language's rules.

- **`WorkbenchLanguage.InterpreterFor(model)` is the one answer**, derived from the document's root concept: the
  shell ships an interpreter for the sample rules language and for nothing else, so a structure definition, a
  notation, a type ruleset and a document of a language opened from a folder all answer `null`. Running a
  structure model through the expression rules would report a missing rule for every concept, which is a wrong
  answer to a question nobody should have been able to ask.
- **`DocumentTab.Evaluation` carries it and defaults to `null`** - a caller that forgets the aspect gets a
  document that cannot be run, never one that silently runs with the sample's semantics.
- **The shell refuses rather than runs**: the Run entry is disabled, the panel's tooltip and the status bar say
  why, and `RunActiveDocument` returns before asking the interpreter.
- **The app's part is small on purpose**: `DocumentRun.Of` (document + registry + the typed variables) answers a
  `RunReport`; `RunInputs` reads the variables from one line of `name=value` pairs (number, then flag, then
  `yyyy-MM-dd`, else text; a malformed or repeated name refuses the whole line instead of half-applying it); a row
  is `RunRow(Text, Place)`, so clicking a trace line moves the caret to the node that produced it.
- **A run is a snapshot**: an edit drops the report, exactly as an edit replaces the findings, because a headline
  about a document that no longer exists is worse than none - and a run cannot be repeated on the user's behalf,
  since the variables came from them.
- **Trace rows are one line and must be styled that way.** Fluent's `ListBoxItem` is 32 px tall; five steps then
  overflow the panel and the answer's own last line is cut in half. The output list sets `MinHeight` 18 and
  `Padding` 4,1 for that reason - a picture of the panel, not a passing test, is what showed it.
- **F5 is the shell's, through the window's tunnelling handler**, and the gesture is written into the menu header
  rather than declared as a `HotKey`: one mechanism, not two that could both fire.

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
- **`ApplyOffers()` never applies anything. It always opens the list** - one offer or ten. Do not reintroduce a
  "lone plain offer runs immediately" shortcut in any form, and do not gate it on `EditSafety` or on
  `Intention.Fix` either: one keystroke that sometimes acts and sometimes asks is one the user has to guess
  about, and its meaning would change the moment a language declared a second offer. `EditSafety` and
  `Intention.Fix` are still on the offer as *information* (a surface may mark a destructive row; `Fix` is what
  routes a repair through its own apply path) - they are not permissions. A one-entry `OfferList` is the
  ordinary case, not an exception.
- **A fix's label names the result** ("Rename to \"Check2\"") and `Description` is part of the contract; a
  menu falls back to the finding's message. The id is stable and never translated.
- **Safety is on the offer, not on the fix type**, because the intention catalogue carries the same enum.

`Projectional.Checks` is the standard catalogue a language includes by naming it: `ICheck.Find(Model)`
(the member is `Find` because CA1716 forbids `Of` on an interface member), `CheckSet` (ordered, immutable,
`With` to extend), and `StandardChecks` (`Names`, `Structure`, `Expressions`, `All`). A language publishes
one `CheckSet`; the host runs it and derives `MarkerSet`/`FixSet` from the fixes. A check produces
diagnostics *and* edits, so it names `Editing`, `Semantics` and (for the expression group) `Expressions` -
which is a reference a language package now makes too: since
[ADR 0021](../../../docs/adr/0021-one-project-per-language.md) a language's checks live in a `Checks/` folder
of the language project, and the project references the behaviour band. `Projectional.Checks` itself stays a
separate package because it is *the catalogue*, shared by every language.

Shipped checks and the rules they encode:

- `STD001` (`PlaceholderNameCheck`, `Hint`) - name still equals the concept's declared default; the repair
  names it `<Concept>1` with the first free suffix, computed as a group so two unnamed rules get `Rule1`
  and `Rule2`. A concept with no default is not reported.
- `STD002` (`DuplicateNameCheck`, `Warning`) - two children of one slot under one name. The group is one
  slot of one node (roots are not a group), the **later** child is the one reported and offered a rename
  from suffix 2, and a child whose name is still its concept's placeholder is skipped - that is `STD001`'s
  finding. **A node anything in the project references is not reported at all**: the rename would dangle a
  reference the repair never saw, so the ambiguity is left to the constraint that reports it at the
  reference. The referrer scan is over `Project.Nodes`, not the model, because a reference may cross a
  model boundary.
- `STD003` (`SurroundingWhitespaceCheck`, `Warning`) - surrounding whitespace; `Trim to "..."`. A name
  that is only whitespace is skipped, because trimming it would produce the empty name.
- `STD004` (`IdentifierNameCheck`, `Warning`) - the name breaks the spelling rule the concept declares;
  the repair is `Use "..."`, which is `NameRule.Propose`. **The rule is a declaration, not a setting**:
  `Concept.NameRule` is `NameRule.AnyText`, `NameRule.Identifier` or one of the case conventions
  (`Pascal`, `Camel`, `Snake`, `ScreamingSnake`, `Kebab`, `ScreamingKebab`, `Train`, `Dot`, `PascalDot`)
  and stays **nullable**, because "nobody declared a rule" and "somebody declared `any`" are different
  facts and only the second survives inheritance - a default would let `Framework.Named` (which declares
  none) shadow a language's own rule.
  The case rules are one algorithm (`NameCase`: cut into the words the name carries, join them the way
  the convention says), so `Propose` is both the repair and the test - a name the rule accepts is the
  name it proposes, and a proposal always passes `IsValid`. **One state, one finding**: an empty name and
  one still equal to the concept's placeholder are `STD001`'s, whitespace around a name is `STD003`'s, and
  `STD001`/`STD002` respell their proposals with the concept's own rule so a repair cannot leave `STD004`
  behind. The declared form is the structure language's `named by rule:` cell, which enumerates
  `NameRule.All`, where an empty cell means inherit. The shipped languages use it: rules/states/features
  are PascalCase, an expression name camelCase, schema/table/column names snake_case, a declared type
  PascalCase and a record field camelCase.
- `STD010` (`EmptyChildSlotCheck`, `Hint`) - a `Cardinality.One` slot with no child; the repair is
  `InsertChildAction`, which is the same creation path `InsertNodeAction` uses, so the fix and Enter cannot
  disagree. A slot whose whole set is abstract has no repair and therefore no finding.
- `STD011` (`MinimumChildCountCheck`, `Hint`) - the list half: a `Cardinality.Many` slot whose declared
  `Minimum` is at least one and that holds nothing. The same `InsertChildAction`, which is why the two checks
  cannot drift from Enter either.
- `STD012` (`EmptyNodeCheck`, `Hint`, destructive repair) - a list entry that holds nothing but its defaults;
  the repair is `DeleteNodeAction(NodeId)`, which exists because this check needed a delete that names its
  node. Three exclusions make it usable: **a list entry only** (emptying a single-child slot is `STD010`'s
  finding), **a concept that declares a slot to fill** (a leaf is its value, and a default cannot be told from
  a typed value), and **nothing set anywhere**.
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

Intentions are *behaviour*: they produce edits, so they name `Editing`. Since
[ADR 0021](../../../docs/adr/0021-one-project-per-language.md) they live in an `Intentions/` folder of the
language they belong to, with their namespace unchanged: `Projectional.Expressions/Intentions/`
(negate), `Projectional.Samples/Intentions/` (accept this rule) and
`Projectional.Structures/Intentions/` (make abstract / make concrete).
`docs/architecture/context-actions.md` carries the plan; the preview and slices A-G are delivered, the last
of them being the range form of `INT006` (`Wrap selection in "X"`).

- **`Projectional.Intentions` is the framework's own catalogue**, not a language's: `StandardIntentions`
  derives `INT001` (rename), `INT002` (duplicate), `INT003` (delete), `INT004` (move up/down), `INT005` (move to
  front/back), `INT030` (insert before/after), `INT031` (sort by name), `INT008` (select node/parent), `INT006`
  (wrap in X), `INT007`
  (replace with X) and `INT020`
  (reset to the default) from
  the *shape* of the caret's place, so any language gains them by registering the provider. Every entry is
  **absent where it would change nothing** (the first node cannot move up, a root has no neighbour, a property
  already holds its default, a list is already in name order) and `Delete`, `Replace` and `Sort` are the ones
  marked `EditSafety.Destructive`, carried as
  information for a
  surface rather than used to hide it. `WrapperConcepts.Of` is the whole of
  "wrap in X": a concrete concept whose single-child slot accepts this node's concept and whose own concept
  fits the parent's slot (and whose `Applicability` allows that place) is a wrapper - the first such slot in
  declaration order, and one id per concept (`standard.wrap.<conceptKey>`) so two wrappers cannot collide in
  the list. A root is never wrappable, because a wrapper has to take the node's *place*.
- **`ReplacementConcepts.Of` is the whole of "replace with X"**, and it is a search for the same reason: a
  concrete concept that conforms to the slot, whose `Applicability` allows that place, **and is not the concept
  the node already is**. A slot declared over one concrete concept therefore offers nothing - swapping a step for
  a step would delete it and put an empty one in its place - and that emptiness is the answer, not a gap.
  `Model.Replace(node, conceptKey)` is the primitive: delete and create at one place inside one transaction, so
  undo restores the **old subtree with its own identities** rather than a rebuilt copy. It cannot be assembled
  from `DeleteChild` + `AddChild` in an action, because a single-valued slot cannot take the new child until the
  old one has left and the two halves would be two undo steps. What the old node held deliberately does not
  travel - no declaration says which slot of one concept corresponds to which slot of another - and the deletion
  clears the references into the subtree as any deletion does. Both the action and the entry refuse a **model
  root**: the document a session draws *is* a root, so replacing it would take away the node the editor is built
  from.
- **The app registers the catalogue as the host's**, and that is why `Expressions.Intentions` no longer ships
  "wrap in parentheses": the catalogue derives `Wrap in "Parenthesized"` from the same declarations, and two rows for
  one operation is the thing the registration order would have made visible. What a language keeps is what a
  declaration cannot express - "Negate" is a `Unary` born with `operator = "!"`, and no declaration says which of the
  two unary operators a bare `Unary` should start with. Do not "fix" that by giving the concept a default operator
  without deciding what a fresh `Unary` should be; declared intentions (M5) are the general answer.
- **A permutation is not a program of moves, so the model takes the order.** `Model.ReorderChildren(parent, slot,
  order)` validates that the order is a rearrangement of the slot's children (each exactly once) before touching
  anything, then detaches them all and inserts them back in the new order - one transaction, one undo step. It is
  written that way rather than as the shortest sequence of moves because every entry it produces is a pair the
  journal already undoes: the removals replay in reverse, each putting a node back at the index it left, and the
  original order falls out. An order that already holds journals nothing, so sorting a sorted list cannot become an
  undo step. **The rule stays out of the model**: `ChildOrder.ByName` (in `Projectional.Editing`, public because
  `Projectional.Intentions` derives availability from it) is ordinal, case-sensitive and stable, and answers
  `null` for a single-valued slot, for fewer than two children, and for a list where **any** element's concept
  declares no name slot - a partial sort would put the unnamed ones in an order nobody asked for.
  `SortChildrenAction` moves the caret with the node it was in, because an element address names a *position*, so
  leaving the caret alone would stand the user in whatever took that position.
- **An addressed selection is idempotent; the ladder's is not.** `SelectNodeAction(NodeId)` selects a node's whole
  range through `SelectionRanges.OfNode` - the same computation `SelectAllAction` uses, so a menu row and a keystroke
  cannot disagree about what "this node" covers. The two differ in one way on purpose: the ladder must not stall, so
  it **skips** a rung the selection already covers, while the catalogue rows are always there and re-establish the
  range (hiding them would make the row appear and disappear while the caret moves inside one node). A node whose
  notation draws no cells answers `Unchanged` and leaves the caret alone rather than collapsing onto nothing.
- **A caret jump is an action, not a mode of `MoveCaretAction`.** `SelectSlotAction(CellAddress)` moves the
  caret into a slot and selects its text (a bare caret when the slot is empty), answering
  `EditResult.Unchanged` because a navigation is not an edit. It is what makes rename plain rather than a
  refactoring: nothing in another model can break, because no model was touched. Its address is always a
  *cell*, since a rename names the value a user can see.

- **A range entry has its own id and its own search.** `Wrap selection in "X"` surrounds every node the
  selection covers completely (`IntentionContext.SelectedNodes`, which is `SelectionRanges.NodesIn` - the
  same computation Delete uses), through the **run form** of `Model.Wrap` and `WrapInAction`, so it is one
  undo step. Three rules: the id prefix is `standard.wrap-selection.` and not `WrapPrefix`, because a
  selection keeps the caret inside one of the selected nodes and both rows are therefore listed; fewer than
  two nodes is the per-node row's business; and the nodes must be **consecutive siblings**, which
  `WrapperConcepts.Of(nodes, ...)` asks exactly as the model would refuse it, so an offer is never an edit
  that comes back `Rejected`. A single node may go into a many-valued slot (a wrapper is born empty), which
  is why a language whose only composition is a list gains both rows.
- **`NodeMove` has four members, and two of them are extremes.** `ToFront`/`ToBack` are named rather than
  "up, several times", because a caller that asked for the front and got one step would run the action
  repeatedly - several undo steps for one intention. `MoveNodeAction` answers `Unchanged` when the target index
  is where the node already is, which is what lets an entry that survived a document change do nothing instead
  of pushing an empty step onto the history.
- **`InsertSiblingAction(node, place)` is `Enter`'s other half**, not a second `InsertNodeAction`: it names the
  node and the side so a menu entry does not depend on where the caret is, it creates **the sibling's own
  concept** (inserting beside a step makes a step - `InsertNodeAction` creates the slot's default instead), and
  it refuses a root and a single-valued slot with a reason.

- **A value with no cell needs an action that names the node, not the cell.** `SetPropertyAction` addresses a
  `CellAddress` and is right when the user can see the value; `SetNodePropertyAction` addresses a `NodeId` + slot
  and is right for a value the drawing hides - behind a conditional (`abstract`), on a collapsed node, or in a
  slot the notation leaves out. Both go through `EditContext.SetProperty`, so a bad value is refused identically.
  `SetNodePropertyAction` must check `FindProperty` **before** reading `GetOrNull` - asking a node for the value
  of a child slot throws `KeyNotFoundException`, which is exactly `Test.Rule`'s `body`.

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
  together, and it always opens a list - nothing is applied from the gesture, however few entries there are.
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

Text entry has two halves, and neither can be derived from the model.
[`ITypingRules`](../../../docs/architecture/language.md#typing-rules) answers for an **empty** slot;
a trigger declared on a concept answers for the edge of a node that already exists: `5` then `+` gives
`5 + <empty>`. `docs/architecture/triggers.md` is the design of record.

- **A trigger is declared on the concept it creates** (`ConceptDeclaration.triggers`), and does *not* name
  that concept. It names the anchor concept (`Concept.IsKindOf` decides, so declaring it over an abstract
  concept covers every concrete one), the `TriggerSide`, the single-child `AnchorSlot` that receives the
  node, and - both optional - the `PropertySlot` the typed text goes into and the `CaretSlot` the caret
  continues in. `ConceptRegistry.TriggersFor(Concept)` answers in **concept declaration order**, which is the
  only tie-break. Triggers are **not inherited** - a subtype reusing the gesture would create the super
  concept. There is no registry beside the language: one existed (`ITriggerRules`/`TriggerRegistry`) and was
  deleted, because a language that borrows `ConceptDeclarations` gets the gestures with them and has nothing
  to forward.
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
- **A gesture may be spelt over several keystrokes.** `ConceptTrigger.Presets` gives the concept values it is born
  with whatever was typed (`and` -> `operator = "&&"`), and the run of keystrokes is kept in
  `EditContext.PendingText`: seeded by `EditorSession.ContextFor` from the place the last keystroke stopped at,
  read back in `ApplyCore`, cleared by `Select` and by every action that is not a `TypeTextAction`. Nothing is
  applied and nothing is journaled while a word is being spelt, so the wrap is the only undo step. `TypeTextAction`
  asks `Candidates` - the run plus this character first, then this character alone - so one meaningless letter
  (`x` before `+`) can never block a gesture. There is no timer: the caret says when a word ends. A preset wins
  over the typed text where both name one slot, and a preset value the property refuses is dropped.
- **A wrong declaration is a load-time error, not a dead keystroke.** `ConceptRegistry.Create` validates every
  trigger: the anchor concept exists, `AnchorSlot` is a single child the concept declares and the anchor
  conforms to it, `PropertySlot` is a property and `CaretSlot` a child slot, every `Presets` key is a property,
  and the concept is concrete.
  At the keystroke `TypeTextAction.Fits` asks only the two things registration cannot know - does the concept
  conform to the slot the anchor stands in, and does `Applicability` allow it there - so a gesture that cannot
  run does nothing instead of reaching `Model.Wrap`, which would throw. A `PropertySlot` whose type refuses the
  text drops the text and still wraps, the same rule `AddChild(parent, slot, concept, propertySlot, text)` uses.
- **A structural keystroke gets no coalescing key.** `TypeTextAction.CoalescingKey` returns `null` when
  `WouldRestructure` - the property would refuse *and* a trigger claims the text - so `5`, `+`, undo
  gives back `5` and a second undo gives back nothing. `WouldRestructure` re-asks the property
  (`Compose` + `Type.IsValid`) because a key is read before the action runs; that duplication is
  deliberate and cheaper than threading a plan through the session.
- **A selection is never a trigger.** Wrapping a range needs a wrapper that can hold one, which is the
  intention list's job; typing over a selection stays "replace the text".
- The shipped catalogue lives on `Binary`, `Unary`, `Parenthesized` and `If` in `ExpressionsLanguage`: operators on
  either side become `Binary` (caret in the empty operand), `-`/`!` on the left become `Unary`, `(` on the
  left becomes `Parenthesized`, `and`/`or` on the right become `Binary` with `&&`/`||` as a preset, and `?` on
  the right becomes the `condition` of an `If`. `Binary`'s left trigger deliberately does **not** claim `-`, so the negation is
  the single reading of a leading minus without needing Unary to outrank Binary in the concept order.

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

### Three tooltips, one pointer

The control can show three boxes: the finding's message over a squiggle (`MarkerTip`), a concept's
description over any node (`ConceptTip`) and a slot's description over a property cell (`PropertyTip`) -
over an empty one always, over a filled one when the slot declared `showDescriptionWhenFilled`. All three
share `MarkerTipPresenter` and `MarkerTip.Place`, and they differ in exactly
two ways that must not be "tidied" away:

- **The marker wins.** A finding is specific and asks for an action; a description is ambient. If both
  would apply under one pointer, only the finding's box is painted. Never paint both.
- **Only the concept tip waits.** A squiggle is a shape a user aims at, so its tip is immediate; text
  is what the pointer crosses, so a description appears after 600 ms. A delay on the marker tip would
  make a hover feel broken.

All three are dismissed through the one path that already existed (`ClearHover`, reached by pointer exit,
any edit and any caret move). `ConceptTip.Of(session, nodeId)` and `PropertyTip.Of(session, position)`
answer `null` for something that no longer exists - a pointer can outlive its node, and a tooltip must
never throw.

**Both tips carry one shape**: `TipContent` is a heading plus a sentence, `TipLines.Of(content, width,
measure)` is the one wrapper, and the control has one painter for both. A second content type would mean
a second painter path.

- **The concept tip is anchored to the node's own address** (`new CellAddress(nodeId)`), not to the
  caret place the pointer resolved to, or it jumps while the pointer crosses a line of a node that spans
  several.
- **The property tip is anchored to the slot's cell**, and the property's sentence beats the concept's.
  A slot tip is about that one box - and a node drawn only as a property (an `IntLiteral` is one cell)
  has **no cell carrying its node address at all**, so anchoring a slot tip to the node would draw
  nothing. `ConceptTipBounds` therefore asks the slot address first and falls back to the node address
  (a fallback the real-window probe forced: the first implementation drew no box for a bare literal).
- **`PropertySlot.description` shows over an empty value without being asked for**, because an empty box is a
  question the user is asking; a filled one shows it only when the slot opted in
  (`PropertySlot.ShowDescriptionWhenFilled`, off by default), for text that does not explain itself. A slot born
  with a `defaultValue` reads as filled, since the model cannot tell a default from something the user typed -
  which is why the built-in languages opt in on their defaulted slots. The hover keys on **(node, slot)**, so
  moving between two properties of one node restarts the delay and moving inside one does not.

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
  accepts exactly four cell shapes: an empty `ChildCell`, an empty `CollectionCell`, a
  `PropertyCell` whose type names its values, and a caret **inside a leaf that fills a
  one-valued slot** - the last one is substitution (`Replace`), which offers what could take the
  child's place instead. A filled slot elsewhere or a constant gets nothing, and a child with a
  subtree of its own is never offered for replacement.
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

`Ctrl+A`/`Ctrl+C`/`Ctrl+X`/`Ctrl+V` select all, copy, cut and paste. Copy, cut and paste carry **either text
inside one property cell or a fragment of the model**; select-all widens. `docs/architecture/clipboard.md` is
the contract. Which of the two a gesture means is decided when it is pressed (`KeyRouter`), never stored: a
selection that covers nodes copies structure, a selection inside one value copies text, and a paste follows
the payload the clipboard holds, preferring the fragment.

- **The seam is `IEditorClipboard`**: `string? Text`, `ModelFragment? Fragment` and one default
  `Task RefreshAsync() => Task.CompletedTask`. It is not called `IClipboard` because Avalonia owns that name
  for its own abstraction, and the adapter is where the two meet. `null` means "no payload", so an empty
  clipboard is not an empty string, and **writing one payload clears the other** - a clipboard holds one
  thing, and a paste must act on what the user copied last.
- **Read and write are synchronous, and must stay synchronous.** A key arrives synchronously and
  an action applies synchronously; an asynchronous contract would put an `await` into every
  action that touches text. A platform whose clipboard can only be read asynchronously caches
  instead, and `RefreshAsync` is the hook that lets it pull the platform's text at a moment
  where waiting costs nothing - focus, or opening the context menu.
- **`RefreshAsync` returns `Task`, not `ValueTask`.** The callers that cannot await fire and
  forget, and a discarded `ValueTask` is an analyzer error because it may be pooled. A `Task`
  can be dropped without pretending anything was consumed. Do not "modernise" this signature.
- **`InMemoryClipboard` is what a session gets when the host supplies none.** It keeps both payloads, each
  write clearing the other; its `RefreshAsync` is the inherited default. Tests use it. A host with a real
  clipboard adapts it - `AvaloniaClipboard` in `Projectional.Avalonia/Input/` is that adapter, and it caches
  the platform's text so a synchronous `Ctrl+V` sees what another application put there. It also drops its
  fragment when a refresh reads text this editor did not write, because another application has written
  something since.
- **`SelectedText.Of(context)` is the single answer to "what does the selection say".** It is
  `internal`, clamps to the cell's length, and returns `null` unless the selection is inside one
  `PropertyCell`. Every text action goes through it; a second reading of the selection is a bug
  waiting to happen. Because `EditorSession.Select` already clamps, the clamp is unreachable
  through the public API - cover it through `CopyAction`/`CutAction`, not by calling it.
- **The actions are in `Projectional.Editing/Clipboard/`.** `SelectAllAction` climbs the
  ladder (see the editing section above) rather than selecting one cell. `CopyAction` writes the
  text and returns `EditResult.Unchanged` - a copy never changes the document. `CutAction`
  writes, then applies `DeleteTextAction(DeleteDirection.Forward)`. `PasteAction` hands the text
  to `TypeTextAction`, so a paste gets exactly the same validation and the same refusal as
  typing; an empty or null clipboard is `Unchanged` ("nothing to do"), never a rejection.
  `CopySelectionAction`, `CutSelectionAction` and `PasteFragmentAction` are the same three over
  nodes: `SelectionRanges.NodesIn` decides what is copied (the same computation Delete uses),
  Cut delegates the removal to `DeleteSelectionAction`, and Paste goes through
  `EditContext.InsertFragment`.
- **A fragment keeps what a paste has to put back.** `FragmentNode` records the concept key, the
  `Origin` id, the properties that were **written** (`Node.IsSet` - a defaulted value stays
  defaulted), both halves of every reference, and each child paired with its slot. `Origin` is not an
  identity - the copy gets a new id - it exists so that a reference *between* copied nodes is rebound to
  the copy; one that pointed outside keeps its id and `resolveInfo` and degrades to unresolved
  (ADR 0004). A value the target concept would refuse is an exception, never a skipped value.
- **A paste never replaces anything.** An empty slot takes the fragment (including an empty
  single-valued one), a node in a **list** takes it directly after itself, a node in a **filled
  single-valued slot** is refused with a reason (replacing is the labelled `Replace with "X"`
  entry), and a root is refused. Before anything is created the paste asks
  `ModelFragment.MissingConcept` and the slot's conformity, so a fragment from another language is
  refused by name instead of half-built.
- **A fragment does not leave the process.** There is no wire format: text is what crosses to another
  application. If that changes, the format belongs beside `ModelXml`, not in a second serializer here.
- **A paste never coalesces.** `PasteAction.CoalescingKey` is `null` (and `CutAction`'s too), so
  a paste is its own undo step and never merges into the typing around it. `ToString()` returns
  `"paste"`, which is what the undo label shows.
- **The keys are a third exception to "Control belongs to the host".** `EditorKeyMap.Map` returns
  `null` for Control combinations except the documented ones, and `A`, `C`, `X`, `V` join
  `Left`/`Right`/`Space` in that list. Shift does not change them: `Ctrl+Shift+C` is still Copy.
  A bare letter is never in the key map - it is text input, not a routed key.
- **The menu runs the routing, not a fixed action.** `EditorMenu.For` lists `Select All`, `Copy`,
  `Cut`, `Paste` after the edit entries, and each clipboard entry executes
  `session.Apply(KeyRouter.For(session, EditorKey.Copy))` (and so on) rather than constructing an
  action: the entry and its gesture cannot disagree about which payload they mean. `Select All` has
  **no** predicate - the ladder always has a rung left until the document is selected, and greying
  it out would make the node rung unreachable. `CanCopy` is true for text in a `PropertyCell` **or**
  for `KeyRouter.CopiesNodes(session)`; `CanPaste` is true for text of length **or** a non-empty
  fragment.
- **The Avalonia adapter resolves the platform clipboard lazily and swallows its failures.** A
  control that is not yet in a visual tree has no `TopLevel`, and a platform clipboard that
  refuses must not take the editor down with it - a copy that cannot reach the platform leaves
  the cached text alone. The control refreshes the clipboard on focus and before opening the
  context menu, and intercepts the platform's own paste so `Ctrl+V` goes through
  `PasteAction` (one undo step, one validation path) instead of the toolkit's default.
- **The shell hands one adapter to every tab.** `MainWindow._clipboard` goes into each `DocumentTab`
  (which passes it to the session) and onto each control. Without that, a subtree copied in one document
  could not be pasted in another, and - worse - the control would refresh an adapter the session does
  not read, so a text paste would use text the platform has already replaced.

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

`Projectional.Workbench/Documents/` also owns the **recent list**: `RecentFiles` is the value (newest first, no
repeats, ten entries, case-insensitive because a Windows path is) and `TextRecentFilesStore` is a text file under
`%LOCALAPPDATA%` with a path per line. The split is the point - the list owns the rules so "what counts as the same
file" is answered once, and the store owns the bytes and **never throws**: a missing, unreadable or unwritable file
answers with what is still true, because a shell that refused to start over its own convenience file would be losing
work to keep a menu. What the store reads back is validated by `RecentFiles.From`, since a file a user can edit is one
they will edit. There is deliberately **no `IRecentFilesStore`**: it existed for one commit, had exactly one
implementer, and the analyzer was right that it was an abstraction waiting for a second caller that never came - the
tests exercise the list and the store directly.

`Projectional.Workbench/Outline/` is the model browser: `Outline.Of(model)` lists every node, depth first, and the
window fills a flat list. It walks the **model**, not the projection - the opposite of `Finder`, and deliberately:
a browser answers "what is this document made of", so a concept with no notation is still part of it, while find
answers "where is this text on the page", which only the projection knows. Depth is counted from the root rather
than stored (a node can be moved), and the list is rebuilt on every model change like the findings. Both shell
features follow one pattern worth copying: **derive the rows in a non-Avalonia type, test it, leave the window as
wiring** - the problems panel is `ProblemList`, the browser is `Outline`.

`Projectional.Editing/Find/` is the search: `Finder.Find(session, query)` answers `FindMatch` values in document
order, `FindNextAction(query, direction)` selects the next one **with wraparound**, and the app's strip
(`Ctrl+F`, Enter/Shift+Enter/Escape) only counts and scrolls. Three rules are decisions, not details:
the search walks **caret places** (so a property the notation hides cannot be found - the rule the caret obeys)
and only **property cells** (a keyword is drawn from the notation, so a match in one is an offer no edit can
keep); matching is **ordinal**, never culture-aware; and the occurrence the last press selected is **skipped**
(`IsCurrent`), because a second press must move rather than find what the first one did - a test caught that rule
missing for backward search. `FindNextAction` is a navigation: no undo step, and a query that matches nothing
leaves the caret alone. **Replacing is an edit and therefore an action**, not bar logic: `ReplaceMatchAction`
rewrites one value's text, `ReplaceAllAction` does every occurrence in one transaction - one cell rewritten once,
replacements applied **right to left** so offsets stay valid, refused values skipped and counted, and a run that
wrote nothing rolled back so the history gains no empty step.

The problems panel is the model for a shell feature: `Problems/ProblemList.Of(model, findings)` turns the
diagnostics the editor is already drawing into rows, headless and tested, and `MainWindow` only fills a `ListBox`
and scrolls. Two rules in it are decisions, not formatting: **the order is the document's** (a severity-sorted
list renumbers itself while the user fixes things) and **a row carries a `CellAddress`** - the finding's slot when
it named one, else its node - because "jump to it" is `session.Select(Selection.At(row.Place))` and nothing more.
A finding whose node is gone still gets a row and sorts last; hiding it would claim the document is clean.
Add the next panel the same way: derive the rows in a non-Avalonia type, assert them, and leave the window as
wiring.

The repository ships two front ends and **one** public surface:

- `Projectional.Avalonia` is the embeddable control. Hosts reference it (which brings `Core`).
- `Projectional.Workbench` is the standalone desktop editor. It references `Projectional.Avalonia` **and** the language
  packages it composes (Samples, Structures, Editors, the reference languages and the three worked examples), each of
  which carries its own checks and intentions since
  [ADR 0021](../../../docs/adr/0021-one-project-per-language.md) - so the list is one entry per language rather than
  two or three. Its own types are `internal` and
  the project grants `InternalsVisibleTo` **to the test project**, so the shell's logic is testable headlessly without
  widening its public surface. What stays forbidden is the app *reaching past* the control into a layer it does not sit
  above by needing a reference for a type no public member mentions ([ADR
  0011](../../../docs/adr/0011-standalone-editor.md), `docs/architecture/standalone.md`).

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

`tests/Projectional.Core.Tests` is the main test project. It references `Core`,
`Language`, `Persistence`, `Projection`, `Editing` and - for the one pure type in it -
`Avalonia`. `TestLanguage.cs` (five concepts: `Test.Tag`,
`Test.Rule`, `Test.Body`, `Test.Step`, `Test.Note`) and `TestNotations.cs` (their notation, in
the same style a language author would write it) exist once. If a second test project
ever needs that language, promote it to a `Projectional.TestLanguage` library - do not copy
it. **Baseline is 2362 tests**; anything below that means tests were deleted.

`tests/Projectional.Toolkit.Tests` is the second test project and is deliberately unlike the first: it
references **only** `Projectional.Toolkit`, and declares its own two-concept language (`BoardLanguage`,
`BoardNotations`) instead of borrowing the fixture. Its four tests are the compile-time proof of
[ADR 0019](../../../docs/adr/0019-toolkit-is-the-language-surface.md) - a language that needed anything the
toolkit does not bring would not build. Do not add a reference to it to "make an assertion possible"; that
would delete the only test of the surface itself.

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

The self-contained single file is a **publish** shape, not a build shape - the single-file
properties in the Workbench csproj are guarded by `'$(PublishSingleFile)' == 'true'`, so a normal
build stays framework-dependent and multi-file:

```powershell
dotnet publish src/Projectional.Workbench -c Release -r win-x64 -p:PublishSingleFile=true -o publish
./publish/Projectional.Workbench.exe --self-check
```

Three things make "one file" true, and each was found by publishing and looking:
`IncludeNativeLibrariesForSelfExtract` (Avalonia's native dependencies), `DebugType=none` plus
`AllowedReferenceRelatedFileExtensions=none` (a portable PDB per referenced project), and a
`RemovePublishPdbs` target (the native PDBs SkiaSharp and HarfBuzz ship, which the native-asset copy
step brings along whatever the reference settings say). `--self-check` runs **before Avalonia
starts**, so it needs no display: it builds the shell's language registry, projects a document and
prints the counts. The `single-file` CI job publishes for `win-x64` and `linux-x64`, asserts
`publish/` holds exactly one file, and runs the check.

### Packing

```powershell
dotnet pack Projectional.slnx -o artifacts
```

Every project under `src/` is packable, so this produces **27 packages** (`.nupkg` + `.snupkg`) and nothing
else; tests, the screenshot tool, the host sample and the language template opt out. `artifacts/` is gitignored.
A local pack needs no tag and no credentials and produces prerelease versions - that is the correct answer,
because nothing was released. The version is derived by GitVersion from the commits and the `v*` tags, so a
package is reproducible from the commit it names. See [packaging.md](../../../docs/architecture/packaging.md).

`global.json` also carries `"test": { "runner": "Microsoft.Testing.Platform" }`. Without it `dotnet test` on this
SDK falls back to VSTest and every xunit.v3 project fails with "Testing with VSTest target is no longer
supported". A template that ships a `global.json` must carry that section for the same reason - and only that
section, never an SDK pin, or a generated project would refuse to build on a machine with a different SDK.

### The language template

`templates/projectional-language/` is a `dotnet new` template, not a solution project: it is not in
`Projectional.slnx` and `dotnet build` never touches it. Verify it the way it is used, in a folder outside the
repository:

```powershell
dotnet new install .\templates\projectional-language --force
dotnet new projectional-language -n Demo -o "$env:TEMP\Demo"
# swap the template's PackageReference for a ProjectReference - the package is not published
dotnet test "$env:TEMP\Demo\Demo.Tests"
dotnet new uninstall .\templates\projectional-language
```

Two defects were found exactly that way and are invisible by reading: **the SDK's compile glob reaches into the
sibling test folder** (the language csproj must `Compile Remove` it, or the tests compile into the language), and
the runner opt-in above. Regenerating is the only honest test of a template - `--force` on install is required
because `dotnet new` caches by identity, not by content.


- All shared properties live in `Directory.Build.props`; all package versions in
  `Directory.Packages.props`. A `.csproj` should not repeat `TargetFramework` and should not
  carry a `Version`.
- Warnings are errors, code style is enforced during build, and analysis runs at
  `latest-recommended`. Fix the cause; do not add `NoWarn` to get a green build.
- Tests use xunit.v3 on Microsoft.Testing.Platform, so a test project is an executable.
- **Every project under `src/` is a NuGet package** ([ADR 0022](../../../docs/adr/0022-every-project-is-a-package.md),
  [packaging.md](../../../docs/architecture/packaging.md)). `IsPackable=true` is the repository default and the
  metadata lives in `Directory.Build.props`; only the four non-products opt out (`tests/`, the screenshot tool,
  the host sample, the language template). A new project is therefore a package unless it says otherwise, and
  the CI `pack` job's "exactly 27 packages" assertion must be updated in the same commit that adds or removes
  one.
- **The version comes from the repository, never from a literal.** `GitVersion.yml` derives it from the
  commits and the `v*` tags; `<Version>0.1.0</Version>` in `Directory.Build.props` is only the no-git
  fallback. Do not set `tag-prefix` in `GitVersion.yml` - with `next-version` present it makes GitVersion 6.8.2
  fail with "Failed to parse ... into a Semantic Version". Every CI job that builds needs `fetch-depth: 0`, or
  GitVersion reads a shallow history and computes a wrong version. A cached `obj/gitversion.json` outlives a
  tag change, so a version that looks wrong after a tag was added or removed usually means "delete the `obj`
  folders and build again".
- **Only a `v*` tag publishes.** A merge builds and packs but never pushes; `publish-nuget` is guarded by
  `startsWith(github.ref, 'refs/tags/v')` and needs the `NUGET_API_KEY` secret in the `nuget` environment.

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
- Do not make `IEditorClipboard` asynchronous. A synchronous key needs a synchronous clipboard;
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
- Whenever a change touches the model, check the seven invariants in
  `docs/architecture/model.md` still hold: unique ID per project, exactly one parent or the
  root, child slot conforms to the concept, no reference into the referencing node's own
  subtree unless allowed, removing a node clears incoming references, every property value
  parses, and a concept only stands where its `Applicability` allows. They are enforced on every
  mutation (`ModelInvariantException`), not merely asserted in debug builds, so a green test run
  means the checked ones hold - the structural ones (invariants 1 and 2) still need a thought.
- **A reference may cross a model boundary but not a project one.** `Node.SetReference` compares
  `Project`, not `Model`: two documents of one project share the node index, and the scope provider
  already offers their nodes by name. Do not "restore" a model-level check - it is what made a
  cross-document foreign key impossible until the SQL schema example found it.

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
