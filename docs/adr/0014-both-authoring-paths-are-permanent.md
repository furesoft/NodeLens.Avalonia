# ADR 0014 — Languages are defined in code and in the project, both permanently

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR 0007](0007-reflection-free-language-definition.md),
  [ADR 0008](0008-bootstrap-order.md), [language.md](../architecture/language.md),
  [language-authoring.md](../architecture/language-authoring.md)

## Context

[ADR 0008](0008-bootstrap-order.md) fixed the bootstrap order and listed its costs. One
sentence in that list reads:

> The built-in bootstrap is throwaway code, kept only until stage C and then demoted to a
> compatibility fallback.

Taken on its own, that sentence says code-first authoring is scaffolding: once a language can
be declared inside the project, the C# path is a leftover to be retired. Three facts say the
opposite, and they are structural rather than sentimental.

1. **Behaviour is code.** A declared language is data and cannot execute anything
   ([integration.md](../architecture/integration.md#language-plugins)); that is a security
   property, not an implementation detail. But every aspect that *decides* something -
   constraints, scopes, type rules, generation, interpretation - is behaviour. A declared
   language can hold the declarative *forms* the framework lowers, and nothing else. The day
   the code path is retired is the day every aspect without a declarative form loses its only
   home.
2. **Distribution is a package.** A host that embeds a DSL installs a NuGet package; it does
   not open our workbench, and it must not need our editor to load its own language. Removing
   the code path removes the shipping path.
3. **The code path is the oracle.** The cheapest proof that the declared path is faithful is a
   test that builds the same language both ways and compares the results. Delete the code
   declarations and the comparison has no reference to compare against - the parity test would
   degrade into "whatever the declared path does is correct", which is exactly how a second
   implementation silently drifts.

## Decision

**Two authoring paths, both permanently supported, at the same standing.**

| Path | Written in | Can express | Ships as | Milestone |
| --- | --- | --- | --- | --- |
| **Code** | C# fluent declarations built from `Projectional.Toolkit` | every declaration *and* every behaviour | a NuGet package | M1 |
| **Project** | structure, editor and aspect models in the project, edited in the workbench | declarations only, no code | project files | M2 (provisional), M5 (MPS-shaped) |

Rules that follow from that, and that a pull request has to respect:

1. **One runtime, two front ends.** The declared path is lowered to exactly the objects the C#
   builders produce: the same `ConceptDeclaration`, the same `Notation` cells, the same aspect
   delegates. There is no second registry, no second notation engine and no declared-only node
   kind. A language's origin is visible in its project files and nowhere in the model.
2. **Nothing may exist only in the declared path.** A capability the workbench can express has
   to be expressible in C# in the same commit, because the parity test asserts it and because a
   language that no package can carry is not shippable. This is the rule that keeps the
   workbench from growing into a tool that produces languages only it can load.
3. **Behaviour keeps its monopoly in code.** An aspect with no declarative form is a C# aspect,
   and adding the declarative form (M6 for constraints, types, generators) narrows *how* it is
   written, never *whether* code can express it. `Validate`, `TypeOf`, `ScopeProvider`,
   `Evaluate` and `GenerateText` remain delegates a C# language passes; a declared aspect is
   lowered into such a delegate at load time.
4. **The core languages keep their C# declarations after stage C.** They stop being the only
   definition and become the parity oracle, the fail-safe when the bootstrap cannot load, and
   the reference a language author reads. "Throwaway code" is hereby replaced by "oracle" for
   the C# half; the staged bootstrap order of ADR 0008 itself is unchanged.
5. **Declared to C# is a planned direction.** A language prototyped in the workbench must end
   up shippable as a package without being re-typed by hand, so export is part of the work
   ([language-authoring.md](../architecture/language-authoring.md#export-declared-to-c)).

## Consequences

### Positive

- A host never needs the authoring tool, and an author never needs a compiler for a notation
  tweak. Each path is used for what it is good at instead of being a temporary stage of the
  other.
- The parity test becomes a real specification: a document rendered through both definitions
  must produce identical bytes, and the same diagnostics. A declared feature that the C# side
  cannot express fails a test instead of surfacing as a support question.
- The bootstrap-failure fail-safe in ADR 0008 stops being a fallback nobody exercises: the code
  declarations are compiled and tested on every push, so the emergency path is the tested path.
- Export gives the workbench a reason to exist beyond editing: it becomes the fastest way to
  *design* a language that later ships as a package.

### Negative / costs

- Two paths exist for the lifetime of the project, so every new language feature costs two
  implementations - one of which (the C# side) is often the only complete one. Accepted: the
  alternative is a feature with no shipping story.
- The parity corpus needs maintenance: each core language needs its declared twin, and the pair
  has to be kept in step. It is the same cost ADR 0008 already accepted between M2 and M5, now
  made permanent.
- Export can lose expressiveness: a construct that lowers to a delegate has no source form to
  write back. Export therefore emits declarations plus explicit `throw` stubs for anything that
  was behavioural, and a language author finishes those by hand. Silent loss is not allowed.

### Rules

1. A declared capability without a C# equivalent is a defect, not a feature.
2. A C# capability may exist with no declared form; the reverse may not.
3. The declared twin of a core language lives in the test corpus, not in the product.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Declared only, retire the C# path at stage C** | Loses the only place behaviour can live, the only shipping story for a host, and the only oracle for the parity test. It also makes the fail-safe load path dead code. |
| **Code only, drop self-hosting** | Already rejected in [ADR 0008](0008-bootstrap-order.md): DSL authors would need the .NET SDK and a rebuild for every notation tweak. |
| **The declared model is the source, and code is generated from it and never edited** | A generated file cannot be the source of truth for a language whose aspects are handwritten C# ([ADR 0002](0002-model-as-source-of-truth.md)); the two halves would diverge on the first behaviour change, and a host debugging a registry it cannot read is worse off than one that can. |
| **Only the declarative subset, no C# builder at all** | Would make the bootstrap impossible: stage A is built entirely from the C# builder, and there is no editor before the first language exists. |
