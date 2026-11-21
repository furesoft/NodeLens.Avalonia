# Gaps against MPS: what the tutorials show and this framework does not have

Status: **a survey, not a plan**. Everything listed here was found by reading MPS' own material and
comparing it with this repository; the milestone columns say where an item would land if it is taken up, and
the last section says what still needs a decision. Nothing here is a commitment, and nothing here changes an
ADR.

## Where this comes from

- [MPS Tutorials](https://www.jetbrains.com/help/mps/mps-tutorials.html) and its two tutorials: *Shapes - an
  introductory MPS tutorial* and the *HTTP support plugin*.
- [Commanding the editor](https://www.jetbrains.com/help/mps/commanding-the-editor.html), which is the
  editor-experience reference the tutorial links to.
- [Fast Track to MPS](https://www.jetbrains.com/help/mps/fast-track-to-mps.html), whose Step 6 lists the
  aspects a language definition consists of - the list this survey is measured against: Structure,
  Constraints, Behaviour, Editor, Actions, Intentions, Typesystem, Dataflow, Generator, TextGen, plus
  dependencies and imported languages (Step 9).

Two things are deliberately *not* counted as gaps, because this repository rejected them on purpose and says
so: `smodel`-style query languages **inside aspect blocks**, and the Java type system
([language-authoring.md](language-authoring.md#why-it-has-to-look-like-mps)). The first exclusion is about
where a query may appear, not whether the framework has one: an aspect block stays a closed rule shape, and a
query language of its own - a document you open, not a fragment you embed - is planned in
[smodel.md](smodel.md).

## Language definition

| MPS feature | Where MPS shows it | Ours |
| --- | --- | --- |
| Concept interfaces (`INamedConcept`, `implements`) | Shapes, the `Canvas` concept | planned: [interfaces.md](interfaces.md), the first slice of M5 |
| Dependencies and imported languages, per module and per model | Shapes, "The first touch on dependencies"; Fast Track Step 9 | missing; planned as `language.mps` with dependencies and extended languages ([language-projects.md](language-projects.md#what-is-still-missing)) |
| **Accessory models** - models inside a language that hold ordinary nodes (the pre-defined colours) which become part of the language and are visible to its users | Shapes, "Pre-defined colors" | **missing**: a language can ship registries, not nodes. The consequence is the one that matters: no built-in library instances - no colour palette, no standard units, no ready-made types |
| **Alias** - the word completion offers and typing creates an instance of | Shapes, the `Circle` concept | **partly delivered**: `ConceptDeclaration(alias:)` is drawn in the structure language and is the word a proposal list shows and filters by. What MPS adds on top - an instance created from the typed alias alone - needs the typing-rule seam and stays open |
| **`instance can be root`** | Shapes, the `Canvas` concept | **delivered**: `ConceptDeclaration(isRoot:)` refuses a concept that should only ever be a child, and `Model.NewNode` is where the refusal happens. It is separate from `Applicability` on purpose: "may start a document" is a fact about the concept, while a place says where a node may stand |
| **Behaviour** - methods on concepts, invoked polymorphically | Fast Track Step 6; the concept-functions example under [Concept Functions](https://www.jetbrains.com/help/mps/concept-functions.html#example) | **split, and only half of it is planned here**: the closed set of pure functions this framework can honour without a code base language - a node's `Name` and a `Describe` sentence - is planned in [behaviours.md](behaviours.md). Behaviour *methods* with real bodies, and concept functions through which a language user supplies code the generator emits, are the M6 "projected C# base language" row and arrive with it or not at all. Three of the four methods the example overrides (`getExpectedReturnType`, `getParameterConcepts`, `showName`) have **no analogue at all**, because they describe a Java class MPS generates and this framework generates none |
| **Actions** - per-language AST transformations behind copy, paste and replace | Fast Track Step 6 | missing: our clipboard and replace are generic and fixed. It needs a behavioural language, so it arrives with the C# base language or not at all |
| **Dataflow** - value and control flow per concept, so unreachable code and null risks are reportable | Fast Track Step 6; the survey of what MPS offers names it as not taken | **missing and unscheduled**. The largest aspect this framework has no plan for |
| **Generator macros and rule selection** - property, node and reference macros, `COPY_SRC`, `LOOP`, template fragments, mapping labels, root mapping versus reduction rules, queries | Shapes, the whole "Generator" chapter | **structurally different**: text generation is nine closed template parts with no macros, and one template per concept. Model-to-model generation is planned ([model-generation.md](model-generation.md)) and carries reduction rules; **mapping labels** - several rules for one concept, chosen by context - are missing even from that plan |
| **Tests as models** - a `unitTest` language, tests living in the model and run from the IDE | Fast Track, the Garage and Calculator tutorials | **planned** ([language-tests.md](language-tests.md)): a `tests/` folder of declarative assertions, a runner in the behaviour band, and one xunit method per language project as the bridge to CI |
| **SModel queries** - a language over the tree: `parent`, `children<link>`, `ancestors<...>`, `descendants<...>`, `isInstanceOf`, casts, `concept/Name/`, `node-ptr/.../`, model queries | the four smodel pages; used inside actions, refactorings and the generator | **planned** ([smodel.md](smodel.md)): the same operation words and meanings, a pure-read language with no modification operations, and concept keys of our own. Not file-compatible with MPS |
| Property accessors and "allowed values" beyond an enumeration | Fast Track Step 6 | partial: `PropertyType` validates what a slot may hold and a C# constraint can state anything; the *declared* constraint language has four bodies ([semantics.md](semantics/constraints.md#language-of-constraints)) |
| Devkits - a bundle of languages and solutions | MPS' project model | missing; it is a packaging question, and packaging is M6/M7 |

## Editor experience

| MPS feature | Gesture | Ours |
| --- | --- | --- |
| **Comments** | `Ctrl+/` | **missing entirely** - there is no comment concept in the model and no gesture in the editor. The most noticeable loss in daily use |
| Go to definition | `Ctrl+B`, `Ctrl+click` on a reference | **shipped**: `GoToDefinitionAction` moves the caret to the declaration a reference names, with its name selected, when that declaration is drawn in the document; a target in another document is a rejection, and opening it stays the host's job |
| Find usages | `Alt+F7` | **shipped**: `ReferenceIndex` answers who points at a node across the models of a project, and the shell lists the rows in a panel beside the document |
| Rename (project-wide) | `Shift+F6` | **shipped**: **Edit ▸ Rename...** writes the name and refreshes the fallback text of every reference in the document as one undo step; a reference in another model keeps its fallback, because undo is per model |
| Block selection expand and shrink | `Ctrl+Up` / `Ctrl+Down` | partial: `Ctrl+A` climbs a three-rung ladder (value, node, document) and cannot shrink |
| Node Explorer and Inspector | `Alt+X`, `Alt+2` | partial: the app has an outline (a flat, read-only list) and no inspector for cell or property details - which is what makes the editor and generator definitions convenient in MPS |
| Inheritance / structure view | `Ctrl+H` | missing |
| Surround with | `Ctrl+Alt+T` on a selection | the capability exists (the range form of `Wrap selection in "X"`); the gesture does not |
| Completion fine points: CamelHumps, a second `Ctrl+Space` that resets the filter, grouped and flattened hierarchical menus | `Ctrl+Space` | partial: proposals are filtered by prefix only, and there is no reset gesture |
| Paste from history | `Ctrl+Shift+V` | missing: a clipboard holds one payload |
| Duplicate, insert before | `Ctrl+D`, `Insert` | **shipped**: `EditorKey.Duplicate`/`.InsertBefore` route to `DuplicateAtCaretAction` and `InsertBeforeAtCaretAction`, which resolve the node from the caret and delegate to the action the menu already used. Insert means *before*, because Enter is the after; Shift+Insert stays the host's, since that is paste where it exists |
| Error message at the keyboard | `Ctrl+F1` | partial: the marker tooltip is hover-only |
| Show the type of the expression | `Alt+Shift+F10` | missing, although the type registry already holds the answer |
| Postfix transformations | `expr.if`, `.var`, `.not` | missing; our triggers are prefix and infix. It belongs to the C# base language (M6) |
| Editor components - a reusable notation fragment another concept's notation embeds | Shapes, "Embedding the editor component" | partial: notation *is* inherited from a super concept, but a language cannot include a named fragment in the middle of a line |
| Cell intentions inside editor definitions ("Add On New Line") | Shapes, the `Canvas` editor | missing: our definition languages ship make abstract/concrete and little else |
| Live templates | `psvm` | missing; a host concern rather than a framework one |

## Deliberately different, not missing

- **No rebuild step.** MPS compiles a language and makes you rebuild after every change; this framework
  lowers a definition when it is loaded, so a definition and its language cannot disagree.
- **No default editor for a concept without a notation.** MPS generates one for prototyping; we draw
  `MissingNotationCell` on purpose, so that unwritten notation is visible instead of looking finished
  ([language.md](language.md#notation)).
- **A generator with no programming language in it.** MPS lets a template hold arbitrary BaseLanguage code;
  this framework's template parts are closed, for the same reason the type rules have three bodies and the
  constraint language four ([semantics.md](semantics/types.md#the-declared-form)).
- **No graphical notation, no `smodel` queries inside an aspect block, no Java type system** - the exclusions
  above. A standalone query language over the tree is a different thing, and it is planned
  ([smodel.md](smodel.md)).
- **A missing-notation cell is not a bug**, in the same way an empty slot with a placeholder is not.

## Where these would land

| Milestone | What |
| --- | --- |
| M5 (self-hosting, language projects) | accessory models; `language.mps` dependencies and imported languages; an instance created from a typed alias |
| M6 (framework maturity) | mapping labels in model generation; the smodel query language ([smodel.md](smodel.md)); concept behaviours - a node's `Name` and `Describe` as declared pure functions ([behaviours.md](behaviours.md)); the Actions aspect and behaviour *methods* with real bodies, both with the C# base language; tests of a language as data ([language-tests.md](language-tests.md)) |
| M7 (release hardening, editor polish) | comments; block-selection shrink; inspector and hierarchy view; completion filter reset and camel humps; show type; paste history; the missing keys (duplicate, insert before) |
| unscheduled | data flow; property accessors |

## What still needs a decision

- **Where a comment lives.** A designated child slot on a concept everyone shares, a framework `Comment`
  concept a language extends, or a per-language concept like any other. The first two need a base concept and
  this framework has none on purpose; the third is honest but means a language without a comment concept
  cannot be commented, which is the state every new language starts in.
- **Whether data flow is worth an engine.** It is the only MPS aspect with no plan at all, and the price is a
  flow-analysis pass with per-concept rules - the same order of work as the type system, without the same
  payoff for small DSLs.
- **Whether an accessory model is a folder or a model.** A `library/` folder beside `structure`/`editor`, or a
  normal `.mps` document inside the language project that the project marks as part of the language.
- **Whether tests-as-models is worth the vocabulary.** It was; the plan is [language-tests.md](language-tests.md), which
  keeps the assertions declarative and the runner in a behaviour package. What that plan still needs decided is
  how a test names the document it is about.
- **Where an editor-experience feature lives.** Find usages settled the shape: the question is the framework's
  (`ReferenceIndex`, `FindUsages`) and the panel is the host's, the same split the intention catalogue makes. Go to
  definition and the inspector are expected to follow it.
