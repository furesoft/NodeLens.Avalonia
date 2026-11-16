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
| 3 | Is the caret allowed to sit inside a collapsed/abstract node? | Decided by design | No — the caret always attaches to a visible leaf. Invisible caret positions produce unexplainable keystrokes. A folded node is drawn as a *visible leaf* carrying the node's own address, so the caret lands on it instead of vanishing; see [collapsing.md](architecture/collapsing.md). | M1 |
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

## Context actions

The light bulb is planned in [context-actions.md](architecture/context-actions.md). These are
the parts of that plan that are choices rather than mechanics.

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 30 | Where does the intention contract live? | Open | Defined in `Projectional.Editing` next to the action model it reuses, and promoted into the public surface of `Projectional.Avalonia` in the same commit as the first language that ships an intention. ADR 0010 says only `Core` and `Avalonia` carry a promise, so a type a host must implement has to end up there — but promoting a contract before a second consumer exists is how a public API gets the wrong shape. | M3 |
| 31 | Does an intention come from the concept, from a host registration, or both? | Leaning | Both, and the aspect comes first: a language's intentions travel with the language, and a host registers what only it can know (generate this, open that, run the interpreter). One list, ordered host, language, quick fixes, and the fixes are the part that exists first ([quick-fixes.md](architecture/quick-fixes.md)). | M3 |
| 32 | Is the bulb shown only at the caret, or also on lines that are merely marked? | Leaning | Once a list of fixes exists, once per line that carries at least one fix, and the caret's line wins when the caret stands on one — a marked line with no offer is the case users complain about, but a bulb on every line of a document full of warnings is noise. A line that has a diagnostic but no fix shows nothing, because there is nothing to offer. | M1, with Q6 |
| 33 | Are intentions also in the context menu, or only on the bulb? | Leaning | Both, from one list: a submenu at the top of the menu, the same entries the bulb shows. The menu answers "what can I do here" with the mouse, and the bulb answers it at the keyboard; two independent lists would drift. | M3 |
| 34 | Does an intention ever preview its effect before applying? | Leaning | No. A one-sentence description and a single undo step instead — in a projectional editor the effect is visible text, so a preview dialog shows what the editor is about to show anyway. Reopen this at M6, where a refactoring that touches several files is not visible at a glance. | M3 |

## Markers

Squiggles are planned in [markers.md](architecture/markers.md); these are the choices in it
that are not settled by mechanics.

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 35 | Is a marker part of the model, or transient state supplied by the host? | Leaning | Transient. A squiggle is a computed diagnostic, so it is recomputed rather than persisted: undo cannot resurrect a stale error, and a model file stays free of editor state. Persistent annotations ("this is a TODO") are a different feature with a different lifetime. | M4 |
| 36 | Does severity change the shape of the underline, or only its colour? | Leaning | Both, minimally: every severity is a wave, and info and hint are dotted instead of solid. Colour alone carries nothing for a colour-blind user or a greyscale print. | M4 |
| 37 | Is a marker attached to a node, or to a range inside a cell? | Leaning | Both: a node id always, an optional range inside one property's text. A constraint usually knows the node, not the offset, and geometry should still be able to underline three characters of a long cell. | M4 |
| 38 | Do markers get a list of their own ("problems"), or only appear where they are? | Open | Where they are, first: hovering explains, and the shell shows the caret line's message in a status bar. A problems list is a document-wide report with navigation and filtering, which is a feature of its own and should come after squiggles prove they are enough. | M5 |

## Multi-line text

