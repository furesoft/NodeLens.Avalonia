# ADR 0011 — A standalone editor is a first-class consumer of the public API

- Status: Accepted
- Date: 2026-11-14
- Related: [0009](0009-custom-drawn-editor-control.md), [0010](0010-public-api-surface.md),
  [0011](../architecture/standalone.md)

## Context

The editor is a component first: a host application embeds
`ProjectionalEditorControl` in a pane of its own UI. Every design decision so far
optimises for that embedding — small public surface, no window management, no file
dialogs, no application shell inside the libraries.

A framework whose only consumer is a hypothetical host application has a problem that
is easy to underestimate: **nobody exercises the public API the way a real application
does.** The integration surface stays untested against the awkward cases — open a
project from a path, switch between two models in tabs, prompt on a dirty close,
reload a file changed on disk, keep the undo stack alive across focus changes. Those
are exactly the cases a host will hit first, and each one is discovered as a bug
report rather than during development.

There is also the authoring side. The workbench in M5 is where languages are defined
by their own notation, which is the point of a projectional editor. Reaching that
through a throwaway harness that is thrown away at M5 means building the shell twice.

The question is therefore not *whether* there is a shell, but **what privilege the
shell has**, and **when it exists**.

## Decision

The repository ships a **standalone desktop editor**, `Projectional.Workbench`, and it
is treated as **an ordinary host application**:

| Rule | Consequence |
| --- | --- |
| It references only public packages — `Projectional.Avalonia` (which brings `Core`), plus `Projectional.Toolkit` when it grows language-authoring UI | no reference to an internal assembly |
| It has **no** `InternalsVisibleTo` entry | internals are as invisible to it as to any customer |
| The control libraries must never reference it | dependency arrows keep pointing at `Core` |
| It starts thin in M1 and grows into the workbench at M5 | one shell, built twice in scope, not twice in code |
| Every app-only feature is a public-API gap | the fix is widening the API, not granting the app access |

`Projectional.Workbench` is an app (`WinExe`) and a library at the same time: the
`Program`/`App` pair is the entry point, but the document model, the command registry
and the shell services are plain types a second front end could reuse.

### What "thin" means at M1

The first version is deliberately unambitious, because its job at M1 is to be a real
consumer of the API, not a product:

```text
┌─ Projectional Workbench ──────────────────────────────┐
│ File   Edit   View                                    │
├───────────────────────────────────────────────────────┤
│ [payroll.rules] [discount.rules] [+]                  │
├───────────────────────────────────────────────────────┤
│                                                       │
│   ruleset Payroll {                                   │
│     ▸ rule Approved when amount > 1000 then Accept    │
│   }                                                   │
│                                                       │
├───────────────────────────────────────────────────────┤
│ 2 problems   ln 3, col 12                Rules        │
└───────────────────────────────────────────────────────┘
```

Open a project, list its models, open a model in a tab, edit it, save, close with a
dirty prompt. Nothing else. Each of those steps is a public-API consumer, and each is
a scenario the integration tests can replay headlessly.

### What the app owns, and what it does not

| Concern | Owner |
| --- | --- |
| Model, caret, undo, diagnostics | the libraries |
| Rendering and input of one model | `ProjectionalEditorControl` |
| Windows, tabs, menus, dialogs, recent files, settings, file watching | the app |
| Language semantics, validation | the libraries |

The app is allowed to be *opinionated* where the library must not be: a library cannot
decide that closing a dirty tab prompts, that a project is a directory, or that
double-clicking a `.prj` file opens it. Those opinions live in the app and reach the
control only through its public interface.

## Consequences

### Positive

- The public API has a demanding, permanent first customer. Every gap becomes visible
  during development instead of after release.
- The embedding scenarios are executable the day the control exists: they are the
  app's own scenarios.
- The M5 workbench has a shell to grow into, so the shell is written once.
- A user who does not want to write a host application still gets a usable editor,
  which is what makes the framework adoptable in the first place.
- Distribution has something to point at. "Install this and try it" is a far lower
  barrier than "write an Avalonia app and reference two packages".

### Negative / costs

- **The app is a real product surface**, with installer, icons, settings and bug
  reports of its own. It costs time that could go into the libraries.
- There is a permanent temptation to solve an app problem by adding an internal
  escape hatch. The rule in the table above exists to make that temptation visible in
  review rather than invisible in code.
- Two front ends (app and host embedding) means two sets of usability feedback, which
  can pull the control in different directions.
- The app must be tested, and UI tests are the most expensive kind. Mitigated by
  keeping shell logic (documents, commands, dirty state) in plain classes tested
  headlessly, so only wiring needs a UI harness.

### Open follow-up

Which shell patterns the app uses (MVVM toolkit versus code-behind), where settings
live, whether it auto-updates, and how it reacts to external file changes are recorded
as open questions with decide-by milestones in
[open-questions.md](../open-questions.md#standalone-editor).

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| **Standalone app only at M5** | The first real consumer of the public API would appear four milestones after the API is defined, so every integration defect accumulates unnoted. Also writes the shell twice. |
| **Shell only through the library** — put window and document management into a library the app merely wires up | The library would grow opinions it cannot have in an embedding host (dirty prompts, project-as-directory, file watching). Hosts would have to opt out of behaviour they never asked for. |
| **App with `InternalsVisibleTo`** | It would pass its own tests while proving nothing about the public API, which is the one thing its existence is meant to test. |
| **No standalone app; rely on samples** | A sample is not maintained, is not installed, and is not tried by anyone who is not already convinced. The "try it before embedding it" path disappears. |
| **Standalone app as a separate repository** | The app is the API's reference consumer; separating it lets the API and its only customer drift between releases. |
