# Worked examples: three languages that prove the axes

Status: **planned** (M4, after what is left of its semantics). Nothing of this exists in the tree yet;
`Projectional.Samples` is still the only worked example, and this document is the plan of record for the
three that follow it.

## Why

The framework ships exactly one worked example - the toy rules language in `Projectional.Samples` - and it
exists to prove that *composing* a language out of a shared sub-language is a handful of declarations. It is
deliberately not a package, and it exercises one axis: children, one enum property, and a table variant.

That leaves the framework without a demonstrator for the axes it actually bets on: references, constraints,
a generator, an interpreter, and a concept rendered two ways in anger. Open question 17 already says a
language package without a golden corpus is untestable and will regress
([open-questions.md](../open-questions.md)); the same is true of a *sample*, and a sample that lives only in
code, with no corpus and no test, is documentation that rots.

## What

Three new language packages under `src/`, one per DSL, each proving a different axis, each shipped the way a
real language ships:

| Artefact | What it is |
| --- | --- |
| the language | concepts and notation, following the `Projectional.Samples` shape (a language package references only `Language` or the toolkit) |
| `Checks/` in the same project | `Projectional.Samples/Checks/` is the template: a `CheckSet` over `Projectional.Checks`' `ICheck` ([ADR 0021](../adr/0021-one-project-per-language.md)) |
| `Corpus/` | `.mps` documents that must load and save byte-identically, which is invariant 7 and the same rule the persistence corpus follows ([testing.md](testing.md#the-round-trip-corpus)) |
| snapshot tests | the notation pinned as text, so a projection change that breaks a sample fails an ordinary test ([testing.md](testing.md#the-snapshot-harness)) |

One DSL per axis, chosen so the three do not overlap:

| Package | Axis it proves | Needs |
| --- | --- | --- |
| `Projectional.Samples.StateMachine` | references, one concept in two notations, constraints (unreachable state), interpreter trace | **delivered** |
| `Projectional.Samples.FeatureModel` | constraints (`requires`/`excludes`), a generator to a config file, an interpreter over the selection | **delivered** |
| `Projectional.Samples.SqlSchema` | cross-model references, the table cell in earnest, a generator to DDL | **delivered** |

They are examples, not products: none of them may grow a capability the framework does not have, and none
gets an app-private feature (composition rule 5 in [standalone.md](standalone.md)).

### Non-goals

- **No new framework features.** If a sample needs something the framework cannot express, the sample is
  wrong, not the framework - except where this document names the gap explicitly.
- **No graphical notation.** A state machine is drawn as text and as a table, not as a diagram; diagrams are
  out of scope before 1.0.
- **Not three pilot languages.** The M4 pilot stays rule and decision tables. These are additional
  demonstrators and must not compete with it for the gate.

## 1. State machine - `Projectional.Samples.StateMachine`

A machine with states and transitions. A transition *references* its source and its target, and carries an
optional guard written in the shared `Expressions` sub-language.

| Concept | Slots |
| --- | --- |
| `Sm.Machine` | `name`, `states`, `transitions`, `initial` (reference to a state) |
| `Sm.State` | `name`, `isFinal` |
| `Sm.Transition` | `source` and `target` references, `event`, `guard` (a child of `Expressions.Expression`) |

`Sm.Machine` has **two notations**, chosen at projection time and never persisted
([ADR 0012](../adr/0012-concept-rendered-more-than-one-way.md)): the block form, and a `Table` over
`transitions`. `isFinal` is drawn with a `Conditional` cell, so the word `final` appears only when it is true -
which is the conditional cell earning its place in a real language rather than in a test.

The checks package ships one rule the framework's catalogue cannot state: **an unreachable state** - a state
that is not the initial one and that no transition leads *into*. The direction is the whole rule: asking what
*leaves* a state would report every final state on every machine, and the first draft of the constraint did
exactly that.

The interpreter evaluates a machine against a start state and answers the trace, so the output panel
demonstrates what the trace rows are for: `Transition` is a predicate (does its guard hold), `Machine` is the
walk from `initial` that takes the first transition whose source matches, and a machine that settles in a
non-final state or exceeds `MaxSteps` is a failure that says where it stopped.

**Delivered.** `src/Samples/Projectional.Samples.StateMachine` (language, notations, evaluation),
`src/Samples/Projectional.Samples.StateMachine/Checks` (the unreachable-state constraint), `Corpus/release.mps`
written by the writer and re-saved byte-identically, and `StateMachineNotationsTests`,
`StateMachineConstraintsTests`, `StateMachineEvaluationTests`, `StateMachineCorpusTests`.

Two findings from building it, both recorded rather than worked around:

- **A table column could name a property or a single child, never a reference.** `NotationRegistry.ValidateTable`
  refused it, so the transition table first drew only `event` and `when` and left `source`/`target` to the block
  form. **Closed since**: a column may name a reference, so the table now draws `from` and `to`, and the same
  capability is what makes the SQL schema's foreign key a column (§3).
- **A graph over children needs `allowOwnSubtree`.** A transition references its source and target, both states
  of the same machine, so without that flag the reference would point into the referring node's own subtree -
  which invariant 5 refuses. The state machine is the first language to need it.

## 2. Feature model - `Projectional.Samples.FeatureModel`

A list of features with `requires` and `excludes` references, a selection, and a generator that writes the
configuration out. (The plan said *tree*, and the list is flat for the reason recorded below - a generator
template cannot lay out nested children.)

The axis is **constraints that read references**: a selected feature whose `requires` target is not selected,
and one whose `excludes` target is. The interpreter evaluates a selection against those rules, which is the
first consumer of the value model that is not a decision table. The generator emits a flat `key=value`
configuration, so "a language has as many generators as it declares" gets a second, smaller target beside the
pilot's JSON and C# fragment.

**Delivered**, with two decisions taken while building it and one gap recorded:

- **A constraint is a node, not a property of a feature.** `Fm.Requires` and `Fm.Excludes` share an abstract
  base `Fm.Constraint` that holds `subject` and `other`, because a slot names one concept and the
  `constraints` list must hold both kinds. The alternative - one concept with an `operator` enumeration - would
  force every reader of the model to switch on a value that the drawn word already states.
- **The selection is a property, not a variable.** `Fm.Feature.isSelected` is saved with the document and
  undone with it; the state machine's `attempts` is a value a reader supplies for one run. A configuration
  belongs to the document, which is why it is model state and why the notation can draw it.
- **A many-valued reference cannot be drawn, and a template cannot lay out a tree.** Both are real gaps rather
  than choices: `NotationEditor` has `Reference(slot)` and no collection of references, so `requires` became two
  single-valued slots; and every generator template that surrounds a child list with line breaks emits them
  unconditionally, so a *nested* feature tree would leave a stray break after every leaf. The sample is
  therefore a flat list of features, and the nesting waits for a template part that can ask whether a slot is
  filled - a `ConditionalPart` asks about a property, not a slot.

The check it ships is a duplicate name, **two features under one name**, which breaks the constraints, the
reader and the generated configuration at once. It is a `CheckSet` entry with a repair
(`SetNodePropertyAction` renames the later duplicate), so the two samples between them cover both halves of the
split - the state machine reports without repairing, this one repairs.

Two further findings, both met while writing the repair:

- **A fix could not delete a named node, and now can.** `DeleteNodeAction` was caret-addressed only, so
  "delete this constraint" was not expressible as a `DiagnosticFix`; `DeleteNodeAction(NodeId)` is the
  named form, added with `STD012` (a node that holds nothing but its defaults). The feature model keeps its
  duplicate-name rule all the same: a name collision says more than "this constraint means nothing".
- **A repair is applied through a session that already knows it** (`session.Fixes`), which `FeatureModelChecksTests`
  demonstrates end to end: the fix runs, the name changes, and the finding is gone.


## 3. SQL schema - `Projectional.Samples.SqlSchema`

Tables with columns, and foreign keys that point at a column in **another model** of the same project.

The axes are the two the pilot never touches in earnest: **references across models** (a project holds
several models, and the default scope chain walks up to the project level - that is what "cross-model" means
here, not an import list), and the **table cell** drawing a schema rather than a rule table. The generator
emits DDL: `create table` per model, then the foreign keys, because a key cannot be written before its
target exists - the same ordering argument model generation makes for its build and link phase.

**The one gap it was thought to need is closed.** A table column may now name a reference
(`feat/table-reference-columns`), so a foreign key is a *column* - `parent_id  person` - rather than a
separate constraint line under the table, which is how the feature model draws its rules and would have been
the workaround. `TableReferenceColumnTests` pins the three things that had to be true: the same
`ReferenceCell` a notation's reference draws, the row's own address, and the column's empty text for an unset
target.

**Delivered**, and it found two framework defects, which is the most valuable thing a worked example can do:

- **A cross-model reference was refused.** `Node.SetReference` compared the target's *model*, so a key in
  `orders.mps` could not name a column in `shop.mps` - while the scope provider already walks up to the
  project to offer exactly those nodes, invariant 5's clearing already walked `Project.Nodes`, and
  `model.md` calls a move between models a case `resolveInfo` absorbs. The rule compares the *project* now;
  a target of another project is still refused. Pinned by
  `NodeTests.AReferenceMayPointAtAnotherModelOfTheSameProject`,
  `AReferenceRejectsATargetOfAnotherProject` and `DeletingATargetClearsAReferenceFromAnotherModel`.
- **A template could not emit a reference at all.** The part set had nine parts and none of them a reference,
  so no language could generate the name a key points at. `ReferencePart` (and its declared twin
  `TextGen.Reference`) is the tenth.

What the third sample therefore proves, in the shipped form: two documents of one project, a key that names a
column of the other, the grid of a table, and a DDL fragment whose keys name their columns. `SqlSchemaTests`
covers the byte-identical round trip of both files, the notation, the generated text, the cross-document
resolution and the degradation when one document is loaded alone.

Still open, and recorded rather than worked around:

- **A template cannot lay out a tree and cannot ask whether a slot is filled** - so the DDL's line break in
  front of a table's keys is emitted even for a table with none, and the golden test pins the resulting
  trailing indentation on purpose.
- **A key is a fragment, not an `alter table`**: a template cannot read the name of the node that *holds* the
  node it expands, so the table's own name is not available inside the key's template.
- **A many-valued reference cannot be drawn**, which is still the shape a list of links would take.

## What the three have to prove

1. **The corpus round-trips byte-identically.** Each `Corpus/` document loads and saves back to the same
   bytes, in the ordinary test run - not in a separate harness ([ADR 0005](../adr/0005-xml-persistence.md)).
2. **The notation is pinned.** Each DSL has a snapshot test over its corpus document, so a projection change
   that moves a cell fails a test that names the language (the rule in [testing.md](testing.md)).
3. **The checks find their finding.** Each checks package reports its own finding on a corpus document that
   demonstrates it, and stays quiet on the clean one - the shape `StandardChecksTests` uses.
4. **The generator and the interpreter answer.** The generated text and the run's trace are compared as text
   against a golden expectation, because that is what a consumer sees.
5. **Three axes, three packages, no framework change.** If any sample cannot be written with the published
   surface, that is the finding, and it goes to the roadmap as a gap rather than into the sample as a
   workaround.

Points 1 to 4 stop being three copies of the same C# test once a language can be tested as data: the `tests/`
folder of [language-tests.md](language-tests.md) is where a corpus document and its assertions sit side by
side, and these three packages are its first customers.

## Sequencing

One branch and one package at a time, in the order above: the state machine first (it needed nothing that was
not delivered and is now done), then the feature model (constraints plus a generator), then the SQL schema
(the table cell and cross-model references). Each lands as its own commits - language, checks, corpus, tests -
and each moves its own row into [implemented.md](../implemented.md) when it is finished.

## What is still undecided

- Whether the packages ship as NuGet packages or only as repository sources. `IsPackable` is off for every
  project today, and packaging a sample is a question packaging answers (M6/M7).
- Whether the SQL DDL generator is written in C# or declared in a `generator` folder. The second is the
  better demonstrator - it would be the first sample whose generator is data - but it costs the declared
  generator language a feature if the DDL needs an ordering the templates cannot state.
- Whether the state machine's second notation is a `table` variant of the same notation or a second notation
  registration. ADR 0012 supports both; the sample should choose whichever reads better in the variant menu.
