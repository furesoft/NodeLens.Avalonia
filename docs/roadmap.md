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

**M1 is complete.** The last entry on its table - folding - landed as slices S1 to S6 plus one fix the
runtime probe forced: `FoldMarks` had assumed a node's cell carries the node's own address, which is true
only for the document root, so no arrow ever appeared. Both the defect and its repair are written down in
[collapsing.md](architecture/collapsing.md).

**The intention list is done** ([context-actions.md](architecture/context-actions.md)): slices A to G, the hover
preview ([intention-preview.md](architecture/intention-preview.md), V1-V6), three language packages and the
framework's own catalogue - rename, duplicate, delete, the moves, both insertions, sorting, the two selections,
wrap, replace and reset, all derived from the shape of the caret's place (`Projectional.Intentions`). The
workbench registers it for every document, as the host's own so the generic operations sit above a language's,
and `Expressions.Intentions` retired its duplicate "wrap in parentheses" in the same step. "Negate" stayed: a
`Unary` born with `operator = "!"` is not what a bare wrap produces, and no declaration says which operator a
fresh `Unary` should start with - declared intentions (M5) are what would close that gap.

What is next, in order:

1. **The language template is done** (`dotnet new projectional-language`, `templates/projectional-language`):
   concepts, notation, a sample model and two tests, written against `Projectional.Toolkit` alone
   ([language-authoring.md](architecture/language-authoring.md), slices C1a/C1b, [ADR 0019](adr/0019-toolkit-is-the-language-surface.md)).
   M2's gate is one step away: what it still asks for is a DSL defined entirely in *project files* - the declared
   path - and the template is the C# half of that story.

