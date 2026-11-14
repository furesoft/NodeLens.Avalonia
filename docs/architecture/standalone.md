# Standalone editor

[ADR 0011](../adr/0011-standalone-editor.md) decided that a desktop application ships
alongside the embeddable control and is an ordinary consumer of the public API. This
document describes what that application is, how it is composed, and what it must not
become.

The application is `Projectional.Workbench`. It is the same executable from M1 to 1.0;
what changes is how much of a workbench it is.

## Four jobs, in this order

| Job | Why it matters | Milestone |
| --- | --- | --- |
| **Reference host** | Exercises the public API exactly as a customer would, so integration gaps surface during development | M1 |
| **Try-before-embed** | Someone evaluates the framework by running an editor, not by writing a host | M1 |
| **Daily DSL tool** | Ordinary use of a shipped language: open, edit, validate, generate | M2 |
| **Language workbench** | Authoring languages in their own notation, with live preview | M5 |

The order is a constraint on scope, not just priority: a feature that serves the fourth
job before the first two exist is a premature feature. Every milestone adds to the same
shell.

## What the shell is made of

```text
┌─ Workbench ──────────────────────────────────────────────────────┐
│ menu bar        File · Edit · View · Language · Help             │
│ toolbar         open · save · undo · redo · validate · generate   │
├──────────────┬───────────────────────────────────────────────────┤
│ side panel   │  document tabs                                    │
│              ├───────────────────────────────────────────────────┤
│ model        │                                                   │
│ browser      │  editor pane  (ProjectionalEditorControl)          │
│              │                                                   │
│ language     │                                                   │
│ browser      │                                                   │
├──────────────┴───────────────────────────────────────────────────┤
│ problems · output · find            ln 3, col 12   Rules  UTF-8  │
└──────────────────────────────────────────────────────────────────┘
```

| Part | Responsibility | Appears |
| --- | --- | --- |
| `App` / `Program` | lifetime, service wiring, theme, unhandled exception reporting | M1 |
| `MainWindow` | layout skeleton, menus, toolbar, status bar | M1 |
| `DocumentTab` + `DocumentSet` | open documents, active document, per-document session and undo | M1 |
| `ProjectDriver` | open a project, enumerate models, save, watch files | M1 |
| `CommandRegistry` | named commands with `CanExecute` and a shortcut table | M1 |
| model browser | tree of models and their root elements | M2 |
| language browser | declared languages in the project, open their definitions | M5 |
| problems panel | diagnostics of the active document, jump to the marker | M2 |
| output panel | generator and interpreter results, traces | M4 |
| live preview | split a language definition against a model using it | M5 |

Everything in that table is a plain class with no Avalonia dependency except the
window and the two controls, so the shell's behaviour is testable headlessly. The
window is wiring.

## Composition rules

1. **The app never mutates a model directly.** It goes through `EditorSession` and the
   public command API. A model change from the app that cannot be made through the
   public API is a missing public API, not a reason for a back door
   ([ADR 0011](../adr/0011-standalone-editor.md)).
2. **The shell knows about documents, not trees.** Nothing in the app inspects editor
   cells except the control itself.
3. **Sessions outlive tabs, tabs outlive windows.** A document keeps its session and
   undo history while it is open; closing the document disposes the session. This is
   the disposal rule hosts get wrong most often, so the app demonstrates it.
