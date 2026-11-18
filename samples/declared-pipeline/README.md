# A language defined entirely in project files

This folder is the M2 gate made concrete: a small DSL that exists only as data. There is no C# for it
anywhere in the repository, so "deleting the language's C# code changes nothing about what the user
sees" is not a claim to check - there is nothing to delete.

| File | What it is |
| --- | --- |
| `structure/` | the language: one document per concept, with its slots and defaults |
| `editor/` | how those concepts are drawn - one document per notation, a header line and a table |
| `constraints/` | the rules the language states about itself - one document per rule |
| `pipeline.mps` | a document of the language, so the folder is something to open |

One definition per document, the way MPS stores a model: `structure/Pipeline.Pipeline.mps` holds the
pipeline concept, `structure/Pipeline.Stage.mps` the stage concept, and the same two keys name the
notation documents in `editor/`. The file name is also the declaration order, so the names are the
concept keys.

## Open it

In the standalone editor: **File ▸ Open language...**, choose this folder, then **File ▸ New document
of an opened language**. The pipeline opens as a table; the columns can be dragged wider, and the
`+ stage` row adds a row when the table is empty.

From code:

```csharp
var language = LanguageProject.Open("samples/declared-pipeline", out var error);
var model = ModelFile.Load(language!.NewProject(), "samples/declared-pipeline/pipeline.mps");
```

## What it declares

`Pipeline.Pipeline` is the root: a `name` and many `stages`. `Pipeline.Stage` is one step: a `name`, a
`command` and a `retries` count. The notation draws the pipeline as a header line followed by a table
over `stages`, and a stage on its own as one line - so the same concept has a table form and a text
form, which is what makes the table a *variant* rather than a special document mode.

`constraints/stage-name.mps` and `constraints/stage-command.mps` are rules, not code: the first is a
hint while a stage still carries the placeholder its concept declares for a name, the second a warning
about whitespace around a command. Both are lowered into the same `IConstraint` a language written in
C# writes, so the language reports its own findings without a line of C# behind either of them.

## Why it is here rather than in the tests

It is what a user opens. The tests copy this folder next to the test assembly and load exactly these
files (`DeclaredSampleLanguageTests`), so the sample cannot drift from what the code accepts without a
test failing.
