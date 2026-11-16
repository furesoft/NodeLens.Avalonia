# ADR 0015 — The shell is a library, and a language can be locked to it

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR 0011](0011-standalone-editor.md), [ADR 0010](0010-public-api-surface.md),
  [standalone.md](../architecture/standalone.md),
  [embedded-shell.md](../architecture/embedded-shell.md)

## Context

[ADR 0011](0011-standalone-editor.md) decided that a standalone app ships and is a plain
consumer of the public API, and [standalone.md](../architecture/standalone.md) describes it as a
`MainWindow` with a menu bar, tabs, a model browser, a problems panel and a status bar.

A host application is expected to want that whole shell, not only the editor control: an
application that stores models of one DSL wants tabs, a model list, a problems panel and the
save machinery handed to it, with the language already loaded so its users never see a
language picker and can only produce models of that one language. Today that is not possible in
principle, for three reasons that are structural rather than accidental:

1. **A `Window` is not content.** The shell's layout lives in `MainWindow`, so nothing can be
   placed inside a host's pane. An embedded shell has to be a `Control`.
2. **An `Application` is a singleton.** The shell currently gets its theme from `App.axaml`; a
   host has its own `Application`, and two cannot coexist in one process.
3. **The shell has no notion of "may not".** Commands are window methods whose availability is
   decided where they are used, so "this shell can only create and edit models of language X" has
   no single place to be true.

Retrofitting those three is a rewrite of exactly the code that is easiest to write the wrong
way once (window-shaped, singleton-shaped, UI-gated). So the decision is taken now, while the
shell is one window.

## Decision

**The shell is a library that a host composes, and it can be locked to one language.**

| Rule | What it means |
| --- | --- |
| **Content, then window** | The shell's UI is `ShellView`, a `Control`. `ShellWindow` is a thin window that hosts it, and the standalone executable becomes `ShellWindow` with the full capability set. `App`/`Program` stay in the standalone app and own the `Application`. |
| **One enforcement point** | `ShellCapabilities` is declared once per shell instance and consulted by the shell's `CommandRegistry`, which is the only thing the menu, the toolbar, the shortcut table and any host-built menu may execute through. Hiding an entry is presentation; the command refusing is the guarantee. |
| **A lock is a project constraint, not a filter** | A locked shell builds its `Project` from the locked language's `ConceptRegistry` and refuses to open a project whose models do not all resolve in it, naming the first offending model. It never opens a project and hides what does not fit. |
| **The environment is supplied** | Storage and confirmation dialogs arrive as interfaces (`IShellStorage`, `IShellDialogs`) with file-system implementations used by the standalone app. The shell itself calls neither a dialog nor `File`, so a host may keep its documents in a database, in memory, or behind its own save flow. |
| **No process-wide state** | No statics holding documents, preferences or capabilities, no `Application.Current` lookup, no global settings file. Two shells in one process are independent, and an embedded shell cannot overwrite the standalone app's recent files. |

## Consequences

### Positive

- A host that ships a DSL gets the whole editing experience - tabs, model list, problems,
  save - by placing one control and setting one language, instead of reimplementing the shell.
  That is the requirement this ADR exists for.
- "No feature exists only in the app" ([standalone.md](../architecture/standalone.md#composition-rules))
  becomes a shipped fact rather than an assurance: the app is a configuration of the library, so
  the code path a host uses is the code path that ships.
- The capability set gives the standalone app's own `--readonly` flag, and any future
  restricted mode, the same single mechanism.
- The environment seam fixes a real embedding hazard before it exists: a shell that wrote its
  own settings on close would let an embedded instance edit the standalone app's state.

### Negative / costs

- The workbench's public surface grows: `ShellView`, `ShellWindow`, `ShellOptions`,
  `ShellCapabilities`, `IShellStorage`, `IShellDialogs`, `ShellCommand`. Every one of them is a
  promise under [ADR 0010](0010-public-api-surface.md), and the API baseline test will hold us
  to it.
- Every command has to be written against the registry instead of being a window method. That is
  a discipline which decays quietly: a new command that calls its handler directly compiles,
  works in the app, and silently bypasses the lock. The test in
  [embedded-shell.md](../architecture/embedded-shell.md#testing) exists for that reason.
- Two lifetimes meet in one process, so disposal order matters more than it did: shell, then
  documents, then sessions, and the shell must never close the host's window.
- Extracting the shell from `MainWindow` is a real refactor with no user-visible payoff on its
  own, done at the milestone that can afford it.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Keep the shell an application and have hosts launch it as a separate process** | No shared model state, no shared undo, an extra process to install and supervise, and the host cannot place the editor in its own layout. That is an integration, not embedding. |
| **Give hosts only the editor control (today's surface)** | Forces every host to rebuild tabs, model list, problems panel and dirty prompting, and guarantees that those rebuilt versions differ and regress separately. |
| **Enforce the language lock by hiding commands and filtering the model list** | A hidden entry is not a constraint: the command still exists, and the model list is one view of a project that still holds the model. The lock has to hold at the project boundary. |
| **Ship a second, embedded-only package** | Two shells with one name: the drift between them would be found by users. One package, capability sets, is the cheaper arrangement. |

## Rules

1. The shell may use only `Projectional.Avalonia`, `Core` and its own types - never an internal
   assembly ([ADR 0011](0011-standalone-editor.md), [ADR 0010](0010-public-api-surface.md)).
2. A capability check that exists only in the UI is a bug, not a shortcut.
3. A locked shell must refuse a foreign model by name; it must never drop or hide it.
4. Anything the shell persists goes through `IShellStorage`; anything it asks the user goes
   through `IShellDialogs`.
