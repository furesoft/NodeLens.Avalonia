# Language descriptions for LLMs

Status: **planned, M8 - after 1.0**. Nothing in this document is implemented; it is the plan of
record for the last milestone on the roadmap, and the reason it is last is in
[roadmap.md](../roadmap.md#m8--language-descriptions-for-llms-after-10).

## The idea

A language is declared data: concepts, slots and cardinalities, property types and enumerations,
applicability, notation, triggers, typing rules, constraints and types. From that alone one can
write a description a language model can read, and then two things become possible:

1. **The model writes a document** in that language, and the framework loads it - refusing what the
   language does not declare instead of guessing, and reporting the findings that a user's file
   would get.
2. **The model works on an existing AST**: it proposes a list of structural operations, the
   framework applies them through the same edit actions a keystroke uses, and the result is one
   undo step that the user can accept, reject or preview.

This is the one thing a projectional editor can do that a text editor cannot do well: hand a model
the *language* rather than 10 000 tokens of example files. Nothing here is guessed from samples,
because there is no sample to read - the declaration is the truth.

It is also the reason this milestone sits after 1.0. The description is only worth shipping once
the language surface is finished (M5's declared aspects), once text import exists (M6), and once
the public API is stable - otherwise the description describes a language that is still moving, and
every consumer has to be rebuilt.

## Two artefacts from one walk

One pass over the language produces both, so they cannot disagree:

- **`LanguageSchema`** - machine-readable, for the generator: concept keys, abstract or concrete,
  super concepts, slots (name, kind, cardinality, target concept or property type), property
  defaults, enumeration symbols, applicability, notation as a description of what the user sees,
  triggers, typing rules, type rules and declared constraints.
- **`LanguageGuide`** - prose assembled from the same walk, for the prompt: "a rule has a name, a
  condition and an outcome; `condition` holds one expression; `outcome` is `Accept` or `Reject`".

Rules that make it usable and testable:

- **Derived, never written by hand.** A hand-maintained guide is a second description of the
  language and drifts the first time a concept changes. Everything the guide says comes from a
  declaration that already exists (the same rule [ADR 0007](../adr/0007-reflection-free-language-definition.md)
  sets for the language itself).
- **Deterministic.** Declaration order everywhere, like the registry dump in the parity harness, so
  two runs of the generator produce identical bytes and the result can live in git and be diffed.
  Never iterate a dictionary or a hash order.
- **Bounded.** A language with five hundred concepts cannot be pasted into a prompt. A
  `DescriptionBudget` truncates by section and says what it left out - the same shape
  `CellBudget` already has for the preview, and the same rule: truncation is visible, never silent.
- **About the language, never about the documents.** The description of `Rules.Rule` is the same
  whether the user has one rule or a thousand. A document is only ever read when the host asks for
  a *fragment* of it, and then it is the host's decision to send it anywhere.
- **No network and no prompt.** The framework produces text; the host calls the model. A framework
  that talked to a provider would put a credential and a rate limit inside a language editor.

## Generating a document

The model writes the interchange format, and the framework loads it through the path a user's file
takes, so a generated document and a hand-written one cannot diverge:

- **Format.** The existing `.mps` XML for a whole document. It is stable, it round-trips
  byte-identically, and the reader already refuses what it cannot build. A fragment of a document
  uses the shape the structural clipboard introduces; **no third format** - a JSON dialect would be
  a second wire format to keep in step with the first, which is the mistake the risk register
  already names for import.
- **Validation is the answer.** A generated document is a candidate, not a fact: the loader reports
  what it refused (unknown concept, unknown slot, a value the type rejects) and the language's
  checks report what is wrong with what it accepted. Nothing is repaired silently - "never drop
  data" applies to a model the user wrote, and a model's output is not treated better.
- **The repair loop is the host's.** Feed the findings back, ask again, stop when the user accepts.
  The framework has no loop of its own, for the same reason it has no network.

## Working on the AST

A proposal is a list of operations over **paths**, not over ids: a node id is internal, and a model
cannot know one. A path is `root index / slot / child index`, for example `0/rules/2/condition` -
stable within one proposal, resolved against the live model when the proposal is applied.

```
{ "op": "insert",    "path": "0/rules", "index": 2, "concept": "Rules.Rule" }
{ "op": "set",       "path": "0/rules/2/name", "value": "VAT" }
{ "op": "reference", "path": "0/rules/2/condition", "name": "amount" }
{ "op": "wrap",      "path": "0/rules/2", "concept": "Rules.Group", "slot": "rules" }
{ "op": "move",      "path": "0/rules/0", "index": 3 }
{ "op": "delete",    "path": "0/rules/1" }
```

Three rules make that safe:

- **Every operation is an existing `EditAction`.** The protocol is a serialisation of the editing
  layer, not a second one: `InsertChildAction`, `SetPropertyAction`, `TypeTextAction` for a
  reference name, `WrapInAction`, `MoveNodeAction`, `DeleteNodeAction`. A new operation type is a
  new action first.
- **A proposal is one transaction and one undo step.** A list that cannot be applied in full is
  rolled back whole, exactly like a multi-step `EditProgram` - a half-applied proposal is worse than
  a refused one.
- **A refusal carries a reason**, and the reason goes back to the model: a path that does not
  resolve, a slot the concept does not declare, a concept that may not stand there, a value its
  type rejects. An unresolvable path is the ordinary case, not an error.

**Previewing a proposal is not a new feature.** The intention preview already applies an edit to a
discarded copy in its own project and projects the difference
([ADR 0017](../adr/0017-preview-is-a-discarded-edit.md)); a proposal is a program of such edits, so
the ghost the user sees before accepting is the same mechanism.

## Where it lives

The description is produced from declarations that sit in two layers: concepts, notation, types and
triggers in `Projectional.Language`, constraints in `Projectional.Semantics`. It therefore cannot be
part of the toolkit - ADR 0019 deliberately keeps `Semantics` out of the language surface - so it
ships as **its own package** (`Projectional.Descriptions`), the way the check catalogue and the
intention catalogue do: a behaviour package that references what it describes. The operation protocol
lives beside it, because it references the edit actions.

What that package must **not** do is invent a second read model of the language. If a description
needs a fact the declarations do not carry, the declaration is what grows.

## What this is not

| Not planned | Why |
| --- | --- |
| The model authoring a *language* | M5 keeps a human in the loop for that; a generated language with no reviewer is a generated language nobody can maintain |
| Auto-repair of a refused document | the repair is a decision, and a tool that guesses it hides what it changed |
| A prompt library or embedding store | the framework produces a description, not a conversation |
| Sending documents to a provider | privacy is the host's decision, and the framework has no credential to leak |
| A model in CI | a non-deterministic answer is not a test; what CI tests is the generator, byte for byte |

## Testing and the gate

- **The generator, deterministically**: golden description files for the test language and for the
  workbench's own languages, compared byte for byte, plus a budget test that proves truncation is
  reported. These are the tests CI can run.
- **The protocol, headlessly**: an operation list applies as one undo step, a list that fails on its
  last operation leaves nothing behind, and each refusal names why.
- **The round trip**: a document generated from the guide alone loads, passes the language's checks
  and saves back byte-identically.
- **The gate** (in [roadmap.md](../roadmap.md#m8--language-descriptions-for-llms-after-10)): two
  real languages, one generated document each, both accepted; the operation protocol applied to one
  of them; the description diffable in git. Whether a model *likes* the description is measured in
  the usability week by hand, not in CI.