A value of text is laid out as lines and breaks itself when the page does not reach
([projection.md](architecture/projection.md)). What that leaves open is how far the
breaking, and the editing of a broken value, may go.

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 39 | Is a break the user typed stored as `\n` in the value, or as a list of lines in the model? | Leaning | `\n` in the value. A text property stays one string, so a language never has to decide whether its value is one or many, and the file format does not grow a shape per property. | M3 |
| 40 | Does the caret stop at a soft wrap the way it stops at a hard break? | Leaning | Yes, and that is what is implemented: a position names a line, and a line is a line whichever broke it. The model is the only place that knows which is which. | M3 |
| 41 | Is the wrap width a per-host view setting, a per-document setting, or a per-cell notation setting? | Leaning | Per host in M1, published as `EditorSession.WrapWidth`. Per document once settings exist, per cell only if a notation asks for a fixed narrow cell. | M3 |
| 42 | Should a long value be soft-wrapped at all, or scrolled horizontally like a code editor? | Open | Soft-wrap for text, horizontal scroll for a notation that is a grid. Wrapping a table row would destroy the alignment that makes the table readable. | M5 |

## Base language

A base language is the logic a DSL author writes *inside* the editor instead of next to it -
MPS has Java for this, and [csharp.md](architecture/csharp.md) plans the .NET equivalent. These
questions are about how far that goes, not whether to do it.

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 43 | Does a C# expression concept extend the matching `Expressions` concept, or is it a sibling under a new shared parent? | Leaning | Extends it. A slot already declared over `Expressions.Expression` then accepts C# expressions with no language change and no migration of existing models, which is the whole reason the small language shipped first. | M6 |
| 44 | Is a behaviour class generated per concept, or per concept and DSL? | Leaning | Per concept. A concept key is unique within a project, so the generated class name is unique and the host's hand-written partial never has to disambiguate. | M6 |
| 45 | Should the editor ever know C# specifically? | Decided by design | No. Everything C#-specific lives in the language package and its generator, so a host that ships its own base language replaces the package without touching the core. | - |
| 46 | Does the framework ever compile the projected logic? | Open | No. It generates source and the host's build compiles it, which is also what keeps the roadmap's "no user-defined executable logic" exclusion meaningful - see [csharp.md](architecture/csharp.md). Running a DSL inside the editor is the M4 interpreter, not this. | M6 |
| 47 | How much of C# is enough? | Open | Expressions plus statements plus methods and classes, with query syntax, `async`, attributes and generic declaration left out. The list is in [csharp.md](architecture/csharp.md) and should be revised by what the pilot DSL actually needs, not by grammar order. | M6 |

## Text import

Import is text becoming a model, once, from a string the host supplies
([import.md](architecture/import.md)). It is the only place a parser exists, which is why the
questions are about scope rather than about whether parsing is acceptable.

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 48 | Can import replace an existing document, or only append? | Leaning | Append only. Replacing is "close the document and open a new one", which is a shell decision, and an import that silently swapped a model would be indistinguishable from data loss. | M6 |
| 49 | What happens to a construct the language does not model? | Leaning | It is kept as raw text and reported as a warning, because invariant 8 (never drop data on load) applies to import too - an editor that quietly discards a CDATA section is an editor that rewrites the user's file. | M6 |
| 50 | Should a JSON number be a `double`, an `int`, or its source text? | Leaning | Its source text. `1`, `1.0` and `1e3` are three different files to a consumer, and a numeric property would rewrite all three on the next generation. | M6 |
| 51 | Is an XML document with a DTD or external entity importable? | Decided by design | No, refused with a diagnostic. An editor that dereferences a `SYSTEM` identifier on import is an XXE vulnerability in every host that embeds it, so this is not a limitation to lift later. | - |
| 52 | Do import errors appear in the problems panel? | Open | Not as model diagnostics: a syntax error has no node, which is exactly the problem. The result carries its own line/column records and the host decides where to show them. | M6 |

## Folding

Folding is planned in [collapsing.md](architecture/collapsing.md): a node whose concept
nominates a boolean collapse slot can be folded to a one-cell summary, and the fold set is view
state on the session, so it is never persisted and never undoable. What is deliberately left open
is how far the summary and the gesture go beyond "fold this node, show an arrow in the gutter".

