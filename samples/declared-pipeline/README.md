# A language defined entirely in project files

This folder is the M2 gate made concrete: a small DSL that exists only as data. There is no C# for it
anywhere in the repository, so "deleting the language's C# code changes nothing about what the user
sees" is not a claim to check - there is nothing to delete.

| File | What it is |
| --- | --- |
| `structure/` | the language: one document per concept, with its slots and defaults |
| `editor/` | how those concepts are drawn - one document per notation, a header line and a table |
| `constraints/` | the rules the language states about itself - one document per rule |
| `generator/` | the language's one text generator, `ShellScript`, as a model of the generator language |
| `pipeline.mps` | a document of the language, so the folder is something to open |
| `nightly.mps` | a second document, with a stage that has no command |

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

`generator/shell-script.mps` is the language's one text generator, `ShellScript`, and it is a model of
the generator language like everything else here. It writes a pipeline as a shell script - a comment line
with the pipeline's name, then one line per stage - and its second mapping is what shows the part a
template could not say before: `when command is filled` emits the command and otherwise a comment, so
`nightly.mps`, whose `smoke` stage has none, generates `# smoke has no command` rather than an empty
line. Run it in the app with **Generate â–¸ ShellScript**.

## What it takes from the framework

Both concepts **implement `Framework.Named`**, so neither nominates a name slot: the interface supplies
the `name` property and the designation, and the documents keep their own `name` line only to declare
the `unnamed` placeholder the hint in `constraints/` talks about. A reference declared over
`Framework.Named` accepts a pipeline or a stage without the language saying anything else.

The `nameSlot` property is *empty* in both documents rather than gone, and that is the model's shape
rather than a rule: a property can be emptied but not unset, so the writer emits
`<nameSlot></nameSlot>`. It lowers to "no designation of my own", which is what is meant.

## Ship it as C#

The folder is a prototype; `Projectional.LanguageExport` turns it into the code a package is made
of. In the app that is **File ▸ Export language to C#…**: pick the language, then a folder, and the
generated source, its README and the compiled assembly are written there. From code:

```csharp
var language = LanguageProject.Open("samples/declared-pipeline", out _);
var exported = LanguageExporter.Export(language!, "Sample.Pipeline", ExportOutput.Assembly);

// exported.Files:     DeclaredPipelineLanguage.cs and README.md, as text
// exported.Assembly:  DeclaredPipelineLanguage.dll, compiled in-process
```

The generated README names what the export could not write: this language's two `constraints/`
documents are behaviour rather than data, so they stay in C# beside the generated declarations.

## Why it is here rather than in the tests

It is what a user opens. The tests copy this folder next to the test assembly and load exactly these
files (`DeclaredSampleLanguageTests`), so the sample cannot drift from what the code accepts without a
test failing.
