# Embedded shell

How the shell ships as a component: a host application places the editor's own shell - tabs,
model list, problems panel, save machinery - inside its own window, states which language it is
for, and its users create and edit models of that language and nothing else.

[standalone.md](standalone.md) describes the shell as an application;
[integration.md](integration.md) describes the embedding contract for the *control*. This
document is the third case, and the one that makes the shell part of a host: the shell as a
library, locked to one language. The decision is
[ADR 0015](../adr/0015-shell-is-a-library.md).

Status: **planned.** The rule binds from now on, because it is cheap while the shell is one
window and expensive once it is a hundred window methods. Slices E1-E3 are scheduled with the
shell work that touches them (M2/M3); the language lock and the embedding sample are M6.

## Two shapes, one content

| Shape | Type | Use |
| --- | --- | --- |
| **Pane** | `ShellView` (a `Control`) | the host docks the shell in its own layout, next to whatever else it shows |
| **Window** | `ShellWindow` (a `Window` around `ShellView`) | the host opens an editor window of its own, optionally modal |
| **Application** | `App` + `Program` in `Projectional.Workbench` | the standalone app: `ShellWindow` with every capability and no language lock |

The content is one type. `ShellWindow` is a wrapper that adds a title, an icon and a close
request; it holds no shell state of its own, so a host switching between a docked pane and a
window does not get two behaviours. The standalone executable keeps `App`/`Program` because an
`Application` is a singleton and belongs to the process owner - which is the host, not the shell
([ADR 0015](../adr/0015-shell-is-a-library.md)).

What the host writes, in full:

```csharp
var shell = new ShellView(new ShellOptions
{
    Language  = RulesLanguage.Registration,       // the same handle AddLanguage<T>() takes
    Storage   = new MyDatabaseStorage(),          // where the models live (host-supplied)
    Dialogs   = new MyDialogs(owner),             // confirmations, file questions
    Title     = "Payroll rules",
    Capabilities = ShellCapabilities.Embedded,    // create, edit, save. Nothing else.
});

hostPane.Children.Add(shell);
```

## What the host configures

`ShellOptions` is the whole configuration, and it is a value: the shell copies it and never
mutates it, so a host may build one options object and give two shells the same one.

| Member | Meaning | Embedded default |
| --- | --- | --- |
| `Language` | the language this shell is for, or `null` for "all registered languages" | required |
| `Storage` | `IShellStorage`: where models are read from and written to | required for saving; `null` means in-memory |
| `Dialogs` | `IShellDialogs`: confirmations and questions | required; `ShellDialogs.Silent` refuses destructive answers instead of guessing |
| `Capabilities` | what the user may do (`ShellCapabilities`) | `ShellCapabilities.Embedded` |
| `Title`, `Icon` | window caption and icon; unused as a pane | language name |
| `InitialModel` | open this model, or start with an empty tab | empty tab |
| `Renderer` | the `ProjectionalEditorControl` factory, so a host can preconfigure palettes or gestures | the stock control |

`Renderer` exists because a host that embeds a shell usually also has an opinion about the
editor's palette and font, and today's `ProjectionalEditorControl` settings are properties a
host sets after construction. Passing a factory keeps that a host decision without the shell
publishing a palette property of its own for every knob.

## What the user may do

`ShellCapabilities` is a small value object (a `[Flags]` set plus a derived check), declared
once per shell and read by the command registry.

| Capability | Grants | Refused means |
| --- | --- | --- |
| `CreateModels` | create a model in the project | no New model command at all |
| `DeleteModels` | delete a model from the project | no Delete entry; the model list has no context menu |
| `EditModels` | mutate content | every document opens read-only, the editor reports `ReadOnly` |
| `Save` | persist through `Storage` | save entries are disabled with the reason "read-only shell" |
| `SwitchProject` | open or switch to another project | the project is fixed for the shell's lifetime |
| `SwitchLanguage` | choose a language for a new or opened model | the language picker does not exist |
| `AuthorLanguages` | language browser and definition editing | no Language menu (M5 features) |
| `Preferences` | theme, font size, gutter, representation | the View menu is absent; the host owns the theme |
| `Generate`, `Run` | the generation and interpretation pipelines | no command until the language has one |

