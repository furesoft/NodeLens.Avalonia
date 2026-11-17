# Language projects: a language that lives in a folder

The two definition languages produce models; this turns those models into a language. A directory holding
`structure.mps` and `editor.mps` loads as a `LanguageProject`: a `ConceptRegistry`, a `NotationRegistry`, and the
declarations they were built from.

Status: **delivered** as the core of slice P3 of [language-authoring.md](language-authoring.md), including the
app-side folder picker.

## Why a folder and not a file

A language has aspects, and the aspects are edited at different times by different concerns: what a rule *is* has to
be reviewable without its presentation, and its constraints have to be reviewable without either. MPS solves this with
one model per aspect in one directory, and the shape is taken from there because it is the shape the problem has.

```text
MyRules/
  structure.mps    concepts, slots, enumerations   (Projectional.Structures)
  editor.mps       notation, one per concept      (Projectional.Editors)
  types.mps        what a concept evaluates to    (Projectional.TypeRules)
  generator.mps    how a model becomes text       (Projectional.TextGen)
```

The folder's name is the language's name. `structure.mps` is required; `editor.mps`, `types.mps` and `generator.mps`
are not. One `generator.mps` holds **every** generator of the language: a language declares as many text targets as it
has, and they belong together the way the type rules do.

## Loading is best effort, and it does not guess

`LanguageProject.Open(directory, out error)` answers either a usable language or a message naming what is wrong -
never an exception at the moment a user picks a directory. That is the same bargain `PreviewSandbox` and
`DocumentFile.TryLoad` make, and for the same reason: opening something is a user action, and a user action reports.

What it refuses, and why each one is an error rather than a warning:

| Case | Why it fails |
| --- | --- |
| no `structure.mps` | the folder is not a language project; there is nothing to load |
| a property naming an unknown type | the structure lowering's own error, with the concept and slot in it |
| a notation for a concept that was never declared | caught during lowering, which is the earliest moment both halves exist |
| a type rule with no body, an unknown type name, a value declared twice, a switch with no `otherwise` | the type-rule lowering's own errors, at load, where the author is looking |
| two generators with one name, a mapping of a concept the language does not have, a part naming a slot the concept does not have (or naming it as the wrong kind) | the generator lowering's own errors, for the same reason |
| two concepts with one key, an unknown super concept | `ConceptRegistry.Create`, which owns the graph |

**A language without a notation loads.** Every node then draws as a missing-notation cell, which is a state a
language being written passes through; refusing it would make "write the structure first" impossible.
**A language without type rules loads too**, and every node is `UnknownType` - the honest answer when nobody
said what a node evaluates to.

## The loop it closes

`LanguageProjectTests` writes both models into a temporary folder, opens it, creates a document of the declared
language, draws it (`rule Approved` through the *declared* notation), saves it and reads it back byte-identically.
Nothing in that test writes C# for the DSL it exercises - the only C# is the language that defines languages. That is
M2's gate in its smallest complete form.

## In the app

`WorkbenchLanguage` is **per shell** - an instance, not a static table - and it grows at runtime:

- **File ▸ Open language…** picks a folder, loads it, and takes it into the set. The language's own
  `structure.mps`, `editor.mps` and `types.mps` open as documents afterwards, which is where the user edits
them.
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
| constraints as a declared aspect | M6 |
| exporting a loaded language back to C# | P6 |

Until the app loads a folder, a language project is usable by any host that calls `LanguageProject.Open` - which is
the point of it living in a library and not in the app.
