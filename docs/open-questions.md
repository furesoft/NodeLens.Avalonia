# Open questions

Decisions that are *intentionally* unresolved. Each row states the question, the
recommendation, and the milestone by which it must be decided — deciding early is
usually worse than deciding late, because most of these only become clear with a
working editor.

Status values: **Open** (undecided), **Leaning** (recommendation exists, not committed),
**Decided** (promoted into an ADR).

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 1 | Does undo store inverse changes or replay commands? | Leaning | Inverse changes with typing-burst coalescing. Command replay breaks as soon as an action depends on completion/scope state. | M0 |
| 2 | How are keyboard shortcuts assigned, and can languages add their own? | Leaning | Fixed core set, language-provided actions must bind to free keys or appear only in menus. Conflicts are a support nightmare. | M3 |
| 3 | Is the caret allowed to sit inside a collapsed/abstract node? | Leaning | No — the caret always attaches to a visible leaf. Invisible caret positions produce unexplainable keystrokes. | M1 |
| 4 | How much of MPS' *editor cell* model is adopted? | Open | Start with the cell set in [projection.md](architecture/projection.md); add primitives only when a real notation needs them. | M2 |
| 5 | Do we support several editors/views on the same model simultaneously? | Leaning | Yes, but with one writer and one undo stack per model, not per view. Cheap now, expensive later. | M3 |
| 6 | Clipboard: structural only, or also plain text? | Leaning | Both, structural preferred on paste. Text-only would make the editor unusable for non-users. | M3 |
| 7 | What exactly is a "model" for scoping and file granularity? | Open | Model = file = import unit. Revisit only if projects grow beyond a few hundred models. | M2 |
| 8 | Do declared languages get their own versioning independent of the project? | Leaning | Yes: `language.xml` carries the version; models record the version they were written with. | M2 |
| 9 | Where do styles/themes live — language or host application? | Leaning | Split: language defines *semantic* tags, host maps tags to colours/fonts. Otherwise every DSL fights the host theme. | M2 |
| 10 | How is generation triggered: manual, on save, on change? | Leaning | Manual plus on-demand; never on every keystroke. Generation on change is a performance trap. | M4 |
| 11 | Does the interpreter support stepping with a caret in the model, or only a trace? | Leaning | Trace first (M4), stepping as a debugger UI later. Stepping needs its own UI vocabulary. | M4 |
| 12 | Are generated artifacts written to disk by the framework? | Leaning | No. The framework returns text; the host decides where it goes. Keeps the component free of build-system opinions. | M4 |
| 13 | How are project-wide renames implemented — dedicated refactoring or scripted edit actions? | Leaning | Edit-action programs, so they are undoable and testable like anything else. | M6 |
| 14 | Model-aware 3-way merge: in the editor, in a CLI tool, or both? | Open | CLI first (usable in hooks), UI later. | M6 |
| 15 | Is AOT/trimming a supported scenario or best-effort? | Open | Best-effort until M7; the reflection-free design keeps the door open. | M7 |
| 16 | Localisation of the framework UI (menus, diagnostics)? | Leaning | English-only UI, but diagnostic messages are keys that a host can localise. | M5 |
| 17 | Do languages ship with example models as tests? | Leaning | Yes — a language package without a golden corpus is untestable and will regress. | M6 |
| 18 | Bidirectional text and CJK input: required for 1.0? | Open | CJK/IME yes (M7), bidi no. | M7 |

## Standalone editor

[ADR 0011](adr/0011-standalone-editor.md) decided that a standalone app ships and is an
ordinary consumer of the public API. What that app *is* in detail is not decided, and
should not be decided before the shell exists.

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 19 | MVVM toolkit, hand-written view models, or code-behind? | Leaning | View models without a toolkit: the shell is small, and a source-generator dependency is hard to justify for a dozen commands. Revisit if binding boilerplate dominates. | M1 |
| 20 | Where do settings live — per user, per project, or both? | Leaning | Both, with project settings in the project directory (portable, reviewable) and user settings per user (window layout, recent list). Project settings must never contain machine paths. | M2 |
| 21 | Does the app auto-update? | Open | No updater before 1.0. An updater is a security surface and an unsigned-binary problem; ship a version check that links to a download page instead. | M7 |
| 22 | Single-file self-contained build, framework-dependent build, or both? | Leaning | Both: single-file for evaluation, framework-dependent for users with the runtime. Installer at M7. | M2 |
| 23 | What happens when a file changes on disk while it is open? | Leaning | Prompt: reload from disk / keep my version. Silent reload destroys work; silent ignore hides conflicts. | M2 |
| 24 | Is the app allowed features the library cannot express — e.g. a search across models? | Leaning | Only through a public API that any host could call. A search that needs internals becomes a public search API, not an app-private one. | M2 |
| 25 | Does the shell logic get its own test project? | Leaning | Yes, once the shell exceeds trivial wiring — headless tests over documents, commands and command-line parsing. Add only then, not in advance. | M2 |

