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

1. **The rest of the catalogue as menu rows.** `INT010`/`INT011` are delivered in the context menu; what is
   left are the language-side entries from `INT040` on, which arrive with the languages that need them.
2. **The rest of the standard-check catalogue is blocked, not pending.** `Projectional.Checks` ships eleven
   (`STD001`, `STD002`, `STD003`, `STD004`, `STD010`, `STD011`, `STD012`, `STD020`, `STD021`, `STD040`, `STD041`).
   The entries that are left cannot be written from a document this model can hold, and
   [quick-fixes.md](architecture/quick-fixes.md#how-a-language-includes-them) records why: `STD030`–`STD032` and
   `STD042` describe a value the parser and the reader refuse; `STD050`/`STD051` wait for the opaque node
   `persistence.md` promises but the reader does not build; and `STD022` is a policy a language writes itself,
   because a cross-document reference is a feature here.
3. **The three worked examples.** One sample language per axis the framework bets on - references and two
   notations (state machine), constraints and a generator (feature model), cross-model references and the
   table cell in earnest (SQL schema) - each one project with its checks in `Checks/`, a `Corpus/` and
   snapshot tests, so the demonstrators rot with a failing test rather than with the documentation
   ([worked-examples.md](architecture/worked-examples.md)). **All three are delivered.** Between them they
   found and closed four gaps - a table column naming a reference, a reference crossing a model boundary, a
   template part for a reference, and the four a sample may not work around - and left two records closed since: a
   template cannot read the name of a node's parent (closed by `ParentPart`) and "a many-valued reference cannot be
   drawn", which was the model's rule rather than a gap - a reference is single-valued, and a list of links is a
   child node per element ([language.md](architecture/language.md#notation)).

**Gaps against MPS, recorded rather than scheduled:** reading MPS' own tutorials and its "Commanding the
editor" reference turned up aspects and gestures this framework does not have - accessory models, an instance
created from a typed alias, data flow, comments, an inspector, block-selection shrink and a
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
| Constraints | the engine, the aspect and the first consumer are in [implemented.md](implemented.md); what is left is the dependency-set incrementality ([constraints.md](architecture/semantics/constraints.md#incrementality)) - and the measurement says incremental *layout* is the cost worth attacking first (open question 111), because a full recheck is now cheaper than the rebuild it runs beside |
| Text generation | the template tree, the engine with its traceability map, two generators for the sample language and the declared generator language (a `generator` folder, one generator per document) are in [implemented.md](implemented.md); what is left is the generator for a second consumer (the C# base language, M6), whose target needs the model-producing stage ([model-generation.md](architecture/model-generation.md)) |
| Interpreter | the `Evaluate` aspect, the value model, the runtime with trace, step budget and cancellation, the pilot's decision-list rules, and the shell's Run command with its output panel are in [implemented.md](implemented.md); what is left is stepping (open question 11) |
| Worked examples | three sample language projects, one per axis, each with its check in `Checks/`, a `Corpus/` that round-trips byte-identically and snapshot tests that pin the notation: a state machine (references, two notations), a feature model (constraints over references, a generator, an interpreter) and a SQL schema (cross-model references, the table cell in earnest) ([worked-examples.md](architecture/worked-examples.md)) |

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
| Language-definition parity | accessory models (the library nodes a language ships, such as a colour palette) and an instance created from a typed alias are what is left; the concept's alias as the word completion offers, and an explicit "may be a root" flag, are delivered ([mps-gap-analysis.md](architecture/mps-gap-analysis.md)) |

By the end of this milestone the definition languages are written in themselves: the same
project folder, the same `<<property … >>` blocks, the same cell glyphs, loaded from the bootstrap
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
| Declarative aspects | **types, generation, constraints, scopes and intentions are done** (`Projectional.TypeRules`, `Projectional.TextGen`, `Projectional.ConstraintRules`, `Projectional.ScopeRules`, `Projectional.IntentionRules`, [types.md](architecture/semantics/types.md#the-declared-form)); a declared intention has the two bodies a language ships so far - wrap with presets, set a property. What is left is the one aspect a declared language still cannot state: **import as a declaration**, so a language written as a model can read its own files and not only print them ([import.md](architecture/import.md#import-as-a-declaration-planned)) |
| Types against MPS' cookbook | the type system answers one question per node today, which is not enough for the cookbook's central case - tying one node's type to another's. Planned as eight slices, of which **T1, T2, T3, T4, T5, T7 and T8's replacement are delivered** (T1: the subtype relation as data, strict where `Accepts` is lenient; T2: `TypeVariable`, `TypeEquation` and a budgeted fixpoint `TypeSolver`; T3: `JoinType`/`MeetType` and `TypeLattice`, reduced from the relation, `unknown` when the edges do not determine one; T4: the declared forms - `TypeRule.Equations`/`Equation`/`TypeVariable`, a variable shared by name within one rule, `TypeRegistry.EquationsFor` as the caller the machinery lacked, the declared `join`/`meet` type expressions, and `TypeRule.Supertypes`, one edge per document lowered into the relation the registry carries so a language's own edges reach the solver and the lattice; T5: `IType.IsComparableTo`, "are these two interchangeable?", asked through the relation both ways, plus the declared `TypeRule.Comparable` - a pair lowered into a `ComparisonRelation` kept apart from the edges so a language can state an interchange without ordering the two types, which is what MPS' weak comparison is for; T7: `TypeRule.Complain` - a body whose message and severity lower into the language's own constraints, so the existing findings path carries them, gated by the optional `when` condition `TypeRule.NotComparable`, which is how "operation is not applicable to these operands" is stated; T8: `TypeRule.Replacement` - a claim between two type patterns, rewritten into the relations the rule states instead, with the framework's own list/optional covariance stated the same way so there is one mechanism instead of two). What is left is `TypeRule.Substitute`, which needs a per-model configuration slot the framework does not have; the *settle* pass is answered rather than built, because a type read descends a tree and cannot cycle, and the solver's fixpoint is the ordering. No statement list: MPS' do { … } blocks become equations ([types.md](architecture/semantics/types.md#planned-parity-with-mps-typesystem), open questions 112/113) |
| Refactorings | rename and the two moves are delivered (`RenameNodeAction`, `MoveNodeAction` with **Alt+Up/Down** and the four "move to a place" entries); **extract and inline are planned**, and they are not the same kind of feature: a reference here is a slot on the owner rather than a node standing for a use, so "inline" has nothing to replace until a language declares what its use *is* - and which node is a definition, and where an extracted one belongs, are language facts the framework must not invent. Planned as two more **intention** bodies, which is the machinery that already has declarations, per-caret availability, safety and preview ([refactorings.md](architecture/refactorings.md)) |
| Concept behaviours | the seam and the body vocabulary are delivered (`ConceptDeclaration(nameOf:)`, `Concept.NameOf(node)`, `Projectional.Language/Names/`); **deferred by decision** are the declared `behaviour` folder, `Describe`, the samples with the parity test, and the override finding ([behaviours.md](architecture/behaviours.md)) |
| SModel queries | a language over the tree with MPS' own operation words - `parent`, `children<link>`, `ancestors<concept = X, +>`, `descendants<...>`, `isInstanceOf`, casts, `concept/Name/`, `node-ptr/.../` - as a **pure-read** language: its modification half stays with the edit actions, so a query cannot mutate and the framework keeps one mutation story ([smodel.md](architecture/smodel.md)) |
| Migrations tooling | migration language, golden-file corpus |
| Model-aware diff/merge | 3-way merge keyed on node IDs |
| Model-to-model generation | a generator whose output is a model of a target language, which that language's own text generator then prints: the rule vocabulary, the build and link phases, the reverse map that keeps "go to source" working through both stages ([model-generation.md](architecture/model-generation.md)) |
| Text import | **JSON, XML, TOML, HOCON and YAML are delivered** as the five formats (`Projectional.Json`, `Projectional.Xml`, `Projectional.Toml`, `Projectional.Hocon`, `Projectional.Yaml`: the concept models, their notations, importers and generators, [import.md](architecture/import.md)), and **File ▸ Import…** opens a file of any of them as a document of that language - with its own intentions, checks and generators. **The declared importer is delivered too** (`Projectional.ImportRules`: a language written as a model reading its own files, with the inverse of its generator's templates as the default), and the same command runs it: the languages opened in the window are asked before the built-in formats, and the first importer that reads the file opens a document with that language's aspects. What is left of import is breadth rather than mechanism - a declared importer written as a twin for a reference format, which the parity rule would then have two subjects for; converting a model into one of them stays model-to-model generation into that language's model and the format's spelling lives in exactly one place |
| Projected C# base language | expression and statement subset, behaviour methods, generator ([csharp.md](architecture/csharp.md)) |
| Embedded shell | the shell as a control a host composes: locked to one language, its storage and dialogs supplied by the host, its capabilities declared once and enforced by the command registry ([embedded-shell.md](architecture/embedded-shell.md)) |

**Gate:** a model can be renamed project-wide without breaking references, and two
branches editing different parts of one model merge without hand-editing XML.
*Effort: several months, split into independently shippable slices.*

#### Text import

Import is the only path by which text becomes a model, and it runs once: the host passes a
string, the framework builds nodes inside one transaction, the string is discarded. It is
therefore not the text buffer [ADR 0002](adr/0002-model-as-source-of-truth.md) forbids - nothing
writes text back. Five reference languages ship with it - JSON and XML, which everyone already
has, TOML and HOCON, which is what a .NET project's settings and a config-heavy host actually
use, and YAML, whose layout is part of its syntax - because a language that imports and
generates a format everyone already has is the cheapest possible proof that the mapping layer is
sound. Each of the five is a **language**, not a printing
mode: that is what makes converting a user's model into one of them a matter of producing a model
of *that* language, whose own generator spells quoting, escaping, table nesting and comment syntax.
Details, including why a construct the language does not model is kept as raw text rather than
dropped: [import.md](architecture/import.md).

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
| Editor experience | comments, block-selection shrink, inspector and hierarchy view, completion filter reset and camel humps, show type, paste history, and the keys the intentions already have ([mps-gap-analysis.md](architecture/mps-gap-analysis.md)) |
| Packaging | **NuGet, the CI matrix and the tag-driven publish are delivered** ([packaging.md](architecture/packaging.md), [ADR 0022](adr/0022-every-project-is-a-package.md)); what is left is a public API baseline (so a breaking change fails a build) and AOT/trimming validation |
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
