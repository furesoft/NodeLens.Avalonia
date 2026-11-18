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

M3 is complete, M4 about half, M5 started, M6 started with one declared aspect, and M7 and M8 are untouched.

What is next, in order:

1. **The rest of the catalogue as menu rows.** `INT010`/`INT011` list what `Enter` and completion already do, so
   they cost a menu entry rather than an action, and the language-side entries from `INT040` on arrive with the
   languages that need them.
2. **The fourteen remaining standard checks.** `Projectional.Checks` ships five (`STD001`, `STD003`, `STD010`,
   `STD011`, `STD040`); the rest of the catalogue lands as a language asks for it rather than in one pass.
3. **The M4 gate's second front end**: a host application that embeds the control, opens a document, shows its
   findings, runs it and generates text - nothing in this repository does that, so the claim that the two front
   ends share the behaviour is untested. A probe proved the semantics are reachable (a project referencing only
   `Projectional.Avalonia` and `Projectional.Samples` ran a document and generated text), so the gap is not
   CLR visibility; it is that the wiring a shell needs - `DocumentRun`, `DocumentGenerate`, `RunReport`,
   `RunRow`, `RunInputs` - sits in the app, so every host would reinvent it. Moving it beside the control and
   proving it with a sample is the slice.
4. **The three worked examples.** One sample language per axis the framework bets on - references and two
   notations (state machine), constraints and a generator (feature model), cross-model references and the
   table cell in earnest (SQL schema) - each shipped as a package with a `…Checks` beside it, a `Corpus/` and
   snapshot tests, so the demonstrators rot with a failing test rather than with the documentation
   ([worked-examples.md](architecture/worked-examples.md)).

**Gaps against MPS, recorded rather than scheduled:** reading MPS' own tutorials and its "Commanding the
editor" reference turned up aspects and gestures this framework does not have - accessory models, concept
aliases, data flow, comments, go to definition, find usages, an inspector, block-selection shrink and a
handful of completion fine points. The milestone tables below name the ones that have a home;
the survey, the deliberate differences and the open decisions are in
[mps-gap-analysis.md](architecture/mps-gap-analysis.md).

**Known gap, recorded rather than fixed here:** `standalone.md` and
[ADR 0015](adr/0015-shell-is-a-library.md) describe a shell that is a library (`ShellView`, `ShellWindow`,
`IShellStorage`, `IShellDialogs`, `ProjectDriver`, `CommandRegistry`), and none of those types exist yet - the
app is `MainWindow` alone. ADR 0015 puts that extraction at M6, so the M2 features were added to
`MainWindow`; whoever does M6 moves them.

The dependency-set incrementality that lets the constraint engine produce diagnostics in volume stays
where it is, in M4.

## Milestones

The tables below are the **scope** of each milestone, not a status report: what is finished is in
[implemented.md](implemented.md), and a finished milestone is removed from this file.

### M4 — Semantics and the pilot DSL

*Goal: the first real DSL works end to end.*

Pilot: **rule / decision tables** (business rules with conditions, outcomes, tables).