Two presets, and no third:

- **`ShellCapabilities.Embedded`** = `CreateModels | EditModels | Save`. Tabs, model list and
  problems panel are present; the project is fixed, the language is fixed, nothing is
  configurable.
- **`ShellCapabilities.Standalone`** = everything, which is what the app ships with.

### The first slice, delivered in `MainWindow`

`ShellCapability` (a `[Flags]` set, `Projectional.Workbench/Shell/`) exists today, with
`ShellCapabilities.Standalone` (every module) and `ShellCapabilities.Embedded` (create, edit, save,
open, with the outline and the problems) beside the check both are read through. `MainWindow` takes
the set as an internal constructor argument, composes the window from it - a module it does not grant
is not built, so a shell without the project explorer gives the width back rather than leaving an
empty strip - and the command handlers ask the same set before they act, which is the "hiding is
presentation, refusing is the guarantee" rule of ADR 0015 applied without waiting for the registry.

What is **not** there yet, and why: `ShellView`, `ShellWindow`, `IShellStorage`, `IShellDialogs` and
the `CommandRegistry` are M6, and this slice deliberately does not invent them. Two consequences are
worth naming rather than hiding. **Editing is not yet refused** by a capability - `EditModels` hides
the Edit menu, but a host that passes a set without it still gets a control that types, because the
editor control has no read-only state; that is the next seam to add. And the count of enforcement
points is the handlers rather than one registry, so a new command has to remember to ask.
### Capabilities are narrowed by facts, never contradicted by them

A capability states what the *user* may do. It is never the only statement of a fact, and it is
not allowed to disagree with one. Two rules keep that honest:

- **No storage, no saving.** With `Storage` null the shell reports "in memory, not saved
  anywhere" and `Save` is refused regardless of the capability - a host that grants `Save` and
  supplies no storage gets a refusal with a reason, not a crash and not a silent no-op.
- **A lock that is not set cannot be lifted.** `SwitchLanguage` is only meaningful when
  `Language` is null; with a lock in place the capability is not consulted, because the picker
  is never built.

The reason a refusal is visible at all is that the entry is not merely absent: `ShellCommand`
carries a `CanExecute` *and* a `BlockedReason`, which a host building its own menu can show as
a tooltip. Hidden entries are the fallback for capabilities that never exist in that shell; a
disabled entry with a reason is what the user gets for everything else. Both come from one
place, so they cannot disagree.

## The language lock

"The right language is already loaded" has to be true in three places, or it is only a UI
decision:

1. **At construction.** `Language` is required for an embedded shell and must be a registered
   language. An unregistered handle is an `ArgumentException` in the constructor, because a
   shell locked to a language nobody has is a wiring bug, not a user error.
2. **At the project boundary.** The shell builds its `Project` from the locked language's
   `ConceptRegistry`, so a model whose concepts are not in it cannot load normally: the reader
   preserves unknown concepts and reports them ([persistence.md](persistence.md)). A locked
   shell turns that into a refusal: the model is not opened at all, and the user is told which
   model and which concept did not fit. A model that half-loaded is worse than one that did not
   open, because it looks editable.
3. **At creation.** New model creates a model of the locked language without asking, and there
   is no command to create any other. With a lock, `SwitchLanguage` is not offered, so the
   language cannot be reached even accidentally through a dialog.

What disappears from the UI in a locked shell is therefore derived, not listed: no project menu,
no language picker, no language browser, no preferences. The model list shows what the project
can hold, and the project can only hold the locked language - so the list is not filtered, it is
complete.

