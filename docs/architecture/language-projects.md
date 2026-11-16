# Language projects: a language that lives in a folder

The two definition languages produce models; this turns those models into a language. A directory holding
`structure.mps` and `editor.mps` loads as a `LanguageProject`: a `ConceptRegistry`, a `NotationRegistry`, and the
declarations they were built from.

Status: **delivered** as the core of slice P3 of [language-authoring.md](language-authoring.md). The app-side folder
picker is the remaining piece (see below).

## Why a folder and not a file

A language has aspects, and the aspects are edited at different times by different concerns: what a rule *is* has to
be reviewable without its presentation, and its constraints have to be reviewable without either. MPS solves this with
one model per aspect in one directory, and the shape is taken from there because it is the shape the problem has.

```text
MyRules/
  structure.mps    concepts, slots, enumerations   (Projectional.Structures)
  editor.mps       notation, one per concept      (Projectional.Editors)
```

The folder's name is the language's name. `structure.mps` is required; `editor.mps` is not.

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
| two concepts with one key, an unknown super concept | `ConceptRegistry.Create`, which owns the graph |

**A language without a notation loads.** Every node then draws as a missing-notation cell, which is a state a
language being written passes through; refusing it would make "write the structure first" impossible.

## The loop it closes

`LanguageProjectTests` writes both models into a temporary folder, opens it, creates a document of the declared
language, draws it (`rule Approved` through the *declared* notation), saves it and reads it back byte-identically.
Nothing in that test writes C# for the DSL it exercises - the only C# is the language that defines languages. That is
M2's gate in its smallest complete form.

## What is still missing

| Missing | Why it is not in this slice |
| --- | --- |
| the app's folder picker and a per-shell language set | `WorkbenchLanguage` is a static composition, so a language loaded after startup cannot join it. Making it instance state is the next step, and it is a shell change rather than a language one |
| `language.mps` (name, version, dependencies, extended languages) | P3 proper: it needs the aspect models it names |
| constraints, types, generators as declared aspects | M6 |
| exporting a loaded language back to C# | P6 |

Until the app loads a folder, a language project is usable by any host that calls `LanguageProject.Open` - which is
the point of it living in a library and not in the app.