| Delivers | Details |
| --- | --- |
| Constraints | the engine, the aspect and the first consumer are in [implemented.md](implemented.md); what is left is the dependency-set incrementality ([semantics.md](architecture/semantics.md#incrementality)) |
| Text generation | the template tree, the engine with its traceability map, two generators for the sample language and the declared generator language (a `generator` folder, one generator per document) are in [implemented.md](implemented.md); what is left is the generator for a second consumer (the C# base language, M6), whose target needs the model-producing stage ([model-generation.md](architecture/model-generation.md)) |
| Interpreter | the `Evaluate` aspect, the value model, the runtime with trace, step budget and cancellation, the pilot's decision-list rules, and the shell's Run command with its output panel are in [implemented.md](implemented.md); what is left is stepping (open question 11) |
| Worked examples | three sample language packages, one per axis, each with a `…Checks` package, a `Corpus/` that round-trips byte-identically and snapshot tests that pin the notation: a state machine (references, two notations), a feature model (constraints over references, a generator, an interpreter) and a SQL schema (cross-model references, the table cell in earnest) ([worked-examples.md](architecture/worked-examples.md)) |

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
| Language-definition parity | accessory models (the library nodes a language ships, such as a colour palette), a concept's alias (the word completion offers and typing creates), and an explicit "may be a root" flag ([mps-gap-analysis.md](architecture/mps-gap-analysis.md)) |

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
| Declarative aspects | **types, generation and constraints are done** (`Projectional.TypeRules`, `Projectional.TextGen`, `Projectional.ConstraintRules`, [semantics.md](architecture/semantics.md#the-declared-form)); what is left is scopes and intentions as definition languages, each lowering to the aspect delegate a C# language writes ([language-authoring.md](architecture/language-authoring.md#aspects-are-annotations)) |
| Refactorings | rename, extract, inline, move — as edit-action programs |
| Find usages | the reference index a rename needs first: who points at this node, across models, and the gesture that asks ([mps-gap-analysis.md](architecture/mps-gap-analysis.md)) |
| Concept behaviours | a node's name (and a one-sentence description) as a **declared pure function** per concept, so a composite name, a name that is a value and a name that is a sentence stop being the three things a `nameSlot` cannot express - one answer for the nine places that ask, and the declared twin of a C# lambda ([behaviours.md](architecture/behaviours.md)) |
| SModel queries | a language over the tree with MPS' own operation words - `parent`, `children<link>`, `ancestors<concept = X, +>`, `descendants<...>`, `isInstanceOf`, casts, `concept/Name/`, `node-ptr/.../` - as a **pure-read** language: its modification half stays with the edit actions, so a query cannot mutate and the framework keeps one mutation story ([smodel.md](architecture/smodel.md)) |
| Tests of a language, as data | a `tests/` folder beside the other aspects, one declarative test per document - the drawing, the findings, the generated text, a run's answer, a round trip and an edit script - with a runner in the behaviour band and one xunit method per language project as the bridge to CI ([language-tests.md](architecture/language-tests.md)) |
| Migrations tooling | migration language, golden-file corpus |
| Model-aware diff/merge | 3-way merge keyed on node IDs |
| Model-to-model generation | a generator whose output is a model of a target language, which that language's own text generator then prints: the rule vocabulary, the build and link phases, the reverse map that keeps "go to source" working through both stages ([model-generation.md](architecture/model-generation.md)) |
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

#### Model-to-model generation

Text generation treats the target as text. That is enough while the target *is* text, and it is
what the JSON and C# fragment generators of the pilot do. It stops being enough when the target
is a language of its own: emitting the projected C# base language's syntax out of templates would
keep its grammar in string fragments and leave its checks with nothing to run on.

The second stage therefore reduces a document into a model of the target language and lets that
language's own generator print it. The vocabulary is a closed set of parts - create a node, set a
property, reduce a child, reduce a list, copy a subtree the target shares, retarget a reference,
choose a branch, lower to nothing - so a transformation is data like a template and not a second
programming language. The build and the link phase are separate because a reference cannot be
written before its target exists, and the reverse map is what lets a generated line still point
at the DSL node that wrote it. Rules, failure discipline, the declared `mapping.mps` and the
slices: [model-generation.md](architecture/model-generation.md).

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
| Editor experience | comments, go to definition, block-selection shrink, inspector and hierarchy view, completion filter reset and camel humps, show type, paste history, and the keys the intentions already have ([mps-gap-analysis.md](architecture/mps-gap-analysis.md)) |
| Packaging | NuGet, CI matrix, public API baseline, AOT/trimming validation |
| App distribution | self-contained installers per OS, file association for `.prj`, winget/Homebrew manifests |

**Gate:** a developer unfamiliar with the project builds a small DSL by following the
tutorial, without asking questions — and a user who does not write C# can install the
standalone editor and work with an existing DSL.

### M8 — Language descriptions for LLMs (after 1.0)

*Last on purpose: a description of a language is worth shipping only once the language surface is
finished, text import exists and the public API is frozen - otherwise it describes something that is
still moving.*

| Delivers | Details |
| --- | --- |
| The description | one walk over a language's declarations produces a machine-readable schema and a prose guide: concepts, slots and cardinalities, types, enumerations and defaults, applicability, notation, triggers, typing rules and declared constraints ([llm-language-description.md](architecture/llm-language-description.md)) |
| Model generation | a model writes a document in the existing interchange format, and the framework loads it through the path a user's file takes - reporting what it refused rather than guessing or repairing |
| AST operations | a serializable operation list over document **paths**, applied through the edit actions that already exist, in one transaction, so a proposal is one undo step and previewable by the intention sandbox |
| Repair loop | the findings (format, concept, constraint, check) are the answer that goes back to the model; the framework owns no loop and no provider |

**Gate:** two documents generated from the guide alone - one for the payroll sample, one for the
declared pipeline - load, pass their language's checks and round-trip byte-identically; an operation
list applies as one undo step and is refused with a reason when it names a slot that does not exist;
the description of the workbench's own languages is stable enough to be diffed in git.
*Effort: 3-4 weeks.*

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
