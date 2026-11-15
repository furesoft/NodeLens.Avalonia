# Roadmap

Sequenced for a single developer working part-time, with the architecture designed for
the full scope from the start. The order matters more than the schedule: each milestone
produces something usable, and no milestone depends on a later one being finished.

## Working assumptions

- One developer, part-time (roughly 8–12 h/week).
- Effort figures are *engineering* estimates, excluding documentation polish and the
  one-week usability gate after each milestone.
- A milestone is done when its **acceptance gate** passes, not when its code compiles.
- After each milestone: one week of using the result for something real, then fix what
  annoyed you before starting the next one. Skipping this is how feature-complete,
  unusable editors happen.

## Milestones

### M0 — Core model and persistence

*Goal: models exist, survive a round trip, and every later layer has a foundation.*

| Delivers | Details |
| --- | --- |
| Solution skeleton | `Core`, `Language`, `Projection`, `Editing`, `Semantics`, `Avalonia`, `Workbench`, tests |
| Node/model/project model | slots, identity, invariants, registries |
| Change events | transactions, inverse changes, coalescing |
| XML persistence | deterministic writer/reader, project layout, atomic save |
| Test harness | round-trip corpus, layout snapshot harness, CI |

**Gate:** a model created in code survives save → load → save byte-identically; undo
and redo restore exact model states; invariant violations fail debug assertions.
*Effort: 2–3 weeks.*

