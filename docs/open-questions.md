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
| 17 | Do languages ship with example models as tests? | Leaning | Yes — a language package without a golden corpus is untestable and will regress. The three worked examples execute that rule for the framework's own demonstrators: each ships a `Corpus/` that round-trips byte-identically ([worked-examples.md](architecture/worked-examples.md)). | M6 |
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
| 34 | Does an intention ever preview its effect before applying? | Leaning (answered) | Yes, on a longer hover, by applying the edit to a discarded copy and projecting that - never by a per-action describe form, which would be a second implementation to keep in step ([intention-preview.md](architecture/intention-preview.md), [ADR 0017](adr/0017-preview-is-a-discarded-edit.md)). A preview dialog is still not wanted: the page itself becomes the result. | M3 |

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
| 98 | JSON and XML parse with the platform library. .NET has no TOML or HOCON reader, so may those two take a package dependency? | Leaning | Yes, in the reference language's own package and never in the toolkit: `Projectional.Toml` and `Projectional.Hocon` are behaviours a host opts into, exactly as `Projectional.Checks` is, and a hand-written TOML or HOCON reader is the liability the JSON decision already refused. The requirement that picks between two candidate readers is not size or speed but **source positions**: `ImportDiagnostic` promises a line and a column, and a reader that only answers a dictionary makes tier-1 diagnostics unable to point at the text. | M6 |
| 100 | Is a declared importer a grammar of its own, or the inverse of the generator's templates? | Leaning | The inverse first: the notation and the generator already say how the language is written, and a third statement of the same syntax would drift from both. A declared rule carries only what the inverse cannot express - an escape, an optional short form, a comment. A full grammar stays the answer for a format whose file shape is not its generated shape, and choosing that is a decision rather than a default. | M6 |
| 101 | May a declared importer fall back to C# for part of its work? | Leaning | No. One delegate would put the declared path back behind code, and the parity test could no longer be "the same model from both paths". A format the vocabulary cannot describe is a gap in the vocabulary - the rule ADR 0014 already gives for every other aspect. | M6 |
| 102 | Where does a declared importer put escaping? | Leaning | In its token rules, on the way in - the same place `JsonImporter` and `XmlImporter` settle it. The template vocabulary has to stay free of an escaping part, or every language that prints would need one. | M6 |
| 103 | Must a declared importer model everything its format allows? | Open | No: `Import.Raw` keeps an unmapped run verbatim, which is tier 2 and the same promise `Xml.Raw` already makes. What must not happen is a silent skip, because that is the rewrite invariant 8 exists to prevent. | M6 |
| 104 | May a token class carry its own character set, or do the lexical classes stay fixed? | Leaning | A declared set of *extra* characters (`plus . -`), never ranges and never a pattern: it is the smallest parameter that describes an identifier in INI, TOML and a DSL alike, and a regular expression would need an engine the framework does not have - the same line [98](#text-import) draws about readers. | M6 |
| 105 | May `Import.Matches` compare anything beyond one remembered run? | Leaning | One run, compared ordinally and unnormalised. XML's closing tag is the case that needs it and the only one known; a comparison that folded case or whitespace would be a decision two format packages would have to agree on, which is exactly what a closed vocabulary avoids. | M6 |
| 106 | Is `Import.Fail` part of the vocabulary, or is the generic "expected ..." message enough? | Leaning | Part of it, carrying a code and a message and never control flow: "a trailing comma is not allowed" is the sentence a user needs and only the rule's author knows it. It must not grow a condition, a branch or a severity argument. | M6 |
| 107 | May significant whitespace ever be an `Import.Skipped` token? | Leaning | No. A skip is consumed between parts and produces no node, so a format whose whitespace is content (`Xml.Element`'s text today) reads it in a rule instead - the two are different promises and the importer is where they stay apart. | M6 |
| 108 | What does an importer invert when its language declares two generators? | Leaning | The one it names: `Import.Importer.target` is a generator name resolved at load, a language ships one `import` document per target, and an undeclared name is a load error. The inverse of two template sets is not one reading, so "which generator" cannot be a detail the lowering decides silently. | M6 |

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
| 59 | Do a language's own checks live in the language package or in the framework? | Leaning (answered by Q3) | In a checks package *beside* the language, not inside it: a check produces diagnostics and repairs, so it needs `Semantics` and `Editing`, while a language package references only `Language` so that it can be used headlessly. `Projectional.Samples` plus `Projectional.Samples.Checks` is the shipped shape. The framework still ships no check runner before M4. | M1 |
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
| 64 | Does a slot carry a minimum cardinality, and where? | Decided by design | On the slot, not on `Cardinality`: `One`/`Many` is the storage shape, `ChildSlot.Minimum` is how many the slot needs, and the upper end is the cardinality's own (one, or as many as there are) - so the four ranges MPS offers in one cell are the four combinations: `0..1`, `0..n`, `1`, `1..n`. It is **reported, not enforced**: `STD010` speaks up for a single child whose minimum is one, `STD011` for a list whose minimum is at least one, and deleting the last element of a `1..n` list stays an ordinary edit. A minimum above one ("at least two rules") is representable in the model but has no spelling in the notation yet - it waits for a language that asks. | P4, delivered |

## Intention preview

What an intention would do, shown on a longer hover by applying it to a discarded copy
([intention-preview.md](architecture/intention-preview.md),
[ADR 0017](adr/0017-preview-is-a-discarded-edit.md)). Question 34 above is the parent decision; these
are the numbers and edges the design leaves open.

| # | Question | State | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 69 | How long is "long enough" to hover, and is it configurable? | Leaning | 300 ms for the description, 700 ms for the preview, 150 ms of grace when the pointer leaves to move between the bulb and the list. Configurable through `PreviewDelay`, because the tolerance for a preview that appears while reading is not the same for every user. | V3 |
| 70 | Does the preview follow keyboard navigation, or is it pointer-only? | Leaning | It follows, with the same delay. A list walked with the arrow keys would otherwise either flash a preview per entry or show none at all, and the keyboard user would be the one who cannot see the result. | V3 |
| 71 | Does a preview get its own budget per document or per session? | Open | Per document, measured in nodes copied: a preview is about one editing context, and a session-wide budget would let a second tab starve the first. Revisit if a host opens many small documents. | V2 |
| 72 | What happens when the edit is previewed but applying it would be refused? | Leaning | The preview shows the result anyway and the badge says "would be rejected"; refusing to preview is the one answer that hides the reason. It needs `EditResult` to be checkable without applying, which is a change to how a preview runs its action. | V2 |
| 73 | Does a destructive offer need a confirmation, and where is it expressed? | Decided by design | Settled twice, and the second answer is the simpler one. First a safety class on the offer (`EditSafety`: `Plain` / `Destructive`) so that the gesture would not apply a lone destructive offer; then, because one keystroke that sometimes acts and sometimes asks is a keystroke the user has to guess about, the gesture stopped applying anything: it always opens the list, and a list of one entry is ordinary. `EditSafety` survives as information a surface may show - nothing in the editing layer reads it as a rule, and the hazard it was invented for (a keystroke that fills an empty slot being the one that deletes a node) cannot arise. Implemented in Q7 (`DiagnosticFix.Safety`, `EditorSession.ApplyOffers`); the later simplification left `DiagnosticFix.Safety` in place and removed the reading. | Q7, E3 |
| 74 | May a standard rename repair update the references to the name it changes? | Leaning | Not the *standard* one: it looks for referrers and stays silent when there are any, because a repair that silently leaves three other models pointing at a name nobody has is worse than no repair. Updating references is the M6 rename refactoring, which may know how to find them. `STD002` has not shipped yet, so the question still has to be answered before it does. | Q7 |
| 75 | Is the standard catalogue a package of checks or a set of framework helpers? | Decided by design | A package (`Projectional.Checks`) that a language lists, so the catalogue can be versioned and extended without touching the core, and so a language that wants none of it pays nothing. The framework ships the mechanism and the aspect the checks read, not the policy. Implemented in Q7: `ICheck`, `CheckSet` and `StandardChecks` (`Names`, `Structure`, `Expressions`, `All`) in a project of their own. | Q7 |
| 76 | Does `Wrap in` offer every concept that fits, or only those the language nominates? | Leaning | Every concept whose single-child slot accepts this node's concept and which can itself stand in this node's place, in declaration order. That is the registry's own answer and needs no new declaration; a language that wants fewer marks them `isOfferedInCompletion: false`, which it already uses for literals. | G |
| 77 | May `Wrap in` produce a wrapper whose own slots are still empty? | Leaning | Only when the wrapper's other slots are empty or defaulted; otherwise the intention would produce a document that is immediately invalid and needs a fix - so it stays a substitution the user completes, not an intention with a warning. | G |
| 78 | Do the standard intentions and the standard checks ship in one package? | Leaning | One package, listed once by the language: they share the safety class and the "what a language gets for free" story, and splitting them would make a language author solve the same inclusion question twice. | G |

## Embedded shell

A host embeds the shell itself - not only the editor control - with one language already loaded
([embedded-shell.md](architecture/embedded-shell.md),
[ADR 0015](adr/0015-shell-is-a-library.md)). These are the parts of that arrangement that are
choices rather than mechanics.

| # | Question | State | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 65 | Does a shell embedded as a pane show its own menu bar? | Leaning | Only when asked. A `ShellWindow` shows it by default, a `ShellView` used as a pane does not: a docked pane that installs a second File menu inside the host's menu bar is the classic embedding annoyance, and the commands are available through the registry either way. | E2 |
| 66 | May a host show two shells with different languages in one window? | Leaning | Yes, and it costs nothing but the rule that already exists: no process-wide state. It constrains nothing today and is worth one test, because a static read through the shell would be discovered by a user, not by a reviewer. | E5 |
| 67 | Is the model list flat, or can a host supply a hierarchy? | Open | Flat for the first slice: `IShellStorage` returns a list, and a project with a folder hierarchy of models is a real need only once a host has hundreds. A second interface (`IShellModelTree`) can be added without changing the shell, but not removed later. | E4 |
| 68 | Who remembers the last active document? | Leaning | The host, through `ShellOptions.InitialModel` and a shell event; the shell persists nothing. It is the same rule as recent files: state that outlives the process belongs to whoever owns the process. | E4 |

## Concept descriptions

A concept carries one sentence of documentation, shown on hover and beside the completion entry
([concept-descriptions.md](architecture/concept-descriptions.md),
[ADR 0018](adr/0018-concept-description-is-language-data.md)). Where it lives is decided; how it is
shown is not.

| # | Question | State | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 79 | Does a hover show only the node's own concept description, or the whole ancestry? | Open | Own concept only, and offer the ancestry as a second line only if a real language asks. An inherited sentence describes a base the user cannot see, and a list of five ancestors is a worse tooltip than one accurate sentence. | D3 |
| 80 | Is the tip's heading the concept key (`Rules.Rule`) or a friendly label (`Rule`)? | Leaning | The key. The notation never shows it, and a language author reading a tip needs the name they can grep for; a label is friendlier for the language's user but leaves the author guessing which concept they are looking at. A short label can become a second line once a language declares one. | D3 |
| 81 | Should a slot carry its own description? | Open | Not in D1. A slot is already named in the notation (`condition`), so the marginal value is smaller than for a concept, and it doubles the declaration surface. Revisit when a language has a slot whose name does not explain it. | D5 |

## Language descriptions for LLMs

Planned as M8, after 1.0, in [llm-language-description.md](architecture/llm-language-description.md).
The principle is decided - one walk over the declarations produces the schema and the guide, so a
description cannot drift from the language. What is open is its shape.

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 82 | Which format does a generated document use: the existing XML, a JSON dialect, or both? | Leaning | The existing `.mps` XML and the clipboard's fragment shape, and no third format: a JSON dialect would be a second wire format to keep in step with the first, while the reader already refuses what it cannot build. Revisit only if evaluation shows parse errors dominating. | M8 |
| 83 | Where does the description generator live: the toolkit, `Semantics`, or a package of its own? | Leaning | A package of its own (`Projectional.Descriptions`), the way the check and intention catalogues are: it needs `Semantics` for constraints, and [ADR 0019](adr/0019-toolkit-is-the-language-surface.md) deliberately keeps `Semantics` out of the toolkit surface. | M8 |
| 84 | Does the operation protocol name nodes by path or by id? | Leaning | By path (`0/rules/2/condition`), because an id is internal and a model cannot know one. The path is resolved when the proposal applies, so a stale proposal is refused with a reason instead of landing on whatever inherited that id. | M8 |

## Model generation

Planned as M6, in [model-generation.md](architecture/model-generation.md). The pipeline is
decided - reduce into a model of the target language, then let that language's generator print
it - because a target with concepts of its own cannot be printed from templates without keeping
its grammar in strings. What is open is how far one rule may go.

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 85 | May a rule produce nothing at all, and what happens to a reference whose target produced nothing? | Leaning | Yes to the first: an `Empty` rule is a decision, exactly as an empty template is, and a source concept that lowers to nothing is common (a keyword, an annotation, a shorthand). A reference *to* something that produced nothing then fails the run, because the alternative is a dangling reference in a model that gets printed. | M6 |
| 86 | Is exactly one output node per source node too strict for a construct that lowers to several statements? | Leaning | For the first slice, yes to one node: the extra statements are its children, which is what keeps "where did this node go" answerable when the parent's slot is single-valued. Revisit when a target language has a construct that cannot be nested inside the node its source maps to. | M6 |
| 87 | Does a rule ever need a computed value - an index, a concatenation, a formatted number? | Leaning | No. A part only copies, chooses and reduces. A computed value would make a transformation a programming language with no debugger, and the declared form could not express it ([ADR 0014](adr/0014-both-authoring-paths-are-permanent.md)); the answer is a concept in the target language that computes it at run time, or a generator in the host's code. | M6 |
| 88 | How does a mapping in a language folder name the language it targets? | Open | The lowering takes the target registry as an argument until a language project can declare dependencies (M5's language-project row), then `mapping.mps` names the language root it maps to. Naming by concept-key prefix would be a second way to say "language" and would break when two languages share a key. | M5 |
| 89 | Is the generated model kept after a transformation? | Leaning | No: it is printed and discarded unless the caller asks for it, and the migration tool is the caller that does. A generated model kept as a document would be a second thing the editor owns, with a save prompt and a dirty flag, for a value the source can always produce again. | M6 |
| 90 | Is a pipeline the unit a host offers, or does the shell pair a model generator with the target's first text generator? | Leaning | The pipeline, declared by name. A menu that guessed which text generator to print with would change its output silently the moment a language added one, which is the opposite of what a generated artefact has to promise. | M6 |
| 91 | Does a transformation ever reorder or group children - sort a list, hoist declarations? | Open | Not in the first slice: `Children` keeps document order, and a target that needs another order needs a part that says so. Sorting in a rule would be a second ordering rule beside the model's, and the first consumer (a decision table becoming a method) does not need one. | M6 |

## Interfaces

Planned as the first slice of M5 in [interfaces.md](architecture/interfaces.md). The shape is
chosen - a concept kind whose members the implementer inherits, `Framework.Named` in every
registry, metadata overridable by the implementer - and these are the parts a second consumer
should decide rather than a plan.

| # | Question | Status | Recommendation | Decide by |
| --- | --- | --- | --- | --- |
| 92 | Is a concept's kind one enumeration (`concrete`/`abstract`/`interface`) or two booleans with a validated implication? | Decided in I1 | Two booleans. `isAbstract` is already a persisted property of every definition model, and an enumeration would need a new property type plus a re-saved corpus for a distinction the registry validates anyway - so `isInterface: true` requires `isAbstract` and `ConceptDeclaration` refuses the combination that would mean nothing (`ArgumentException`, `ParamName` `isInterface`). Revisit when a fourth kind appears. | M5, I1 |
| 93 | May an interface declare a child slot? | Decided in I1 | No. A child slot is shape, and merging one into an unrelated implementer makes that implementer's notation owe a cell it never asked for; a language that needs composition declares the slot on a shared abstract concept instead. Enforced by `ConceptRegistry.ValidateInterface`, which refuses an interface with a child slot. Revisit with a language that wants "any node with a body". | M5, I1 |
| 94 | Does a marker interface with no members mean anything? | Decided in I1 | It is legal and means exactly what `IsKindOf` means: a slot declared over it accepts the implementers. Do not give it a second meaning such as "not persisted" - a marker is a type, not a behavioural switch. A registry resolves an interface with no slots and no implementers without complaint, which is what `ConceptRegistryTests.AnInterfaceWithNoMembersIsAType` pins. | M6 |
| 95 | Does `Framework.Named` also promise that names are unique? | Decided in I3 | No. The property and the designation are the interface; uniqueness depends on where a node stands (two rules of one ruleset may not share a name, two rulesets may), which is a constraint's question and not a type's. Adoption in `Expressions`, the rules language and the pipeline sample shipped without it, and nothing needed it. | M6 |
| 96 | May a concept narrow an inherited member's shape - a child slot over a more specific concept, a property over an enumeration where the interface said string? | Decided in I1 | No: shape equality is what makes the merge safe and an override readable, and narrowing is covariance in a storage model that has none. `ConceptRegistry.HasSameShape` refuses a different property type, reference target, cardinality or minimum, and keeps only the metadata overridable. A language that needs the narrower slot declares it itself and does not implement the interface. | M6 |
| 97 | Does the framework ship a second interface (`Collapsible`), replacing the collapse-slot designation? | Open | No. The designation stays, because a concept may want its boolean named `foldable`, and question 55's per-node reason is unchanged; an interface would be a ready-made default, not a replacement. Add one only when two languages ask for the same member. | M6 |

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
| Shell | a library a host composes, lockable to one language | [0015](adr/0015-shell-is-a-library.md) |
| Quick fixes | edits a host supplies, applied by the session as one undo step | [0016](adr/0016-quick-fixes-are-edits-a-host-supplies.md) |
| Intention preview | a projection of a discarded edit, never a second description | [0017](adr/0017-preview-is-a-discarded-edit.md) |
| Concept descriptions | language data on the concept, never per node, never inherited | [0018](adr/0018-concept-description-is-language-data.md) |
| Execution | interpreter *and* text generation | ADR to be written (M4) |
| Pilot DSL | rule/decision tables | ADR to be written (M4) |