2. **The shell's M2 half**: recent projects and the self-contained single-file build.
   The problems panel is done - diagnostics of the active document in document order, choosing a row puts the
   caret on the cell the finding is about ([standalone.md](architecture/standalone.md#the-problems-panel)) - and
   it is the model for the rest: the row-building half is headless and tested, only the list and the scroll need
   a window. Find is done too: `Projectional.Editing/Find/` searches the caret places, `FindNextAction` walks the
   occurrences with wraparound, and the app shows it as a strip (`Ctrl+F`) rather than a dialog
   ([standalone.md](architecture/standalone.md#find)). **Replace is deliberately not part of it**: replacing is an
   edit and belongs with the text actions, over the selection a match already produces.
   The model browser is done as well: a left panel lists every node of the active document, depth first
   ([standalone.md](architecture/standalone.md#the-model-browser)); it walks the model rather than the projection,
   which is what separates it from Find.
   The language picker that used to sit in this list is done too - File > *Open language project...*
   loads a folder of `structure.mps` + `editor.mps` into the shell's own registry.
   The language picker that used to sit in this list is done too - File > *Open language project...*
   loads a folder of `structure.mps` + `editor.mps` into the shell's own registry.
   **Known gap, recorded rather than fixed here:** `standalone.md` and
   [ADR 0015](adr/0015-shell-is-a-library.md) describe a shell that is a library (`ShellView`, `ShellWindow`,
   `IShellStorage`, `IShellDialogs`, `ProjectDriver`, `CommandRegistry`), and none of those types exist yet - the
   app is `MainWindow` alone. ADR 0015 puts that extraction at M6, so the M2 features are being added to
   `MainWindow`; whoever does M6 moves them.
3. **The table's writing affordances** (adding a row, resizing a column). The grid itself is delivered and the
   flattened navigation is correct but not what a user expects in a grid; [projection.md](architecture/projection.md)
   and open question 29 hold the detail.
4. **The rest of the catalogue as menu rows.** `INT010`/`INT011` list what `Enter` and completion already do, so
   they cost a menu entry rather than an action, and the language-side entries from `INT040` on arrive with the
   languages that need them.

**Slice Q7 - the standard checks as a package** is done: `Projectional.Checks` ships `STD001`, `STD003`,
`STD010` and `STD040`, and a language includes them by naming them in a `CheckSet`. The safety class the
catalogue needed (`EditSafety`) is carried on every offer, though the gesture no longer reads it: it always
opens the list. What is
still open from the catalogue is the fifteen checks the design lists after the first four, which land as a
language asks for them rather than in one pass.

The constraint engine that will produce diagnostics in volume stays where it is, in M4.
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
| Avalonia control | rendering, caret, selection, wheel scrolling — **delivered**; host scrollbars via `ILogicalScrollable` ([scrolling.md](architecture/scrolling.md)) — **delivered**; virtualization is planned |
| Input | typing, arrows, tab, enter, delete, undo/redo — **delivered** |
| Node moves | Alt+Up / Alt+Down reorder the node under the caret; identity, caret and undo survive ([node-moves.md](architecture/node-moves.md)) — **delivered** |
| Clipboard | Ctrl+A/C/X/V: Ctrl+A widens the selection from the value to the node to the document, the other three copy, cut and paste text within one property cell through a host-supplied `ITextClipboard` ([clipboard.md](architecture/clipboard.md)) — **delivered** (text only) |
| Completion | Ctrl+Space offers the concepts an empty slot accepts, filtering by typed prefix, with a host-supplied icon per kind ([completion.md](architecture/completion.md)) — **delivered** (concepts only) |
| Syntax highlighting | a semantic style per cell, a host-supplied palette, no colour in the model — **delivered** |
| Text decorations | bold/italic/underline/strikethrough decided by the model, per cell — **delivered** |
| Notation variants | one concept, more than one notation (block, table), chosen at projection time and never persisted ([ADR 0012](adr/0012-concept-rendered-more-than-one-way.md)) — **delivered** |
| Folding | a node folds to a one-cell summary when its concept nominates a boolean collapse slot, with an arrow in the gutter; the fold set is view state and never persisted ([collapsing.md](architecture/collapsing.md)) — **delivered** |
| Built-in language | minimal `Projectional.Expressions` + a toy block language (`Projectional.Samples`) — **delivered** |
| Standalone shell (thin) | `Projectional.Workbench`: open project, model tabs, edit, save, dirty prompt. Public API only ([standalone.md](architecture/standalone.md)) — **delivered** |

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
| Concept declarations as nodes | structure language (stage B) - **delivered** as P1 ([declared-structure.md](architecture/declared-structure.md)) |
| Notation declarations as nodes | editor language (stage B) - **delivered** as P2 ([declared-editor.md](architecture/declared-editor.md)) |
| Generic renderer | renders any declared concept; the MPS-shaped definition languages replace it in M5 ([language-authoring.md](architecture/language-authoring.md)) |
| Parity harness | the same language declared in C# and in the project, compared by registry dump, snapshot and diagnostics - the rule of [ADR 0014](adr/0014-both-authoring-paths-are-permanent.md) made executable; **delivered** as C2 ([language-parity.md](architecture/language-parity.md)) |
| Language template | `dotnet new projectional-language`, plus the `Projectional.Toolkit` surface a package needs |
| Tables | grid cell, column sizing, cell caret, add-row affordance |
| Language loading | declared languages from the project directory - a folder of `structure.mps` + `editor.mps` loads as a usable language ([language-projects.md](architecture/language-projects.md)); the app-side picker is what is left |
| Shell grows | model browser, problems panel, find/replace, recent projects, self-contained single-file build |

**Gate:** a small DSL (including a table) is defined entirely in project files and is
editable with correct structural editing; deleting the language's C#
code changes nothing about what the user sees. The declared DSL can be opened, edited
and saved in the standalone app, which no longer needs C# to know the language. Its C#
twin is still in the repository and still agrees with it, because the parity harness says so.
*Effort: 6–8 weeks.*

### M3 — References, scopes and completion

*Goal: it starts to feel like an editor instead of a structured form.*

| Delivers | Details |
| --- | --- |
| Scopes | chain, defaults, per-language overrides, ambiguity diagnostics |
| Reference cells | resolve, create-on-the-fly, unresolved state, chooser popup |
| Completion | scope-driven entries alongside the concepts M1 already offers, insert-and-continue ([completion.md](architecture/completion.md)) |
| Substitutions | transformation menus and wrapping; keyword-triggered insertion is **delivered ahead of this milestone** as concept triggers ([triggers.md](architecture/triggers.md)) |
| Intentions (light bulb) | actions offered at the caret by the language and by the host, applied as one undo step ([context-actions.md](architecture/context-actions.md)) |
| Intention preview | a longer hover shows what the highlighted entry would do, by applying it to a discarded copy and projecting that; the live model is never touched ([intention-preview.md](architecture/intention-preview.md)) - the copy and the diff (slice V1) are **delivered** |

**Gate:** referencing an element in another model works; renaming a target does not
break models thanks to `resolveInfo`; wrapping a selection in an `if` is one undo step.
*Effort: 6–8 weeks.*

### M4 — Semantics and the pilot DSL

*Goal: the first real DSL works end to end.*

Pilot: **rule / decision tables** (business rules with conditions, outcomes, tables).

| Delivers | Details |
| --- | --- |
| Constraints | incremental validation, and diagnostics as wavy severity underlines ([markers.md](architecture/markers.md)) |
| Quick fixes | a diagnostic carries its own repairs, listed beside the marker set and reached with `Alt+Enter`/`Ctrl+.`, the marker tooltip and the context menu, applied as one undo step ([quick-fixes.md](architecture/quick-fixes.md)); implemented ahead of this milestone, with the intention list joining it in M3 |
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
| Structure language (stage C) | `Projectional.Structure` drawn in its own notation: concepts, slots, inheritance, enumerations as an editable model ([language-authoring.md](architecture/language-authoring.md)) |
| Editor language (stage C) | `Projectional.Editor` as cell glyphs, with notation and the table variant as an editable model |
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
| Declarative aspects | constraints, types, scopes, generation, intentions, textGen and accessories as definition languages, each lowering to the aspect delegate a C# language writes ([language-authoring.md](architecture/language-authoring.md#aspects-are-annotations)) |
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
