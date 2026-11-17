# Model generation

Status: **planned**, M6. The engine is independent of the rest of that milestone and can land
earlier; the consumer that justifies it is the projected C# base language
([csharp.md](csharp.md)).

This document supersedes the note in [semantics.md](semantics.md#generation) that said
model-to-model generators were excluded before 1.0. No ADR is touched: the exclusion was a
scope decision, and the reason it changes is a consumer that did not exist when it was written.

## Why a second stage

Text generation answers "what does this model look like as text". That is enough for a target
which *is* text - JSON, a configuration file, a C# fragment short enough to be a string
template.

It stops being enough as soon as the target is a language: the projected C# base language has
concepts, slots, references, its own notation and its own checks
([csharp.md](csharp.md#the-subset)). Emitting C# straight from DSL templates would mean

- the target's grammar lives in string fragments, so every construct is written twice - once in
  the target's notation and once in whichever template happens to emit it;
- the target's constraints, checks and types never see what was emitted, because there is no
  model to check;
- nothing between the DSL and the text can be inspected, diffed or tested on its own.

The second stage is the answer: **reduce the document into a model of the target language, then
let the target language's own text generator print it.** The target's grammar exists once, in
the target's concepts and generator; the target's checks apply to what the transformation
produced; and the intermediate model is an ordinary model that can be saved, diffed and
asserted on.

```
DSL document ──ModelGenerator──► target model (its own project) ──TextGenerator──► text
```

Two consumers are planned: the C# base language (M6) and the migration tooling of the same
milestone - a migration *is* a model transformation, so it gets the engine for its output model
and no text at all. Two consumers is also what makes the engine a thing of its own rather than a
corner of the text generator; the earlier exclusion was written when the only consumer was text.

## The two values

| Type | Is | Used by |
| --- | --- | --- |
| `ModelGenerator` | a name, the `ConceptRegistry` it targets, and a `ReductionRegistry` - one rule per source concept | a migration, which wants the model |
| `GeneratorPipeline` | a name, a `ModelGenerator` and the `TextGenerator` that prints its target | a host, which wants text |

They are two types rather than one with an optional emitter because they answer different
questions. A host's Generate menu lists *pipelines*: a menu entry that produced a model nobody
can see would be a command with no outcome. A migration runs the model generator directly, and
a `ModelGenerator` whose target language declares no text generator is then perfectly legal -
which is what makes the target language's generator a separate declaration rather than a field
of the transformation.

`ModelGenerator` mirrors `TextGenerator` deliberately: a name plus the rules, chosen by the
host the way a notation variant is, with nothing about the model changed by the choice. A
language may declare several, for several targets.

## Rules are data

A `ReductionRegistry` answers one `ReductionRule` per source concept, inherited from the super
concepts in declaration order - the same lookup notation, triggers, types and templates use. A
rule is an ordered list of parts, and the set is **closed**:

| Part | Does |
| --- | --- |
| `NewNode(conceptKey)` | creates the rule's output node; exactly one per rule |
| `Set(slot, value)` | writes a target property: a literal, or the source node's value for a property of the same name |
| `Child(slot, targetSlot)` | reduces the single child of a source slot into a target slot |
| `Children(slot, targetSlot)` | reduces every child of a source list into a target list, in document order |
| `Same(slot, targetSlot)` | copies a subtree the target language declares as well, with new identities (see below) |
| `Reference(slot, targetSlot)` | retargets a reference through the reverse map, in the link phase |
| `Conditional(slot, value, then, otherwise)` | chooses a body by comparing a property with a value, exactly as `ConditionalPart` does in a template |
| `Empty` | the rule produces no node - a source concept that lowers to nothing |

Four rules make the vocabulary safe to review:

- **The set is closed.** The engine fails on a part it does not know, the way `CellLayouter`
  fails on an unknown cell and `Generator` fails on an unknown template part. A new part is
  added on purpose, in an ADR-shaped change, not accepted silently.
- **No part computes.** There is no arithmetic, no string concatenation, no index. A value the
  target must contain is either copied from the source or produced by a concept of the target
  language. A transformation that computed would be a programming language with no debugger,
  and the declared form could not express it - which is ADR 0014's parity rule, not a
  preference.
- **There is no delegate escape hatch.** A `Func<...>` per concept would be easier to write and
  would break the parity rule in one stroke: a language author who declares a mapping could no
  longer express what a C# author can, and the other way around.
- **One output node per source node.** Several target nodes are children of that one node. The
  reason is the parent's slot: the output node lands where its source stood, and a single-valued
  target slot cannot take three nodes, so "where did this go" would have no answer. Whether that
  is too strict is open question 86.

`Same` exists for the case the C# base language already sets up: a C# expression is an
`Expressions.Expression` (open question 43), so the DSL's expression subtree passes through
unchanged and only the statements around it are transformed.

**`Same` needs a copy primitive the framework does not have yet.** `Model.InsertCopy` mints new
identities but stays inside one model, and `Model.CopyTo` keeps the original identities because a
preview addresses nodes by id - neither can put a subtree into another project, which is exactly
what a target is. The copy is therefore added to `Core` (a sibling of `InsertCopy` that takes the
target model): new identities, references between copied nodes rebound to the copies, and
references leaving the subtree kept by origin id so the link phase can retarget them. Reusing
`ModelFragment` was rejected: it lives in `Projectional.Editing`, above this layer, so an engine
in `Projectional.Semantics` that used it would invert the dependency diagram. `Same` also
requires the target language to declare a concept for every concept in the subtree - a target
that only shares keys is what a *rule* is for - and the check names the first concept it cannot
find.

## Two phases: build, then link

A reference cannot be written while the nodes are being created, because its target may not
exist yet. So a run has two phases.

1. **Build.** Walk the source in document order; for each node, run its rule: create the target
   node in the target place, write properties, reduce children recursively. `Reference` parts
   record work rather than doing it.
2. **Link.** For every recorded reference, ask the reverse map for the target node generated from
   the source reference's target, and write it. The same walk covers a reference *inside* a copied
   subtree: the copy keeps it by origin id (that is what makes a reference within the copy rebind
   to the copy), so the link phase is what turns an id from the source project into one from the
   target - which is why it walks the whole output rather than only the parts the rules recorded.

A reference whose source target produced nothing (an `Empty` rule) or whose concept is not legal
in the target slot is a **failure that names the source node** - not an unset reference. A
generated model with a dangling reference would be printed and shipped, and "the reference is
missing, look for it yourself" is the wrong answer from a transformation that had every
opportunity to know.

The link phase is also why a transformation is not "just a copy with different concepts": the
order of the two phases is the only thing that makes a reference between two reduced nodes
survivable.

## Ownership and traceability

**The output is its own project.** `Project.Register` asserts id uniqueness, so generating into
the input's project is impossible by construction, and that is the correct answer rather than a
gap ([model.md](model.md#identity)): the same reasoning that made the preview copy take a fresh
`Project` ([ADR 0017](../adr/0017-preview-is-a-discarded-edit.md)). One target model per source
model per generator.

**The input is never mutated and no undo step is created.** A generation is a function, not an
edit, exactly like the preview sandbox: the document's history must not gain a step for asking
what it would look like elsewhere.

**The reverse map is the half that cannot be retrofitted.** Every created node records the node
whose rule made it:

```csharp
readonly record struct GeneratedNode(NodeId Target, NodeId Source);
```

`SourceOf(target)` answers the DSL node; `MadeFrom(source)` answers the target node. The map is
kept in target document order, is a list rather than a dictionary, and is answered by the same
`GenerationResult` that carries the failure - the shape `GeneratedText` already has for text.

**The chain composes, and that is the point.** A text region names a *target* node, and the
reverse map names the *source* node, so "click a generated line, land on what wrote it" keeps
working through two stages. That gesture exists today for text generation; a second stage that
broke it would have made the editor worse at the moment it learned something new.

## Determinism

- The source is walked in document order, and the target is built in that order, so child order
  and root order are the input's.
- No dictionary iteration reaches the output, and no property value comes from the clock, the
  culture, the environment or the file system. Generation stays pure, as text generation is.
- **The printed text is the byte-stable artefact.** The generated *model* is not compared
  byte-identically across runs, because a generated node gets a fresh identity: ids are GUIDs and
  two runs cannot produce the same file. A determinism test therefore compares the printed text
  byte for byte and the model as a normalised dump (concepts, slots, values, child order,
  reference targets by source name) - comparing model files byte-wise would test the GUID
  generator, not the transformation.

## Failure discipline

A run is **all or nothing**. The output project is discarded unless the whole transformation
succeeded, because half a target model is worse than none: it can be printed, it looks like an
answer, and nothing in it says which part is missing.

| Failure | Why it fails rather than degrades |
| --- | --- |
| A source concept with no rule | "nobody said what this becomes" - the same rule as a concept with no template, and the same distinction: an `Empty` rule *is* a decision |
| A value the target's type refuses | the model would throw anyway; the failure names the **source** node, so the message points at what the author wrote, not at a node they never saw |
| A reference that cannot be retargeted | see above: a dangling reference would survive into the output |

**There is no budget and no depth guard, and both are deliberate.** The engine walks the source
once, reduces each source node at most once, and copies at most the subtree a `Same` part names,
so the output is linear in the source and a runaway is not a state this engine can reach.
`Generator.MaxDepth` exists because a template can name itself through `SelfPart`; no part here
can refer to the node being built. Adding a guard "for symmetry" would tell the next reader that
an explosion is possible and make them hunt for the case.

**One known limit comes from property defaults.** The model materialises a declared default at
creation (`name = "unnamed"`), so a rule that says nothing about a defaulted target property
produces the default, not an absent value - the accepted limitation documented for editing
(defaults until `feat/property-defaults`). A target language whose concepts must not invent a
name should declare no default for it.

## The declared form

ADR 0014 rule 2 says a capability that only the code path has does not exist. So a transformation
must be expressible as a model, in its own declared language:

- **`Projectional.ModelGen`**, loaded from `mapping.mps` in a language folder beside
  `structure.mps`, `editor.mps`, `types.mps` and `generator.mps` ([language-projects.md](language-projects.md)).
  A document holds any number of generators; a generator holds one mapping per concept; a mapping
  holds its rule as a list of parts. It is drawn in MPS' shape, like the other definition
  languages.
- **A separate language and a separate file**, not a second root kind in `generator.mps`. A
  template and a transformation share nothing but the word "generator": one emits characters, the
  other creates nodes, slots and references. One document carrying both vocabularies would make
  every mapping document also a template document that declares nothing, and the parity test would
  have to compare two vocabularies in one registry - the same reasoning that split the structure
  language from the editor language.
- **`ModelGenLowering` is the only lowering, and it validates at load**: generator names are
  unique, the source concept exists, the target concept exists in the registry it is lowered
  against and is concrete, every part names a slot of the concept it reads *as the kind it is*
  (a `Child` part on a list slot is a load error), every `Set` value parses against the target
  property's type, and `Same` names only concepts the target declares. That is the earliest
  moment the author is looking at the file, which is the rule the notation registry and
  `TextGenLowering` already follow. What only the run can know - whether the target concept fits
  the slot the rule's output lands in - is checked there and fails with a reason naming the
  source node.
- **The target registry is an argument.** `ModelGenLowering.Lower(document, target)` takes the
  concepts the mapping produces, because a language folder cannot yet name another folder - a
  language project gains dependencies and extended languages in M5. How a `mapping.mps` will name
  its target once it can is open question 88.
- **Parity is executed as a test**: `ModelGenLoweringTests` asserts that a declared mapping and
  its hand-written C# twin produce the same target model for the same source document, compared
  as a normalised dump and never by identity.

## Layers and files

| Concern | Lives in | Because |
| --- | --- | --- |
| `ModelGenerator`, `GeneratorPipeline`, `ReductionRegistry`, `ReductionRule`, the parts | `Projectional.Language/Generation/` | a language package must declare a transformation without referencing the layer that runs it - the rule the notation, types, evaluation and templates already follow |
| the engine, `GenerationResult`, `GeneratedNode` | `Projectional.Semantics/Generation/` | beside `Generator`; it produces a model, which is a semantics-level act, not a model fact |
| `Projectional.ModelGen` | its own package beside `Projectional.TextGen` | a declared language is a package, written against the toolkit ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)) |
| the two-stage command | the host, e.g. `DocumentGenerate` in the shell | the framework returns a model or text; where it goes is the host's decision (open question 12) |

Nothing about generation goes into `Core`: a transformation is not something a model knows about
itself, and `Core` must stay free of the language layer.

## The chain in the shell

- **Generate lists pipelines**, one entry per pipeline the active document's language declares,
  and the output panel keeps showing text rows. A model generator with no emitter is not offered
  there: it has nothing to show in a panel whose rows are lines.
- **Rows still point at DSL nodes.** The panel's row → region → target node → source node walk is
  the composition above; no shell change is needed beyond asking the map for the source.
- **A migration runs a model generator directly** and saves the target model, through the same
  "one transaction, one undo step" discipline an edit has when the host applies it.

## Slices

| # | Delivers | Proves |
| --- | --- | --- |
| 1 | `ModelGenerator`, `ReductionRegistry`, the parts, the engine, the reverse map, the result type, the `Core` copy a `Same` part needs, and a pair of test languages in the test project (a two-concept source, a two-concept target) | a rule creates a target node, reduces children, copies a shared subtree, retargets a reference, fails with the source node's id; two runs print identical bytes |
| 2 | `Projectional.ModelGen`, `mapping.mps`, `ModelGenLowering` with load-time validation, `ModelGenLoweringTests` | the declared form reaches the same target model as its C# twin, and a bad mapping fails at load with a message naming the part |
| 3 | `GeneratorPipeline` and the composed traceability | DSL → target model → text, with a region answering the DSL node |
| 4 | the projected C# base language's generator ([csharp.md](csharp.md)) | a real target with references, precedence and nesting - the consumer that pays for the stage |
| 5 | the migration host: run a transformation alone, save the target model | one engine, two outcomes: text for a host, a model for tooling |

Slices 1 and 2 are self-contained and can land before M6's other work; slice 4 is the one that
cannot be faked, which is why the gate below is written on it.

**Gate:** the payroll ruleset reduces into the C# AST model and prints C# that compiles; a second
run prints the same bytes; a target property the model refuses fails the run naming the DSL node;
a reference between two reduced nodes resolves to the generated counterpart; and a migration
produces a target model that loads, passes its own language's checks and round-trips
byte-identically.

## Testing

| Property | Test |
| --- | --- |
| Determinism | two runs → identical printed text; models equal as a normalised dump |
| Coverage | every node of a corpus document has a rule; a document with a gap fails naming the concept |
| Failure names the source | missing rule, refused value, dangling reference, target concept that does not fit its place - each answered with the DSL node id |
| Traceability | `SourceOf`/`MadeFrom` are inverses on a generated model; a text region walks back to the DSL node |
| Parity | declared mapping vs C# twin, compared as dumps, never by ids |
| Load-time validation | a `mapping.mps` that names an unknown slot, the wrong kind of slot, an illegal target concept or an unparsable value refuses to load |

The corpus matters here as it does for persistence: a transformation is a function over the
shapes real documents have, and a golden file is the only cheap way to notice that a change to a
rule reordered half a generated model.

## Rejected alternatives

| Alternative | Why not |
| --- | --- |
| MPS' imperative generator (rules with conditions, macros, `$COPY_SRC$`) | it is a second programming language to implement, debug and document, and it cannot be declared as data with a closed vocabulary - the same trade the framework refuses for constraints and types |
| A delegate per concept (`DelegateReductionRule`) | the fastest path to a working transformation and the end of the parity rule: a declared mapping could not express what the delegate does |
| Templates that print the target's syntax | the target's grammar lives in strings, the target's checks never run, and every construct is maintained twice |
| Generating into the source project | ids are unique per project, and the generation would become an undoable edit the user did not make |
| Building the output through an `EditorSession` | the session is a view; a transformation runs headless, and a preview of it would then need a second headless path |
| Several output models per source model | nothing needs it yet; one model per source model per generator keeps "which file did this produce" answerable |

## What this does not include

Mapping labels and priorities, multi-phase generation, weaving (generating around a user's own
code), generating a whole project into a tree of models, incremental generation, editing a
generated model in a tab, and importing text back. Each is a question with a consumer, and the
slices above are useful without any of them.

## Open questions

The choices this plan leaves to a later decision are recorded as questions 85-91 in
[open-questions.md](../open-questions.md#model-generation).
