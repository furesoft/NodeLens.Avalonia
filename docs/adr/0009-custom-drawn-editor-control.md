# ADR 0009 — Custom-drawn control with a structural caret

- Status: Accepted
- Date: 2026-11-14
- Related: [projection.md](../architecture/projection.md), [ADR 0001](0001-ui-stack-avalonia.md)

## Context

Given a model-first document ([ADR 0002](0002-model-as-source-of-truth.md)), the visible
representation still has to be editable. There are two implementation families:

1. **Compose platform controls**: build each line/cell from `TextBlock`s, `TextBox`es,
   `Grid`s, and let the UI framework handle input, caret and scrolling.
2. **Draw it yourself**: one custom control that measures and draws text runs, does its
   own hit testing, and owns the caret.

Family 1 looks easier and is a trap here: the platform's caret is a character offset
into a text control, but this document has no character offsets. Awkward consequences
follow immediately: selection across cells, arrows that must jump over non-editable
keywords, "the caret between two cells", undo grouping, and caret restoration after a
relayout. Virtualization also suffers, because a 1000-line model becomes thousands of
controls.

## Decision

One **custom `Control`** that:

- measures and draws text runs itself (`TextLayout`/`TextShaper`)
- performs its own hit testing against the layout box tree
- owns the caret as a **structural address** (`NodeId`, role, index, offset-in-cell)
- implements logical scrolling and viewport virtualization
- draws selection, carets and diagnostics from the same layout data that produced the
  glyphs

No per-cell platform controls are instantiated for rendering.

## Consequences

### Positive

- The caret model matches the document model exactly; a whole class of "caret in the
  wrong place" bugs cannot occur.
- Virtualization is straightforward: nothing exists outside the viewport.
- Selection, markers and carets cannot drift from the text, because both come from one
  measurement pass.
- Rendering cost is dominated by glyph shaping, not by control tree overhead.

### Negative / costs

- We re-implement what text controls give for free: mouse selection, drag, double-click
  word selection, autoscroll, IME hooks, accessibility tree, clipboard, and scrolling
  semantics. This is a substantial, permanent maintenance surface.
- IME (`TextInputMethodClient`) and accessibility (`AutomationPeer`) are the two most
  underestimated items; both are scheduled explicitly in M7, not "when needed".
- Debugging layout requires our own tooling (the headless ASCII snapshot helps but is
  not a substitute for a visual overlay).

### Rules

1. The layout box tree is the single source of truth for geometry; no ad-hoc measuring
   in the input layer.
2. All hit testing goes through one function; no component computes pixel positions
   independently.
3. Any new interaction (drag, double-click) is implemented in the UI-free editing layer
   as an action first, then bound to input in the control.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Per-cell `TextBox`es** | Caret/selection semantics do not map onto a structural document; control explosion kills virtualization. |
| **`RichTextBox`/flow document with embedded elements** | Inherits a text-based editing model and its caret; fighting it costs more than writing our own. |
| **Web view with a DOM-based renderer** | Same mismatch, plus a second language runtime boundary inside the process. |
| **Headless layout + thin generic view (e.g. `ItemsRepeater` of drawing elements)** | Reasonable middle ground for text-only lines, but loses per-run hit testing precision inside a line. Revisit only if the custom control becomes a bottleneck. |