## Multiple representations

[ADR 0012](adr/0012-concept-rendered-more-than-one-way.md) decided that a concept may declare
several notations and that the projection picks one per concept. What that leaves open is how
far the choice, and the representations, may go.

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 26 | Is a variant chosen per concept or per node? | Leaning | Per concept in M1 — implemented: `NotationChoice` keys variants by concept key and one choice per session. Per node needs a choice keyed by node id and a reason a single node should differ — take it up when a real DSL asks. | M4 |
| 27 | Is the choice persisted per document, and where? | Leaning | Per document in project settings, keyed by model URI and concept key. Never in the model file, never in a path that breaks portability. | M3 |
| 28 | How do custom representations that a declarative notation cannot express (charts, images, maps) plug in? | Open | A separate cell-provider contract that must still report addresses and caret places. The declarative table cell is the reference implementation of "a cell that is a grid but not a grid of lines", so the contract should be derived from it rather than designed before it. | M5 |
| 29 | Do tables get column-aware navigation (down a column, next row), a header row that stays visible when scrolling, and resizable columns? | Leaning | Yes to navigation and a sticky header; resizing only if a language asks. M1 navigates the flattened grid in document order, which is correct but not what a user expects in a grid. | M3 |

## Scope honesty

Two things should stay written down, because they are easy to talk oneself out of:

1. **"Like MPS" is a direction, not a deliverable.** MPS is a multi-year, multi-person
   product. Targeting its feature list with a part-time solo effort is the reliable
   route to an unfinished framework. The gates in [roadmap.md](roadmap.md) exist to
   keep the project at "usable subset" instead of "unusable whole".
2. **The pilot DSL is a forcing function.** Rule/decision tables were chosen because
   they stress the projection engine (rows, columns, alignment) and the semantics
   (constraints, generation, interpretation) at the same time. If the pilot works, the
   architecture is probably right; if it needs special cases, the architecture is
   wrong and should be fixed before more languages exist.
3. **The standalone app is a second product.** It exists to keep the public API honest
   and to make the framework tryable, not to grow a feature list of its own. The moment
   it has a capability the library cannot express, the framework has two front ends and
   the embedding path becomes the untested one.

## Questions that were already decided

Listed here so they are not reopened accidentally; the authoritative text is the ADR.

| Topic | Decision | ADR |
| --- | --- | --- |
| UI stack | Avalonia | [0001](adr/0001-ui-stack-avalonia.md) |
| Source of truth | model, never text | [0002](adr/0002-model-as-source-of-truth.md) |
| Runtime and license | .NET 10 LTS, Apache-2.0 | [0003](adr/0003-runtime-and-license.md) |
| Reference identity | stable IDs with `resolveInfo` fallback | [0004](adr/0004-stable-node-ids.md) |
| Persistence | XML, MPS-like | [0005](adr/0005-xml-persistence.md) |
| Change propagation | change events as backbone | [0006](adr/0006-change-event-backbone.md) |
| Language definition | reflection-free, delegate-based aspects | [0007](adr/0007-reflection-free-language-definition.md) |
| Bootstrap order | built-in languages before self-hosting | [0008](adr/0008-bootstrap-order.md) |
| Editor control | custom-drawn, structural caret | [0009](adr/0009-custom-drawn-editor-control.md) |
| Public API | `Core` + `Avalonia` only | [0010](adr/0010-public-api-surface.md) |
| Standalone editor | ships, and is a plain consumer of the public API | [0011](adr/0011-standalone-editor.md) |
| Execution | interpreter *and* text generation | ADR to be written (M4) |
| Pilot DSL | rule/decision tables | ADR to be written (M4) |
