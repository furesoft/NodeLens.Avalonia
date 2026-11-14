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
in `Projectional.Language` and the runtime editor tree in `Projectional.Projection/Tree/` are
done; layout, snapshot rendering, edit actions, the expressions language and the Avalonia
control follow.

## Status and where the truth lives

- `docs/roadmap.md` - milestones M0-M7 with acceptance gates and effort estimates.
- `docs/architecture/` - model, language, projection, editing, persistence, semantics, integration.
- `docs/adr/` - 10 accepted decisions. **Frozen.** To change one, write a new ADR that supersedes it.
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
- `EditorTree.Find(address)` returns the **first** match in document order - usually the
  outermost container carrying a node address. `EditorTreeWalker` is the single place that
  defines document order; a second walk that disagrees with it is a caret bug.

## How the tests are organised

`tests/Projectional.Core.Tests` is currently the only test project. It references `Core`,
`Language`, `Persistence` and `Projection` so that `TestLanguage.cs` (five concepts: `Test.Tag`,
`Test.Rule`, `Test.Body`, `Test.Step`, `Test.Note`) and `TestNotations.cs` (their notation, in
the same style a language author would write it) exist once. If a second test project
ever needs that language, promote it to a `Projectional.TestLanguage` library - do not copy
it. **Baseline is 261 tests**; anything below that means tests were deleted.

- **The corpus is the file format.** `tests/Projectional.Core.Tests/Corpus/*.mps` are
  documents the writer produced once and that now have to load and save back to the same
  bytes. They are copied to the output directory by a `<None Update="Corpus\**\*.mps"
  CopyToOutputDirectory="PreserveNewest" />` item. **Never regenerate a corpus file to make
  a test pass** - a byte that moves is a wire-format change and needs an ADR. The files must
  stay LF, BOM-free and without a trailing newline.
- **Notation is tested as text, not pixels.** `Projectional.Projection/Snapshot/` holds
  `SnapshotGrid` (a character grid that refuses to draw past its edge rather than clipping)
  and `LayoutSnapshot.Render(model, width)`. `Render` is the M0 stub: it prints one
  indented line per node until projections exist. The signature is the contract; only the
  body changes. Both types are `internal` per ADR 0010, reached from tests through
  `InternalsVisibleTo`.
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

## Do not

- Do not modify `LICENSE`. It must stay byte-identical to the canonical Apache-2.0 text
  (SHA-256 `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`).
- Do not "improve" the XML format without a new ADR and a migration - existing `.mps` files
  would stop round-tripping byte-identically.
- Do not put Avalonia types, `Dispatcher`, or `Control` references into `Projection` or `Editing`.
- Do not introduce reflection or source generators for language definition.
- Do not reorder children or renumber IDs during save.

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