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

## Next

**What is finished is recorded in [implemented.md](implemented.md)** - milestone by milestone, with the
test that proves each row. This section keeps only what is left, and a feature moves out of it into
that file in the same commit that finishes it.

M3 and M4 have landed their first halves early, M5 has started, M6 and M7 are untouched.

What is next, in order:

1. **Ambiguity diagnostics** - the last piece of scopes: a name that matches two things is refused at the
   keystroke today, but nothing reports it in the problems panel, so a document that already holds an
   ambiguous reference says nothing about it.
2. **The rest of the catalogue as menu rows.** `INT010`/`INT011` list what `Enter` and completion already do, so
   they cost a menu entry rather than an action, and the language-side entries from `INT040` on arrive with the
   languages that need them.
3. **The fifteen remaining standard checks.** `Projectional.Checks` ships the first four (`STD001`, `STD003`,
   `STD010`, `STD040`); the rest of the catalogue lands as a language asks for it rather than in one pass.
4. **M4's semantics**: the constraint engine, types, text generation, the interpreter and the cross-check
   tests that make generated code and the interpreter agree on the corpus.

**Known gap, recorded rather than fixed here:** `standalone.md` and
[ADR 0015](adr/0015-shell-is-a-library.md) describe a shell that is a library (`ShellView`, `ShellWindow`,
`IShellStorage`, `IShellDialogs`, `ProjectDriver`, `CommandRegistry`), and none of those types exist yet - the
app is `MainWindow` alone. ADR 0015 puts that extraction at M6, so the M2 features were added to
`MainWindow`; whoever does M6 moves them.

The constraint engine that will produce diagnostics in volume stays where it is, in M4.

## Milestones

The tables below are the **scope** of each milestone, not a status report: what is finished is in
[implemented.md](implemented.md), and a finished milestone is removed from this file.

### M3 — References, scopes and completion

*Goal: it starts to feel like an editor instead of a structured form.*

| Delivers | Details |
| --- | --- |
| Scopes | the chain, the default provider, per-language overrides and ambiguity as a value are in [implemented.md](implemented.md); what is left is ambiguity *diagnostics* in the problems panel |
| Completion | scope-driven entries are in [implemented.md](implemented.md); what is left is insert-and-continue and completing a filled slot ([completion.md](architecture/completion.md)) |
| Substitutions | transformation menus; keyword-triggered insertion is concept triggers ([triggers.md](architecture/triggers.md)) |

**Gate:** referencing an element in another model works; renaming a target does not
break models thanks to `resolveInfo`; wrapping a selection in an `if` is one undo step.
*Effort: 6–8 weeks.*

### M4 — Semantics and the pilot DSL

*Goal: the first real DSL works end to end.*

Pilot: **rule / decision tables** (business rules with conditions, outcomes, tables).