**A lock is not a sandbox.** A declared language is data ([integration.md](integration.md#language-plugins)),
and a model of a locked language can still contain anything that language allows:
`CreateModels` does not limit *what* a model says, only which language describes it. A host that
needs more (field-level restrictions, approval states) supplies them as a language constraint or
keeps the document read-only; the shell does not grow a permission model per concept.

## Where documents live

Two interfaces, because they answer two different questions:

| Interface | Answers | Standalone implementation | Embedded implementation |
| --- | --- | --- | --- |
| `IShellStorage` | what models exist, and how one is read and written | a project folder, `ModelFile` per model | whatever the host has: a database, a zip, memory |
| `IShellDialogs` | confirm a discard, ask where to save, report a failure | Avalonia dialogs | the host's own, or `ShellDialogs.Silent` |

The shell never calls `File`, `Directory` or `StorageProvider` when a host supplied an
implementation; the file-system pair lives in the workbench as `FileSystemShellStorage` and
`WindowShellDialogs`, and it is exactly the code the standalone app runs today. Splitting it out
is what makes the standalone app the reference *implementation* of the seam rather than the
owner of the behaviour.

Storage members are deliberately few, and asynchronous, and honest about failures:

```csharp
public interface IShellStorage
{
    string DescribeLocation { get; }              // shown in the status bar; may be "in memory"
    ValueTask<IReadOnlyList<ShellModelInfo>> ListAsync(CancellationToken ct);
    ValueTask<string?> ReadAsync(ShellModelInfo model, CancellationToken ct);
    ValueTask WriteAsync(ShellModelInfo model, string xml, CancellationToken ct);
    ValueTask DeleteAsync(ShellModelInfo model, CancellationToken ct);
}
```

XML crosses the seam as a string rather than a `Model`, because the shell is not the only writer
a host may have and a host that stores bytes should not have to own a live model to save one. It
also means the lock can be checked on the way in, before a `Model` exists to be half-built.

`ShellDialogs.Silent` is the default for an embedded shell because the alternative default - a
modal box the host did not ask for - is the classic embedding surprise. Silent refuses
destructive answers: a close of a dirty document returns "cannot close", which the host then
resolves its own way.

## One place where "may not" is true

The command registry is the enforcement point, and it is the only thing the UI may execute
through:

| Consumer | How it gets commands |
| --- | --- |
| the shell's menu bar, toolbar and shortcut table | the registry |
| a host-built menu or toolbar | the same registry, mapped onto its own items |
| the shell's own code | `ExecuteAsync(command)` on the registry, never a handler directly |

A `ShellCommand` is a name, a label, an optional gesture, a `CanExecute` that reads the active
document *and* the capabilities, an optional `BlockedReason`, and an `ExecuteAsync`. The registry
refuses a command whose capability is off even when it is called directly, and it is the same
refusal the menu sees - so a keyboard path, a host menu and the shell's own toolbar cannot
disagree. This is the pattern the editor already uses for its context menu
([editing.md](editing.md), `EditorMenuItem.Execute` refuses a disabled entry), lifted to the
shell.

## Theme, resources and the application

- **The host owns the `Application`.** The shell must not read `Application.Current`, must not
  require `FluentTheme` to be installed by itself, and must not add styles to the application's
  collection when it is loaded as a pane.
- **The shell brings its own resources.** Anything the shell styles lives in `ShellView`'s own
  resources or styles, so no `App.axaml` dependency survives the move out of the window. A
  missing theme degrades to inherited defaults instead of throwing at load.
- **The editor palette follows the resolved variant.** The shell asks for
  `SyntaxTheme.Of(ActualThemeVariant)` for each document, the same call the app makes today, so a
  host with a custom variant gets a matching document without the shell knowing what the variant
  means.
- **Shell preferences are options, not settings.** With `Preferences` off there is no theme,
  font-size or gutter UI at all; a host that wants to offer them passes the values through
  `Renderer` and keeps the UI. The shell never writes a preference file - see below.

## Lifetime

| Concern | Rule |
| --- | --- |
| Who closes what | The shell closes documents; it never closes its parent window, never calls `Shutdown`, never calls `Environment.Exit` |
| Disposal order | shell, then documents, then sessions - a session disposed before its document leaves the tab holding a dead editor |
| Dirty close | the shell asks `IShellDialogs`; with `ShellDialogs.Silent` the answer is "cannot close" and nothing is lost |
| Threading | one writer on the UI thread, background validation through the snapshot API ([overview.md](overview.md#threading-model)) |
| Reuse | a `ShellView` is added to and removed from a host layout any number of times; it holds no `TopLevel` reference after `Detach` |
| Two shells | independent by construction: no static state, no shared registry, no shared storage unless the host hands the same instance to both |

The last row is why **no process-wide settings**. A shell that wrote its recent files and window
position on close would let an embedded instance overwrite the standalone app's history - and a
host that never asked for a settings file would get one. Recent files and last-document state
belong to the host, or to `IShellStorage` when the host has nowhere else to put them.

## What the shell stays out of

| Out of scope | Owner |
| --- | --- |
| installers, updates, file association | the app ([standalone.md](standalone.md#packaging)) |
| global settings, recent files across processes | the host |
| language authoring in an embedded shell | not offered (`AuthorLanguages` is off in the preset); a language is a build artifact for a host |
| a permission model per concept | the language, through constraints ([semantics.md](semantics.md)) |
| telemetry, crash reporting | the host |

## Slices

| Slice | Branch | M | Delivers |
| --- | --- | --- | --- |
| E1 | `feat/shell-commands` | M2 | `ShellCommand` and `ShellCommandRegistry` with capabilities and `BlockedReason`; the app's existing commands routed through it with no behaviour change |
| E2 | `feat/shell-view` | M2 | `ShellView` extracted from `MainWindow`, `ShellWindow` around it, `MainWindow` reduced to wiring; the app behaves identically |
| E3 | `feat/shell-storage` | M3 | `IShellStorage` and `IShellDialogs`, the file-system pair moved out of the app, and `DocumentFile` calling the seam instead of `File` |
| E4 | `feat/shell-language-lock` | M6 | `ShellOptions.Language`, the project guard, creation without a picker, and every capability-driven UI difference |
| E5 | `feat/shell-embedding-sample` | M6 | a sample host that embeds the shell locked to `Projectional.Samples`' rules language, as a pane and as a window |
| E6 | `test/shell-embedding` | M6 | the headless capability matrix, the foreign-model refusal, the two-shells test, and one `Avalonia.Headless.XUnit` window test |

E1-E3 have no user-visible payoff and are therefore attached to milestones that already touch the
shell: writing them first is what keeps the lock from being a filter and the storage from being
`File` calls scattered through a window. E4 is the requirement itself; E5 is the proof, and it is
also the sample a host copies.

## Testing

| Level | What it proves |
| --- | --- |
| Capability matrix | for every command in the registry and every capability combination, the command executes only when its capability is granted - the test enumerates the registry, so a new command cannot be added without appearing in it |
| Foreign model | a locked shell refuses a model of another language, and the message names the model and the concept |
| Completion of the project | a locked shell's model list is exactly the project's models, because a project can only hold the locked language |
| Storage seam | an in-memory `IShellStorage` saves and reloads a model without touching the file system; a shell with null storage refuses `Save` with a reason and reports itself unsaved |
| Two shells | two `ShellView`s in one process edit independently; disposing one leaves the other working |
| No application dependency | a `ShellView` renders inside a window whose `Application` has no shell styles, and after a `Detach` holds no `TopLevel` |
| UI wiring | `Avalonia.Headless.XUnit`: the menu shows exactly the commands of the capability set, and a host-built menu executes through the same registry |

The capability matrix test is the important one: the failure mode this design has is a command
added later that calls its handler directly. A table-driven test over the registry catches it,
and a review comment does not.

## Open questions

Rows 65-68 in [open-questions.md](../open-questions.md#embedded-shell) are the parts of this
document that are choices rather than mechanics.

## What stays in the standalone app

Everything in [standalone.md](standalone.md) that is about being an application rather than being
a shell: `App`/`Program`, the command line, installers and file association, the single-file
build, and the project's own lifetime. The app is then one configuration of the library -
`ShellCapabilities.Standalone`, `FileSystemShellStorage`, `WindowShellDialogs` - and the fact
that it is a configuration is what makes "a host can do what the app does" true by construction
rather than by review.
