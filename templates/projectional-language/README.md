# MyLanguage

A projectional language: concepts, notation, a sample model and the two tests a notation is worth.

## What is here

| File | What it is |
| --- | --- |
| `MyLanguageConcepts.cs` | the concepts and their slots - *what the language is* |
| `MyLanguageNotations.cs` | how each concept is drawn - *what it looks like* |
| `MyLanguageSample.cs` | a small document, so a first run and every test have something to show |
| `MyLanguage.Tests/NotationTests.cs` | the drawing and the byte-stable round trip |

The split is deliberate: a definition that mixes domain and presentation cannot be reviewed, and a notation that
lives apart from its concepts can be replaced without touching them.

## Reference

One package. `Projectional.Toolkit` brings the model, the declaration builders and `LanguageSnapshot`, which draws the
model as text so a notation can be asserted on:

```csharp
var drawing = LanguageSnapshot.Render(model, MyLanguageNotations.Registry, width: 40);
```

Nothing here references the editor, and it must stay that way: a language that reaches into `Projectional.Editing`
stops being usable headlessly, and tests of an editor are slower and weaker than tests of a drawing.

## Verify

```powershell
dotnet test
```

## Next

- Every concept needs a notation. A missing one renders as a missing-notation cell rather than throwing.
- Add a golden corpus: save a document, commit the `.mps` file, and assert that loading and saving it again produces
  the same bytes. That test is the wire format's contract.
- A language that needs behaviour - a check, an intention, a typing rule - adds a `Checks/` or `Intentions/` folder
  and references the behaviour band it uses, which is how the shipped languages are shaped (one project per language,
  ADR 0021). What the template promises is narrower on purpose: the *declaration* of a language needs the toolkit and
  nothing else, and this project keeps it that way so a notation test stays a fast, headless test.