| # | Question | State | Answer | Decide by |
|---|----------|-------|--------|-----------|
| 53 | Does the collapsed summary carry a count of what is hidden ("3 steps")? | Open | Not in the first slice. A count needs the subtree built before layout, and the builder only builds what it is about to draw, so the count would mean building a subtree whose cells are then discarded. An ellipsis is enough for a first editor. | M2 |
| 54 | Can a user peek inside a fold without unfolding it? | Leaning | A hover preview, not a second click state. Unfolding is reversible and undoable, so the preview is a convenience for "which rule is this", not a way to recover lost text. | M2 |
| 55 | Is foldability a designated slot, or a framework base concept? | Leaning | A designated slot, mirroring the name slot. A `Collapsible` base concept would tie foldability to concept inheritance, which cannot express "this rule folds, that one does not" inside a single concept — and the user's requirement is precisely "only when the node has the property set". | M2 |
| 56 | Is the fold set persisted per document? | Leaning | Per document in project settings, keyed by model URI and node id, the same place variants go (question 27). Never in the model file: a fold is not model state, and a saved fold set in the document would make the file change on hover. | M3 |
| 57 | Can a single child slot fold, rather than the whole node? | Open | Only if a real language asks. It needs the caret and layout to agree on a slot whose cell is a node address, which the current cell model does not have — that is a projection-layer change, not a gutter feature. | M5 |

## Quick fixes

The repairs a diagnostic carries, and how they are reached. The design is
[quick-fixes.md](architecture/quick-fixes.md); these are the parts still open.

| # | Question | State | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 58 | How does a fix find its diagnostic again after the checks re-ran? | Leaning | Joined on `(Diagnostic, Id)` against the current set, and a pick that no longer resolves is rejected with a reason - never re-matched by node, because "some fix for this node" is how a repair lands on a problem it was not built for. | M1 |
| 59 | Do a language's own checks live in the language package or in the framework? | Leaning | In the language package, beside its notation and emphasis, so referencing the package is enough for a host to get them. The framework ships no check runner before M4 and gains one only when a second language needs it. | M1 |
| 60 | Can one fix repair several diagnostics at once ("fix all")? | Open | Not in the first slice. Repairs that overlap or contradict need a conflict policy (apply in which order? re-check between steps?), and that policy is not a side effect of one fix. Reopen at M4, when the constraint engine produces findings in volume. | M4 |

## Language authoring

A language can be written in C# or in the project, and both stay
([ADR 0014](adr/0014-both-authoring-paths-are-permanent.md),
[language-authoring.md](architecture/language-authoring.md)). These are the parts of that
arrangement that are not mechanics.

| # | Question | State | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 61 | Is the definition language one language with an aspect per section, or one language per aspect? | Leaning | One language per aspect, as in MPS. A new kind of aspect is then a new language plus an annotation concept rather than a new value in an enum the framework owns - and "the set of aspects is closed" is the one thing a language workbench must not decide. | M5, with P3 |
| 62 | Is exported C# a one-way artefact or can it be re-imported? | Leaning | One-way, with a generated header saying so. The declared model stays the source of truth ([ADR 0002](adr/0002-model-as-source-of-truth.md)); allowing an edit of the generated file back into the project would give one language two sources that diverge on the first behaviour change. | P6 |
| 63 | What does the workbench show for a declared concept that has no editor model? | Open | The same `MissingNotationCell` a C# language shows, plus the editor model opened at that concept. A generic fallback form would hide the notation work behind something that looks finished, and every language would ship with it. | M5, with P5 |
| 64 | Does a slot carry a minimum cardinality, and where? | Leaning | A minimum on the slot declaration, not on `Cardinality`: `One`/`Many` describes storage, while "at least two rules" is a validity statement the constraint engine reads. The bracket syntax (`Rule[1..n]`) already promises it, so dropping it silently is the one option that is not available. | P4 |

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
| Language authoring | C# and project paths, both permanent | [0014](adr/0014-both-authoring-paths-are-permanent.md) |
| Execution | interpreter *and* text generation | ADR to be written (M4) |
| Pilot DSL | rule/decision tables | ADR to be written (M4) |