**Delivered.** Invariants are enforced on every mutation rather than by assertion, and the
stored form is pinned by a round-trip corpus that CI runs on every push. What M0 actually
implements is recorded in [persistence.md](architecture/persistence.md#what-m0-implements)
and [testing.md](architecture/testing.md).

### M1 — Projection and editing on a built-in language

*Goal: an editable editor with a text-like notation, no language authoring yet.*

| Delivers | Details |
| --- | --- |
| Editor tree + layout | cells, measure/arrange, line-based layout |
| Editing layer | caret, edit actions, key routing, undo/redo — **delivered** |
| Headless snapshot render | ASCII grid, used by all notation tests — **delivered** |
| Avalonia control | rendering, scrolling, virtualization, caret, selection — **delivered** |
| Input | typing, arrows, tab, enter, delete, undo/redo — **delivered** |
| Syntax highlighting | a semantic style per cell, a host-supplied palette, no colour in the model — **delivered** |
| Text decorations | bold/italic/underline/strikethrough decided by the model, per cell — **delivered** |
| Notation variants | one concept, more than one notation (block, table), chosen at projection time and never persisted ([ADR 0012](adr/0012-concept-rendered-more-than-one-way.md)) — **delivered** |
| Built-in language | minimal `Projectional.Expressions` + a toy block language (`Projectional.Samples`) — **delivered** |
| Standalone shell (thin) | `Projectional.Workbench`: open project, model tabs, edit, save, dirty prompt. Public API only ([standalone.md](architecture/standalone.md)) |

**Gate:** a user can edit a nested model with a text-like look, using only the
keyboard, without the caret ever getting lost; typing latency and incremental layout
are within budget on a 1000-line model. Additionally: **the standalone app opens the
toy language, edits a model and saves it, without referencing an internal assembly** —
the first end-to-end check of the embedding API.
*Effort: 6–10 weeks. Highest technical risk — spike custom text layout and hit testing
first.*

### M2 — Declarative languages

*Goal: a new language is added as data, without C# code.*

| Delivers | Details |
| --- | --- |
| Concept declarations as nodes | structure language (stage B) |
| Notation declarations as nodes | editor language (stage B) |
| Generic renderer | renders any declared concept |
| Tables | grid cell, column sizing, cell caret, add-row affordance |
| Language loading | declared languages from the project directory |
| Shell grows | model browser, problems panel, find/replace, recent projects, self-contained single-file build |

**Gate:** a small DSL (including a table) is defined entirely in project files and is
editable with correct completion-free structural editing; deleting the language's C#
code changes nothing about what the user sees. The declared DSL can be opened, edited
and saved in the standalone app, which no longer needs C# to know the language.
*Effort: 6–8 weeks.*

### M3 — References, scopes and completion

*Goal: it starts to feel like an editor instead of a structured form.*

| Delivers | Details |
| --- | --- |
| Scopes | chain, defaults, per-language overrides, ambiguity diagnostics |
| Reference cells | resolve, create-on-the-fly, unresolved state, chooser popup |
| Completion | scope-driven, prefix filtering, insert-and-continue |
| Substitutions | transformation menus, wrapping, keyword-triggered insertion |

**Gate:** referencing an element in another model works; renaming a target does not
break models thanks to `resolveInfo`; wrapping a selection in an `if` is one undo step.
*Effort: 6–8 weeks.*

### M4 — Semantics and the pilot DSL

*Goal: the first real DSL works end to end.*

Pilot: **rule / decision tables** (business rules with conditions, outcomes, tables).

| Delivers | Details |
| --- | --- |
| Constraints | incremental validation, markers, quick fixes |
| Types | basic type system with `Unknown` semantics |
| Text generation | template trees, traceability map |
| Interpreter | evaluation with trace and step budget |
| Cross-check tests | generated code and interpreter agree on the corpus |

**Gate:** a user writes a decision table, sees errors immediately, runs it in the
preview and generates C# from it — both inside the embedded control in a small host
application and in the standalone app, proving the two front ends share the behaviour.
*Effort: 6–8 weeks.*

### M5 — Self-hosting

*Goal: the MPS moment — languages are edited in the editor itself.*

| Delivers | Details |
| --- | --- |
| Structure language (stage C) | concepts, slots, superconcepts as an editable model |
| Editor language (stage C) | notation and styles as an editable model |
| Live preview | edit a language definition, see the effect on a model next to it |
| Bootstrap | versioned bootstrap XML, migrated with user data |
| Workbench shell | the standalone app grows into the workbench: language browser, live preview, output panel, language project creation |

**Gate:** a new DSL is created from an empty project inside the workbench, including
its notation, and used immediately — no C# and no restart. The workbench is the same
application that shipped in M1, not a second one
([ADR 0011](adr/0011-standalone-editor.md)).
*Effort: 10–14 weeks. Second-highest risk: bootstrap breakage can lock the workbench
out of its own languages, so the bootstrap corpus gets the strictest tests.*

### M6 — Framework maturity

*Goal: things that make it viable for other people's projects.*

| Delivers | Details |
| --- | --- |
| Declarative constraints/types | lowering to the existing aspect delegates |
| Refactorings | rename, extract, inline, move — as edit-action programs |
| Migrations tooling | migration language, golden-file corpus |
| Model-aware diff/merge | 3-way merge keyed on node IDs |
| Model-to-model generation | optional second generator stage |
| Plugin packaging | one NuGet per language, versioned |

**Gate:** a model can be renamed project-wide without breaking references, and two
branches editing different parts of one model merge without hand-editing XML.
*Effort: several months, split into independently shippable slices.*

### M7 — Release hardening

| Delivers | Details |
| --- | --- |
| Performance | profiling pass, incremental layout and validation tuning |
| Accessibility | automation peer, keyboard-only navigation review, contrast |
| IME | `TextInputMethodClient`, composition handling, CJK testing |
| Docs and samples | tutorial DSL, embedding guide, language-author guide |
| Packaging | NuGet, CI matrix, public API baseline, AOT/trimming validation |
| App distribution | self-contained installers per OS, file association for `.prj`, winget/Homebrew manifests |

**Gate:** a developer unfamiliar with the project builds a small DSL by following the
tutorial, without asking questions — and a user who does not write C# can install the
standalone editor and work with an existing DSL.

## What 1.0 means

1.0 does **not** mean MPS parity. It means:

- one developer can define a non-trivial DSL with notation, constraints and a text
  generator, without touching framework internals;
- a host application can embed the editor as a documented component and rely on its
  public API;
- **there is a standalone editor you can download, install and run** — the framework is
  usable without writing a host app at all;
- models survive editing by hand in git, including non-overlapping merges;
- the known gaps are documented ([open-questions.md](open-questions.md)) instead of
  being surprises.

## Deliberately out of scope before 1.0

| Excluded | Why |
| --- | --- |
| Multi-user / real-time collaboration | changes the whole model layer; nothing about it is cheap |
| Graphical notation (diagrams, edges) | a second projection engine; different interaction model |
| Bidirectional text | caret math implication, no user demand yet |
| User-defined executable logic | trust boundary; would require a sandbox |
| Mobile / touch editing | projection assumes a keyboard and a caret |
| Language interop with external IDEs | out of the component's remit |

## Risk register

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Custom text layout is slower or harder than expected | blocks everything visual | dedicated spike in M1 before committing to the full control |
| Scope creep from "make it like MPS" | never reaches usable state | milestones with hard gates; non-goals list fixed |
| Incremental validation complexity | correctness bugs, phantom errors | dependency sets kept coarse first, refined only with profiling data |
| Bootstrap self-lockout | workbench cannot open its own languages | bootstrap corpus tested in CI from M2, before self-hosting exists |
| The standalone app competes with the libraries for time | slower core progress, two bug backlogs | app scope is gated per milestone, and every app feature that needs an internal fails the public-API check ([ADR 0011](adr/0011-standalone-editor.md)) |
| Solo project loses momentum | abandoned mid-way | each milestone ships something usable and demoable |
