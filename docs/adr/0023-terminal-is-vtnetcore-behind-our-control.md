# ADR 0023 — The terminal is VtNetCore's engine behind a control of our own

- Status: Accepted
- Date: 2026-11-21
- Related: [terminal.md](../architecture/terminal.md), [standalone.md](../architecture/standalone.md),
  [ADR 0015](0015-shell-is-a-library.md), [ADR 0022](0022-every-project-is-a-package.md)

## Context

The workbench needs a place to run a program and watch what it prints, and to type answers back into it — the
missing half of "try this document against something real". The suggested component was
[AvalonStudio.TerminalEmulator](https://github.com/VitalElement/AvalonStudio.TerminalEmulator) (MIT).

It is not one component. It is two projects plus a git submodule:

| Part | What it is | State |
| --- | --- | --- |
| `VtNetCore` | the VT100/xterm state machine — parser, screen buffer, attributes, cursor | on NuGet as `VtNetCore` 1.0.30, `netstandard2.0`, no UI dependency |
| `VtNetCore.Avalonia` | a 743-line `TemplatedControl` that paints that screen | only on GitHub |
| `AvalonStudio.Terminals` | process plumbing over `AvalonStudio.winpty.NET` | only on GitHub, native per platform |

Three facts decide this ADR, and each was measured rather than assumed:

1. **The engine is available as a package; the control is not.** `AvalonStudio.TerminalEmulator` and
   `Avalonia.GuideControl` are not on NuGet under any id — the flat-container lookups answer 404 — while
   `VtNetCore` resolves to 1.0.30, published by its author.
2. **The control is Avalonia 0.10 code from April 2021.** Retargeted to `net10.0` and built against Avalonia
   12.1.6 in a scratch folder it fails on three kinds of breakage that are *not* mechanical: `GotFocusEventArgs` and
   `LostFocusEventArgs` became one `FocusChangedEventArgs`, `OnPointerLeave` became `OnPointerExited`,
   `AvaloniaScheduler` (Avalonia 0.10 shipped Reactive) no longer exists, `e.InputModifiers` became
   `e.KeyModifiers` in nine places, `Application.Current.Clipboard` became `TopLevel.Clipboard`, and
   `DrawingContext.PushPreTransform` became `PushTransform`. That is the *first* error page; the drawing code
   (`FormattedText`, `TextLayout`, `Typeface`) had not been reached yet.
3. **A fourth public assembly would break a frozen invariant.** ADR 0019 makes `Core`, `Avalonia` and `Toolkit`
   the three surfaces that expose public API. A terminal package would have to be public to be used from the
   shell, and shipping `VtNetCore` types in a package of ours would make someone else's emulator part of our API.

## Decision

**Take the engine as a package and write the control.** `Projectional.Workbench` references `VtNetCore` and
contains `Terminal/`: a `TerminalSession` (the emulator plus its parser), a `TerminalProcess` (a child process
with redirected streams), a `TerminalView` (the painting, ~300 lines) and the panel that connects them.

Consequences that follow from that shape, not from taste:

- **No new assembly, no new package.** The terminal is shell furniture, not framework API: an embedder of
  `Projectional.Avalonia` must not pay for an emulator. The pack count stays at 33.
- **No pseudo-terminal.** `AvalonStudio.Terminals` is not taken, and neither is `winpty`: with redirected
  streams a child does not echo, does not do line editing and does not colour its output unless asked. The panel
  therefore echoes the visible part of what it sends (`TerminalEcho`), which is a temporary measure with a
  deletion condition rather than a feature.
- **The picture and the assertion share one object.** What a program's bytes become is `TerminalSession`'s
  answer, so it is testable with no window; the control paints that answer and holds no emulation of its own.
- **Attribution.** `VtNetCore` is MIT (Darren R. Starr / Telenor Inpli AS); the package reference is the
  attribution, and nothing of it is copied into this repository.

## Alternatives rejected

| Alternative | Why not |
| --- | --- |
| Vendor the 2021 Avalonia control and port it | 743 lines of another project's UI code, a Reactive dependency it does not need, and a palette that would have to be rewritten to follow our theme. The port is hours of work whose end state is code this repository must still own. |
| A terminal package of our own | A fourth public assembly (ADR 0019) and a package every embedder inherits, for a tool only the app shows. |
| Take `AvalonStudio.Terminals` for a real pty | Native per platform, `netstandard2.0`, and the ConPTY wrapper is a `winpty` build from the same year. A pty is worth doing deliberately, per platform, or not at all. |
| Write the emulator as well | A VT parser is a specification with a hundred traps; the one thing here that is worth taking from someone else. |
