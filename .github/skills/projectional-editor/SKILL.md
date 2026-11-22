---
name: projectional-editor
description: Architecture invariants, working conventions, build commands and Windows tooling pitfalls for the Projectional repository - an MPS-style projectional editor on .NET, with one guide file per layer beside this index. Use when implementing, testing, reviewing or committing anything in this repo, so that new code does not break the decisions already frozen in docs/adr.
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
`Projectional.Language/Import/` with `Projectional.Json` as its first reference language) are done, as is
find usages (a reverse reference index in `Projectional.Core`, the caret query in `Projectional.Editing` and the
app's usages panel), and the rename refactoring that index was built for (`RenameNodeAction`: the name plus every
reference fallback in one undo step, with **Edit ▸ Rename...** in the shell), and go to definition (`Ctrl+B`,
`Ctrl+click` and the context menu). The
standalone shell (tabs, menu bar, status bar, a problems panel) is being delivered.

**The app is `MainWindow`, not a `ShellView`.** `standalone.md` and ADR 0015 describe a shell that is a
library (`ShellView`, `ShellWindow`, `IShellStorage`, `IShellDialogs`, `ProjectDriver`, `CommandRegistry`).
Four of those now exist: `ShellCapability`, `ShellFileType`, `UnsavedChoice`, `ShellOptions`,
`IShellStorage`, `IShellDialogs`, `ShellCommand` and `CommandRegistry` are public in
`Projectional.Avalonia/Shell/`, while the file-system and window implementations stay in the app.
`ShellView`, `ShellWindow` and `ProjectDriver` do not exist yet - ADR 0015 puts that extraction at M6. Read
those documents as the target shape, and add M2 features to `MainWindow` rather than inventing a
`ShellView` on the way. **A new command goes in `MainWindow.RegisterCommands`** and its handler dispatches
through the registry (`RunCommand("name")`); do not add a capability check to the handler body, because
the registry is the one enforcement point.

## Status and where the truth lives

- `docs/roadmap.md` - milestones M0-M8 with acceptance gates and effort estimates (M8 is after 1.0). It holds the **scope** of each milestone and what is left, not a status report; a finished milestone is removed from it.
- `docs/implemented.md` - the record of delivery: every finished feature, where it lives and the test that proves it, milestone by milestone.
- `docs/architecture/` - model, language, projection, editing, persistence, semantics, integration, standalone, plus one document per planned feature: `completion.md`, `markers.md`, `node-moves.md`, `scrolling.md`, `import.md`, `csharp.md`, `llm-language-description.md`.
- `docs/adr/` - 22 accepted decisions. **Frozen.** To change one, write a new ADR that supersedes it.
- `docs/open-questions.md` - open items, each with a recommendation and a decide-by milestone.

A milestone is done when its acceptance gate passes, not when the code compiles.

## Where the rest of this guide lives

This file is the index. It holds what must never be violated, how the code is laid out, and how to
build and test - the parts a session needs before it knows what it is touching. Everything else has
moved into the files beside this one, one per layer: **read the one you need, not all of them.**

| File | Read it when you touch | What is in it |
| --- | --- | --- |
| [guide-core-model.md](guide-core-model.md) | `Projectional.Core`, `Projectional.Persistence` | how a mutation records its undo closure, `Model.CopyTo`/`InsertCopy`/`ModelDiff`, undo/redo and coalescing, the byte-stable XML writer, `ReferenceIndex` and the refactorings built on it |
| [guide-language.md](guide-language.md) | `Projectional.Language` - notation, variants, triggers | the inline/block split, every notation cell, why `Conditional` may draw nothing, variants and tables, and `ITypingRules` |
| [guide-projection.md](guide-projection.md) | `Projectional.Projection` - tree, layout, snapshot | addresses and the builder, the measure/arrange rules, what a layout remembers, and the headless renderer |
| [guide-editing.md](guide-editing.md) | `Projectional.Editing` - actions, intentions, completion, clipboard | the caret/selection rules, text and structural actions, the fix and intention catalogue, the offer list, completion, clipboard and the intention preview |
| [guide-semantics.md](guide-semantics.md) | scopes, constraints, types, generation, interpretation | the name index and the scope chain, what a constraint may and may not do, `IType.Accepts`, the twelve template parts, and the interpreter's budget |
| [guide-avalonia.md](guide-avalonia.md) | `Projectional.Avalonia`, `Projectional.Workbench` | what the control owns and what it must not, input routing, the three palettes and the three tooltips, emphasis, and the shell's panels and rules |
| [guide-languages.md](guide-languages.md) | a language package, or text import | the definition languages and `LanguageProject`, the reference languages' decisions, the import contract, and why a construct is kept as raw text |
| [guide-tooling.md](guide-tooling.md) | tests, the SDK, or a failing command | how the test projects are organised, the C# 14 feature list, Windows/PowerShell pitfalls, and the runtime-probe recipe |

A rule that a session must not break and cannot find here is a defect: add it to **this** file. A
rule that only matters inside one layer belongs in that layer's guide.

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
    injects `Framework.Named` and `Framework.BaseConcept` into every registry, so a language cannot ship without them
    and a host can rely on those keys meaning the same thing in every document
    (`docs/architecture/interfaces.md`). Every concept implements `Framework.BaseConcept`, which is what lets a slot be
    declared over "any node".
    A separate flag, `ConceptDeclaration(..., isOfferedInCompletion: false)`, keeps a concept out
    of the completion **list only**. `DefaultInstantiableFor` must keep ignoring it: a hidden
    concept stays a legal child reachable with Enter, which is exactly how `Expressions` keeps
    its three literals usable while leaving them out of the menu. Never "fix" the two into one
    rule; `CompletionSource.Proposals` filters on both, the default fill on neither.

## Working conventions (explicit user requirement)

- **A new aspect extends `samples/declared-atlas`.** One declared sample holds every aspect the framework knows, so the
  aspects are proven to compose; a guard test reads `LanguageProject.AspectFolders`, so an aspect not added there is a
  red test rather than a feature nobody exercises ([full-sample.md](../../../docs/architecture/full-sample.md)). The
  per-axis samples stay - they explain one thing at a time.

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
- **Write the language the SDK offers, not the one the file happens to use**: `LangVersion` is `latest`,
  which on the pinned SDK is C# 14. The feature list a change should reach for (collection expressions,
  `params ReadOnlySpan<T>`, `field`, primary constructors, pattern matching) is in
  [guide-tooling.md](guide-tooling.md#language-level-explicit-user-requirement).

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

## Build and verify

`global.json` pins SDK **10.0.403** with `rollForward: latestFeature` because SDK 11 is the
machine default. Do not remove it.

```powershell
dotnet build
dotnet test
```

To measure rather than argue, `tools/Projectional.Benchmarks` reports layout, constraints, checks and one
rechecked node per document size, for a language without references and one made of them:

```powershell
dotnet run -c Release --project tools/Projectional.Benchmarks   # best of five, pass N as the first argument
```

Release only - a Debug build reports numbers that are true of a Debug build - and the numbers belong in
`docs/architecture/semantics.md` §What a full recheck costs, not in a comment.

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

Every project under `src/` is packable, so this produces **34 packages** (`.nupkg` + `.snupkg`) and nothing
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
  the CI `pack` job's "exactly 34 packages" assertion must be updated in the same commit that adds or removes
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
