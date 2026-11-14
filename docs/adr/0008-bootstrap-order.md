# ADR 0008 — Built-in languages before self-hosting

- Status: Accepted
- Date: 2026-11-14
- Related: [language.md](../architecture/language.md#bootstrap-stages), [roadmap.md](../roadmap.md)

## Context

The goal is a workbench where languages are defined *inside the editor*, the way MPS
does it. That is circular: the editor that defines languages must itself be driven by a
language definition, and there is no language definition to start from.

Three ways out:

1. Start self-hosted: define the structure language in its own terms from day one.
2. Start built-in: hardcode a subset in C#, then describe the rest with itself.
3. Skip self-hosting entirely: languages are always C# code.

## Decision

**Stage the bootstrap explicitly**, with the order fixed:

| Stage | Exists | User can | Milestone |
| --- | --- | --- | --- |
| A | built-in languages in C#, generic renderer | edit models | M1 |
| B | concepts/notation as declared data | add a language without C# | M2 |
| C | structure/editor language is a declared language; bootstrap XML shipped | edit languages in the editor | M5 |

Stages are not throwaway: stage B's declared language mechanism is exactly what stage C
uses to describe itself. Stage C only replaces the *content* of the built-in bootstrap
with data files, in a small, well-tested step.

## Consequences

### Positive

- Every milestone is usable on its own; no "nothing works until the bootstrap is done"
  valley.
- The hardest part (self-hosting) happens when the mechanism is proven by real DSLs, not
  before.
- The stage C switch is verifiable: load the bootstrap XML, compare behaviour against
  the built-in declarations.
- Two structures (built-in and declared) exercise each other; a missing aspect in one
  surfaces quickly in the other.

### Negative / costs

- The built-in bootstrap is throwaway code, kept only until stage C and then demoted to
  a compatibility fallback.
- Two representations of the same languages exist between M2 and M5; keeping them in
  sync needs a golden test that renders the same model via both paths.
- The bootstrap XML becomes a load-bearing artifact: if it breaks, the workbench cannot
  open its own languages. It gets the strictest corpus and a fail-safe fallback to the
  built-in declarations.

### Rules

1. The stage C bootstrap is versioned and migrated like user data — never hand-edited in
   place.
2. The fail-safe: if the bootstrap cannot load, the workbench starts with built-in
   declarations and reports the problem instead of refusing to start.
3. No language may depend on a stage C feature before M5.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Self-hosted from day one** | Requires a working editor, layout engine, parser-free notation mechanism *and* a self-description — all before anything is visible. Extremely high risk of never shipping. |
| **C#-only languages, no self-hosting** | Cheap and defensible, but abandons the stated goal: DSL authors would need the .NET SDK and a rebuild for every notation tweak. |
| **Grammar-based bootstrap (MPS' `SModel` written in a text DSL)** | Reintroduces a parser and a grammar toolchain, contradicting [ADR 0002](0002-model-as-source-of-truth.md). |