| Delivers | Details |
| --- | --- |
| Constraints | the engine, the aspect and the first consumer are in [implemented.md](implemented.md); what is left is the dependency-set incrementality ([semantics.md](architecture/semantics.md#incrementality)) |
| Types | the primitive and `Unknown` kinds, the aspect and the first consumer are in [implemented.md](implemented.md); what is left is the named and structural kinds ([semantics.md](architecture/semantics.md#types)) |
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
| Structure language (stage C) | `Projectional.Structures` drawn in its own notation: concepts, slots, inheritance, enumerations as an editable model ([language-authoring.md](architecture/language-authoring.md)) |
| Editor language (stage C) | `Projectional.Editors` as cell glyphs, with notation and the table variant as an editable model |
| Language project | one model per aspect beside a `language` root, with dependencies and extended languages; a new kind of aspect is a new language plus an annotation concept, not a framework change |
| Language export | declared to C#, so a language prototyped in the workbench ships as a package without being retyped, behavioural aspects emitted as explicit stubs |
| Live preview | edit a language definition, see the effect on a model next to it |
| Bootstrap | versioned bootstrap XML, migrated with user data; the C# declarations stay as the oracle and the fail-safe |
| Workbench shell | the standalone app grows into the workbench: language browser, live preview, output panel, language project creation |

By the end of this milestone the definition languages are written in themselves: the same
project folder, the same `<<property>>` blocks, the same cell glyphs, loaded from the bootstrap
corpus and checked by the parity harness against their C# twins.

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
| Declarative aspects | **types are done** (`Projectional.TypeRules`, [semantics.md](architecture/semantics.md#the-declared-form)); what is left is constraints, scopes, generation, intentions and textGen as definition languages, each lowering to the aspect delegate a C# language writes ([language-authoring.md](architecture/language-authoring.md#aspects-are-annotations)) |
| Refactorings | rename, extract, inline, move — as edit-action programs |
| Migrations tooling | migration language, golden-file corpus |
| Model-aware diff/merge | 3-way merge keyed on node IDs |
| Model-to-model generation | optional second generator stage |
| Text import | JSON and XML reference languages behind an `ITextImporter` ([import.md](architecture/import.md)) |
| Projected C# base language | expression and statement subset, behaviour methods, generator ([csharp.md](architecture/csharp.md)) |
| Embedded shell | the shell as a control a host composes: locked to one language, its storage and dialogs supplied by the host, its capabilities declared once and enforced by the command registry ([embedded-shell.md](architecture/embedded-shell.md)) |
| Plugin packaging | one NuGet per language, versioned |

**Gate:** a model can be renamed project-wide without breaking references, and two
branches editing different parts of one model merge without hand-editing XML.
*Effort: several months, split into independently shippable slices.*

#### Text import

Import is the only path by which text becomes a model, and it runs once: the host passes a
string, the framework builds nodes inside one transaction, the string is discarded. It is
therefore not the text buffer [ADR 0002](adr/0002-model-as-source-of-truth.md) forbids - nothing
writes text back. Two reference languages ship with it, JSON and XML, because a language that
imports and generates a format everyone already has is the cheapest possible proof that the
mapping layer is sound. Details, including why a construct the language does not model is kept
as raw text rather than dropped: [import.md](architecture/import.md).

#### Projected C#

MPS's Java base language, for .NET: the logic inside a behaviour or a generator is written in
the editor as model data, so it is undoable, completable, validated and diffable, and the
generator emits C# that the **host's** build compiles. The framework never compiles or executes
it. The subset, the precedence rule, the composition with the delivered expression language and
the gate (compiled output and the interpreter must agree on the corpus):
[csharp.md](architecture/csharp.md).

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
| User-defined executable logic | trust boundary; would require a sandbox. **Generating** source the host compiles is in scope ([csharp.md](architecture/csharp.md)); the framework compiling or loading an assembly is not |
| Mobile / touch editing | projection assumes a keyboard and a caret |
| Language interop with external IDEs | out of the component's remit |

## Risk register

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Custom text layout is slower or harder than expected | blocks everything visual | dedicated spike in M1 before committing to the full control |
| Scope creep from "make it like MPS" | never reaches usable state | milestones with hard gates; non-goals list fixed |
| Incremental validation complexity | correctness bugs, phantom errors | dependency sets kept coarse first, refined only with profiling data |
| Bootstrap self-lockout | workbench cannot open its own languages | bootstrap corpus tested in CI from M2, before self-hosting exists |
| Import becomes a second source of truth | the editor silently rewrites a file the user thinks it is showing | import is one-shot and the text is discarded ([import.md](architecture/import.md)); the model is what is saved, and no watcher or sync path exists |
| The standalone app competes with the libraries for time | slower core progress, two bug backlogs | app scope is gated per milestone, and every app feature that needs an internal fails the public-API check ([ADR 0011](adr/0011-standalone-editor.md)) |
| Solo project loses momentum | abandoned mid-way | each milestone ships something usable and demoable |
