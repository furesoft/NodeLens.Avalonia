# Host application

The M4 gate says a user writes a decision table, "runs it in the preview and generates C# from it —
**both inside the embedded control in a small host application and in the standalone app**, proving the
two front ends share the behaviour". This is that host application, and it is deliberately tiny.

```powershell
dotnet run --project samples/host-app
```

## What it is

One window, one document of the shipped sample language (`Payroll`), and four buttons:

| Button | What it calls |
| --- | --- |
| **Check** | `DocumentFindings.Of` - the language's constraints, the checks' repairs and the framework's ambiguity rule, merged into one `FindingSet` that becomes the control's `Markers` and `Fixes` |
| **Run (amount=1500)** | `DocumentRun.Of` - the interpreter's answer as a headline and its trace as rows |
| **Generate C#** | `DocumentGenerate.Generate` and `.Report` - the generated text, one row per line, each naming the node that wrote it |
| **Break a rule** | adds a rule with no condition, so a finding appears - and disappears again when it is given one |

## What it proves

- **A host needs nothing but published packages.** The `ProjectReference`s are `Projectional.Avalonia`
  (the control and the three entry points), `Projectional.Expressions` and `Projectional.Samples` (the
  language and, since one project per language, its rules too - `Checks/` lives in that assembly). There is
  **no** reference to `Projectional.Workbench`, so nothing here can lean on the standalone editor - a build
  that needed one would not compile.
- **Composing a language is three lines.** `RulesLanguage.Concepts`,
  `RulesLanguage.CreateNotations(...)` and `RulesLanguage.TypingRules`, handed to
  `ProjectionalEditorControl.OpenSession`. Everything else in this folder is window layout.
- **The two front ends share the behaviour by construction**, because they call the same functions rather
  than two implementations that are expected to agree.

## What it deliberately is not

- **Not a workbench.** No tabs, no file menu, no recent files, no language picker. Those are shell
  concerns, and they belong to `Projectional.Workbench` (or to the embedded shell of
  [embedded-shell.md](../../docs/architecture/embedded-shell.md), which is M6).
- **Not a test.** It is compiled by the solution on every build, so it cannot rot, but its behaviour is
  the tested behaviour of the functions it calls - duplicating those assertions here would prove the
  window, not the framework.
- **Not a package.** Like `Projectional.Samples`, it is an example: a host that wants it copies the three
  lines of composition, not the folder.