4. **One writer.** Every model mutation happens on the UI thread. Background work
   (validation, generation) goes through the snapshot API
   ([overview.md](overview.md#threading-model)).
5. **No feature exists only in the app.** If the app can do something a host cannot,
   either the public API grows or the feature is removed. This invariant is the whole
   point — without it the app silently becomes the only supported front end.

## Document lifecycle

```text
closed ──open──▶ loading ──▶ clean ⇄ dirty
                                │  │
                        save ───┘  └─── prompt ──▶ discard ──▶ closed
                                              └──▶ cancel
```

| State | Meaning | Transition |
| --- | --- | --- |
| `loading` | project and model read from disk | success → `clean`; failure → error dialog, back to `closed` |
| `clean` | session exists, no unsaved change | first change → `dirty` |
| `dirty` | change journal has entries since the last save | save → `clean` |
| `prompt` | close requested on a dirty document | save / discard / cancel |

The dirty flag is **derived from the change journal**, not set by a flag the app
maintains, so undo back to the saved state returns to `clean`. A boolean "dirty" flag
that is set on any edit and cleared on save is the classic bug: undo to the original
state still claims the document is modified.

External changes are detected by a file watcher and answered with a prompt ("reload
from disk / keep my version"), never silently — see
[open questions](../open-questions.md#standalone-editor) #23.

## Commands

A command has a name, a shortcut, an `Execute`, and a `CanExecute` that reads the
active document. The menu, the toolbar and the shortcut table all bind to the same
command object, so a command cannot be enabled in one place and dead in another.

| Command | Shortcut | `CanExecute` |
| --- | --- | --- |
| New project / New model | `Ctrl+N` | always |
| Open project / Open model | `Ctrl+O` | always |
| Save / Save all | `Ctrl+S` / `Ctrl+Shift+S` | active document dirty |
| Close document | `Ctrl+W` | a document is open |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` | session reports availability |
| Find | `Ctrl+F` | a document is open |
| Validate project | `F5` | a project is open |
| Generate | `Ctrl+G` | language provides a generator |
| Next / previous problem | `F8` / `Shift+F8` | diagnostics exist |

Routing rule: the **focused session** receives editing commands; application commands
(open, save, close) act on the active document. A key that the session handles must not
also trigger a command, which is why the control reports unhandled keys rather than
swallowing them.

## Command line

| Invocation | Behaviour |
| --- | --- |
| `projectional` | empty shell, open-project prompt |
| `projectional <project-dir>` | open that project, restore its last active document |
| `projectional <model-file>` | open the project containing the model, activate that model |
| `--readonly` | no mutating commands; useful for reviews and generated documentation |
| `--language <name>` | open a scratch document of that language without a project |
| `--new <language>` | create a scratch document with one empty root |

Exit code is `0` on clean exit and non-zero on an unhandled startup error, so the app
can be used from scripts. `--readonly` plus an exit code is what makes it usable in a
review or documentation pipeline.

## Packaging

| Artifact | Form | Milestone |
| --- | --- | --- |
| Developer build | `dotnet run --project src/Projectional.Workbench` | M1 |
| Self-contained single file | `projectional.exe`, no .NET install required | M2 |
| Per-OS installer | `.msi`/`.dmg`/`.AppImage`, file association for `.prj` | M7 |
| Scoop / winget / Homebrew | package manifest | M7 |

Self-contained single file is the M2 target because it is what makes
"try before embedding" work for someone without a .NET SDK. Installers wait for M7
because an installer implies a stable file format and update policy, neither of which
should be frozen before the language format settles.

AOT is validated at M7 like the libraries
([roadmap](../roadmap.md)), and the app is the right place to validate it: it is the
artifact users install, so trim and AOT problems surface there first.

## Testing the app

The shell is tested at two levels:

| Level | What is covered | How |
| --- | --- | --- |
| Headless shell logic | document lifecycle, dirty state, commands and `CanExecute`, command-line parsing, recent files, settings | plain unit tests, no window |
| UI wiring | menus and shortcuts are wired to the right command, tabs bind to sessions | `Avalonia.Headless.XUnit` |

The headless level is the default and covers the majority. A UI harness is introduced
when the shell first has wiring worth asserting on, not in advance.

## Limits of this document

This is a plan, not a specification of current behaviour. At the time of writing
`Projectional.Workbench` is a `WinExe` scaffold whose `MainWindow` shows a centred
placeholder; the layers below it — projection, layout and editing — are implemented
and tested, but the Avalonia control they feed does not exist yet. The shell is
therefore scheduled after the control, in M1
([roadmap](../roadmap.md#m1--projection-and-editing-on-a-built-in-language)).
