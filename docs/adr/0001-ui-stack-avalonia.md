# ADR 0001 — Avalonia as the UI stack

- Status: Accepted
- Date: 2026-11-14
- Related: [ADR 0009](0009-custom-drawn-editor-control.md), [integration.md](../architecture/integration.md)

## Context

The editor is a component that other applications embed. That rules out anything tied
to one application shell, and it makes cross-platform reach valuable even if the first
consumer is Windows-only.

The control has unusually deep requirements for a UI framework:

- custom drawing with direct access to text shaping and measurement
- sub-pixel-precise hit testing on text runs we position ourselves
- a scroll model we control (virtualization, caret scroll-into-view)
- keyboard, IME and clipboard plumbing at a low level
- a component model that works when hosted inside someone else's window

Candidates considered: Avalonia, WPF, WinUI 3, Blazor/WebAssembly, MAUI, and a
headless core with several UI adapters.

## Decision

Use **Avalonia** (currently 12.1.6) as the rendering and input stack, with the editor
implemented as a custom `Control`. All projection logic stays UI-free so that a second
front end remains possible, but Avalonia is the only front end built before 1.0.

## Consequences

### Positive

- Cross-platform (Windows, Linux, macOS) without extra work, which matters for an
  open-source component's audience.
- `Avalonia.Media.TextFormatting` exposes `TextLayout`/`TextShaper`, which is exactly
  what a projectional editor needs: shape a run, measure it, get its geometry, draw it.
- MIT-licensed, permissive, commercially usable, and not tied to a platform vendor.
- Real embedding story: an Avalonia `Control` drops into Avalonia hosts trivially and
  into WPF/WinForms hosts via host controls.

### Negative / costs

- Smaller ecosystem and fewer answered questions than WPF; some low-level APIs are
  less battle-tested (notably IME plumbing and accessibility peers).
- Avalonia's major version cadence can carry breaking changes; the component must pin
  its range deliberately.
- WPF has the more mature text stack; choosing Avalonia means accepting a somewhat
  higher risk in exactly the area that matters most for this project.

### Mitigation

- Projection and input routing are UI-free, so swapping or adding a front end does not
  touch the core.
- The custom-layout spike is the first task of M1 — the risk is retired early, not at
  the end.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **WPF** | Windows-only, effectively legacy for new open-source work; would cap the audience and the embedding story. |
| **WinUI 3** | Windows-only, weaker text layout control, harder to host as a reusable component. |
| **Blazor / WebAssembly** | Rendering a structural editor through the DOM fights the browser's text model; keystroke latency and IME control are worse; embedding into desktop apps becomes a webview problem. |
| **MAUI** | Targets mobile form factors the projection model does not support (no keyboard/caret assumptions); weakest text APIs of the set. |
| **Headless core + multiple adapters** | Architecturally cleanest, but building two front ends as a solo developer guarantees neither is finished. The layering keeps the option open at no cost. |
