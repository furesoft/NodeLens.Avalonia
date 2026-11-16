# Integration

The editor must be embeddable as a component in other .NET applications. This shapes
the public API more than any internal concern: a host application should be able to
open a model, show it, react to changes and save it — without knowing anything about
projection, concepts or aspects.

## Two audiences

| Audience | What they do | What they need |
| --- | --- | --- |
| **Language authors** (design time) | define a DSL, tune notation, write generators | the workbench, the full language API |
| **Host applications** (runtime) | let their users edit models of a shipped language | a small, stable embedding API |

The runtime surface is the smaller one and must not leak the language API. A host
application that only edits models should never reference `Projectional.Language`.

The framework itself has a third consumer: `Projectional.Workbench` is an application
built *only* on the runtime surface, so it demonstrates that the small API is
sufficient for a real editor. See [standalone.md](standalone.md).

## Embedding

```csharp
using Projectional;
using Projectional.Avalonia;

var project = Project.Open(@"C:\App\Dsl");
var model   = project.Model("payroll");
var session = EditorSession.Open(model, language: "Rules");

var editor = new ProjectionalEditorControl
{
    Session = session,
    ReadOnly = false
};

session.ModelChanged += (_, e) => host.MarkDirty();
session.Diagnostics  += (_, e) => host.ShowErrors(e.Errors);

await project.SaveAsync();
```

### Object model exposed to hosts

| Type | Purpose |
| --- | --- |
| `Project` | open/save, enumerate models and languages, project settings |
| `ModelHandle` | a model: name, language, roots, change events, save |
| `EditorSession` | one model in one editor instance: caret, selection, undo, focus |
| `ProjectionalEditorControl` | Avalonia control bound to a session |
| `INodeView` | read-only navigation for hosts: name, concept, children, properties |
| `Diagnostic` / `DiagnosticEventArgs` | what is wrong and where |

Deliberately absent from the public surface: `Node` mutation internals, change
records, projection cells, aspect registration, layout boxes.

Hosts that need to *read* the model (to act on it) use `INodeView` or the text
generator. Hosts that need to *create* model content programmatically should do it
through a language-provided command, not by mutating nodes — otherwise they anchor on
internals that change between minor versions.

## Hosting modes

| Mode | Description | Requirements |
| --- | --- | --- |
| **Single model** | one model, one editor, embedded in a pane | smallest surface |
| **Full workbench** | tabs, model browser, language project | ships `Projectional.Workbench` as a library or app |
| **Dialog** | modal editor for a small model, e.g. a rule set inside a settings dialog | fast open, single-document undo |
| **Headless** | generation or interpretation without any UI | no Avalonia reference at all |

The first three are the *host* modes: the host owns the window and the editor is a
control inside it. The framework also ships its own host:

| Shipment | Description | Reference |
| --- | --- | --- |
| **Standalone app** | `Projectional.Workbench`, a desktop editor for projects, using only the public API | [standalone.md](standalone.md), [ADR 0011](../adr/0011-standalone-editor.md) |

The standalone app is not a fifth hosting mode; it is a *consumer* of the modes above,
which is why it has no privileged access to the layers. Its existence is the check that
the embedding API is complete: if the app needed an internal, a host would too.

Headless is not an afterthought: it is the same assembly graph minus the UI, and it
is what lets a host use the DSL as a config format. It must remain possible to
reference `Projectional.Core` + `Projectional.Semantics` with no Avalonia packages.

## Language plugins

A language can arrive in two ways, and the host decides:

```csharp
// built-in language from a referenced assembly
services.AddProjectional().AddLanguage<RulesLanguage>();

// declared language from the project directory
services.AddProjectional().AddProject(@"C:\App\Dsl");
```

| Aspect | Built-in plugin | Declared language |
| --- | --- | --- |
| Distribution | NuGet package | files in the project |
| Versioning | assembly version | language version + migrations |
| Trust | same as the host app | data only, cannot execute code |
| Extensibility by users | no | yes |

Neither column replaces the other, and neither stops being supported: behaviour aspects can
only live in a built-in assembly, and a project file cannot ship a package. Both paths are
permanent ([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)), and how a language is
written down on either side is [language-authoring.md](language-authoring.md).

This is the security-relevant distinction: **declared languages are data and cannot
run code.** Interpretation of a model is performed by the language's `Evaluate`
aspect, which lives in a built-in assembly. A user who edits a model cannot introduce
behaviour the language does not already provide. Any future "user-defined logic"
feature must be sandboxed explicitly and is out of scope before 1.0.

## Threading contract for hosts

| Concern | Contract |
| --- | --- |
| Model access | only on the UI thread, or through a snapshot taken inside a transaction |
| Change events | raised on the UI thread, after the transaction commits |
| Diagnostics | raised on the UI thread, batched; may arrive after the edit that caused them |
| Save | async, snapshot-based, atomic; safe to call while the user keeps editing |
| Dispose | a session must be disposed before the control is recycled, otherwise undo state leaks |

Violating the model-access rule is the most likely integration bug, so the debug build
asserts the thread on every mutation.

## Packaging

| Package | Content | Depends on |
| --- | --- | --- |
| `Projectional.Core` | model, persistence, migrations | — |
| `Projectional.Avalonia` | control, rendering, input | Core, Avalonia |
| `Projectional.Toolkit` | language authoring helpers, built-in languages; aggregates `Language`, `Expressions`, `Projection`, `Editing`, `Semantics` | Core |
| `Projectional.Workbench` | workbench app (also usable as a library) | Avalonia, Toolkit |

The projects listed in the [architecture overview](overview.md#projects) are the
compile-time structure; the four packages are the distribution structure. `Toolkit`
exists so that language authors reference one package instead of five, and so that the
internal assemblies are never packaged individually — their API is internal
([ADR 0010](../adr/0010-public-api-surface.md)), so they are implementation detail, not
a supported surface.

`Projectional.Samples` is deliberately not a package: it is a test fixture and the
documentation example, not something a host should ship.

The application has its own distribution channel, separate from the packages:

| Artifact | Form | Milestone |
| --- | --- | --- |
| developer build | `dotnet run --project src/Projectional.Workbench` | M1 |
| self-contained single file | one executable, no .NET install required | M2 |
| installers + file association (`.prj`) | `.msi` / `.dmg` / `.AppImage`, plus winget/Homebrew | M7 |

An installer is deliberately late: it implies a frozen file format and an update
policy, and neither should be settled before the language format is
([standalone.md](standalone.md#packaging)).

- `net10.0` target for all packages; no multi-targeting before 1.0.
- Trimming/AOT: the design is reflection-free ([ADR 0007](../adr/0007-reflection-free-language-definition.md)),
  but AOT support is validated at M7, not promised earlier.
- Public API is validated against a baseline file in CI
  ([ADR 0010](../adr/0010-public-api-surface.md)); accidental breaking changes fail the
  build.

## What a host application must be able to do without reading our source

1. Open a project, list its models, list their languages.
2. Show a model, let the user edit it, undo/redo.
3. Get notified about changes and about problems.
4. Save, and detect that saving is needed.
5. Read the model's structure or its generated text.
6. React to a "generate" or "run" request.

If any of these requires digging into internals, the embedding API is wrong.

The standalone app ([standalone.md](standalone.md)) is the executable proof: it does
exactly these six things, through the public API, with no `InternalsVisibleTo` entry.
Every milestone therefore ends with a check that is not a review — the app either still
builds against the published surface, or the surface was extended on purpose.
