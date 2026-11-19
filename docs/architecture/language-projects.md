# Language projects: a language that lives in a folder

The definition languages produce models; this turns those models into a language. A directory holding a `structure`
folder of definitions loads as a `LanguageProject`: a `ConceptRegistry`, a `NotationRegistry`, and the declarations
they were built from.

Status: **delivered** as the core of slice P3 of [language-authoring.md](language-authoring.md), including the
app-side folder picker. Documents are per definition since the one-root-per-document slice.

## Why a folder and not a file

A language has aspects, and the aspects are edited at different times by different concerns: what a rule *is* has to
be reviewable without its presentation, and its constraints have to be reviewable without either. MPS solves this with
one model per aspect in one directory, and the shape is taken from there because it is the shape the problem has.

```text
MyRules/
  structure/                      concepts and enumerations   (Projectional.Structures)
    Pipeline.Pipeline.mps         one concept
    Pipeline.Stage.mps            one concept
    Pipeline.Outcome.mps          one enumeration
  editor/                         notation, one per concept  (Projectional.Editors)
    Pipeline.Pipeline.mps
    Pipeline.Stage.mps
  types/                          what a concept evaluates to (Projectional.TypeRules)
    Pipeline.Stage.mps            one type rule
    Amount.mps                    one named type
  generator/                      how a model becomes text    (Projectional.TextGen)
    Json.mps                      one generator
  constraints/                    the rules the language states about itself (Projectional.ConstraintRules)
    Pipeline.Stage.mps            one rule
  pipeline.mps                    a document of the language, not part of the definition
```

**One document holds one definition.** A structure document's root *is* a concept or an enumeration, an editor
document's root is one notation, a types document's root is one rule or one named type, a generator document's
root is one generator, and a constraints document's root is one rule. That is MPS' shape - one root per file - and it
is what makes a language reviewable: a diff of one concept is a diff of one file, and a change to the notation of
`Pipeline.Stage` cannot touch `Pipeline.Pipeline` by accident. There is deliberately no container concept
(`Structure.Language`, `Editor.Notations`, `TypeRule.Rules`, `TextGen.Generators` are gone), because a container with
one child would be a second place to say which definitions exist.

The folder's name is the language's name. The `structure` folder is required and must hold at least one document; the
other four are optional.

## The file name is the declaration order

Documents are read in **ordinal order of their file names**, and that order is the declaration order - which decides
what an empty slot is filled with ([language.md](language.md#concept)), how completion ranks, and which concept is
the first rootable one a new document starts with. Naming a definition after its key (`Pipeline.Stage.mps`) keeps the
order readable; a language that wants another order names its files so that they sort that way.

## Loading is best effort, and it does not guess

`LanguageProject.Open(directory, out error)` answers either a usable language or a message naming what is wrong -
never an exception at the moment a user picks a directory. That is the same bargain `PreviewSandbox` and
`DocumentFile.TryLoad` make, and for the same reason: opening something is a user action, and a user action reports.

What it refuses, and why each one is an error rather than a warning:

| Case | Why it fails |
| --- | --- |
| no `structure` folder | the folder is not a language project; there is nothing to load. A leftover `structure.mps` from the old layout is named in the message, with what to do about it |
| a document with no root, or with two | one definition per document is the shape; two definitions in one file is what the layout exists to remove, and silently taking the first would hide the second |
| a root that is not a definition of that aspect | a `Structure.PropertyDecl` at the top of a structure document, or an `Editor.Line` in an editor document, is a file the lowering cannot read |
| a property naming an unknown type | the structure lowering's own error, with the concept and slot in it |
| a notation for a concept that was never declared | caught during lowering, which is the earliest moment both halves exist |
| a type rule with no body, an unknown type name, a value declared twice, a switch with no `otherwise` | the type-rule lowering's own errors, at load, where the author is looking |
| two generators with one name, a mapping of a concept the language does not have, a part naming a slot the concept does not have (or naming it as the wrong kind) | the generator lowering's own errors, for the same reason |
| a rule naming a concept the language does not have, or reading a slot the concept declares as another kind (or not at all), or with no message and no body | the constraint lowering's own errors: a rule that could never fire is the one failure an author cannot see from the outside |
| two concepts with one key, an unknown super concept | `ConceptRegistry.Create`, which owns the graph |

**A language without a notation loads.** Every node then draws as a missing-notation cell, which is a state a
language being written passes through; refusing it would make "write the structure first" impossible.
**A language without type rules loads too**, and every node is `UnknownType` - the honest answer when nobody
said what a node evaluates to. **A language without constraints loads**, and it reports nothing of its own - the
same answer, and the framework's own ambiguity rule is the host's to add rather than the project's.

## The loop it closes

`LanguageProjectTests` writes the documents into a temporary folder, opens it, creates a document of the declared
language, draws it (`rule Approved` through the *declared* notation), saves it and reads it back byte-identically.
Nothing in that test writes C# for the DSL it exercises - the only C# is the language that defines languages. That is
M2's gate in its smallest complete form.

## In the app

`WorkbenchLanguage` is **per shell** - an instance, not a static table - and it grows at runtime:

- **File ▸ Open language…** picks a folder, loads it, and takes it into the set. **Every definition document of the
  folder opens afterwards** - one tab per concept, enumeration, notation, rule, generator and constraint - which is
  where the user edits them.
- **File ▸ New concept**, **File ▸ New enumeration** and **File ▸ New notation** create a document with exactly one
  empty definition of that kind. A definition document is saved into the folder's aspect directory, so a concept
  created in the app is one more file beside the others.
- **File ▸ New document of an opened language** creates a document of the first concept that can be a root: concrete,
  and naming no applicability places. A concept that names places needs a parent and a slot to exist, so it can never
  start a document - the same rule [language.md](language.md#applicability) states.
- **A refused language changes nothing.** `TryAdd` builds both candidate registries first and adopts them only if
  both succeed, because a half-added language would give a document a concept registry that disagrees with the
  notation registry in force - the one state that produces files nobody can open.
- **Growing the set rebuilds it.** `ConceptRegistry` is immutable and takes declarations, so every concept already
  in the set is written down again (`Redeclare`). That path is covered by the parity harness's dump comparison, so a
  re-declaration that loses a flag or an order fails a test rather than a file.

## What is still missing

| Missing | Why it is not in this slice |
| --- | --- |
| `language.mps` (name, version, dependencies, extended languages) | P3 proper: it needs the aspect models it names |
| exporting a loaded language back to C# | delivered: `Projectional.LanguageExport`, see [language-authoring.md](language-authoring.md#export-declared-to-c). A shell entry that writes the files is the workbench's, not the library's |

Until the app loads a folder, a language project is usable by any host that calls `LanguageProject.Open` - which is
the point of it living in a library and not in the app.
