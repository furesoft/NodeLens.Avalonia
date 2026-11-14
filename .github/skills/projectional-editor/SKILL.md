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
implementation has started at milestone M0.

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