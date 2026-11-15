# Scrolling

The editor can already be scrolled: the control keeps an offset, the wheel moves it, the
caret drags it into view, and layout only ever places the cells the viewport can show. What
is missing is the part the user can see and grab - a scrollbar - and the part a host expects
when it embeds an editor in a panel that scrolls.

This document plans that, and it is deliberately short on new machinery: the arithmetic that
scrollbars need already exists in `LayoutViewport`, and Avalonia already ships the widget and
the protocol for a control that scrolls itself.

Status: **planned**, an M1 remainder. See [roadmap.md](../roadmap.md).

## What is delivered

`Projectional.Projection/Viewport/LayoutViewport` is the whole scroll model, and it is
Avalonia-free so it can be tested headlessly.

| Member | What it answers |
| --- | --- |
| `Of(layout, viewport, offsetX, offsetY)` | clamp an offset, then filter the layout to the cells that intersect it |
| `Clamp(layout, viewport, x, y)` | the same clamp without the filter |
| `ScrollableExtent(layout, viewport)` | per axis `max(0, content - viewport)`, i.e. the furthest an offset may go |
| `ScrollIntoView(layout, viewport, x, y, target)` | the offset that brings a rectangle into view |
| `ToViewport(rect)` / `ToLayout(x, y)` | the two coordinate systems, and nothing else converts between them |

`ProjectionalEditorControl` keeps `_offsetX`/`_offsetY`, exposes them as `OffsetX`/`OffsetY`,
clamps them in `ScrollTo` and again in `Render`, follows the caret through `ScrollToCaret`, and
scrolls on the wheel by `WheelLines = 3` lines per notch on both axes. `MeasureOverride`
returns the available size, so the control *is* its viewport, and `ViewportSize` reports
`new(Bounds.Width, Bounds.Height)`.

That is a complete scrolling implementation with no scrollbar. The gaps are exactly three:

1. Nothing is drawn, so the user cannot see that there is more, or how much more, or drag.
2. There are no page keys. `PageUp`/`PageDown`/`Ctrl+Home`/`Ctrl+End` are not routed in M1,
   and without a visible bar there is no other way to move by a screen.
3. The wheel is swallowed unconditionally (`e.Handled = true`), so an editor whose document
   fits still eats the wheel and a surrounding panel never scrolls.

## Decision: implement `ILogicalScrollable`, do not build scrollbars

There are two ways to get a visible scrollbar, and the difference matters.

**Option A - two `ScrollBar` children of the control.** The control adds an `ArrangeOverride`,
reserves a gutter, sets `Minimum`/`Maximum`/`ViewportSize`/`Value` on the bars, listens for
their `ValueChanged`, and draws text clipped to the smaller rectangle. This is what
`ScrollViewer` itself does internally.

**Option B - implement `Avalonia.Controls.Primitives.ILogicalScrollable` and let a
`ScrollViewer` own the bars.**

Option B is chosen. The interface's own documentation states the contract: "Interface
implemented by controls that handle their own scrolling when placed inside a
`ScrollViewer`." That is a literal description of this control.

Why B:

- **The protocol exists for exactly this.** A normal child of a `ScrollViewer` is measured at
  infinity and scrolled by translating a surface as large as its content. This control must
  not work that way: it lays out the whole document once and then *draws only the cells the
  viewport intersects*, which is the reason a large model stays fast. Reporting the content
  size as its own size would make the `ScrollViewer` allocate and arrange a surface as large
  as the document, and the virtualization would be gone. `ILogicalScrollable` is the escape
  hatch: the control reports `Extent`, `Viewport` and `Offset` as numbers, and the
  `ScrollViewer` never has to lay the child out at its extent.
- **It is literally the Avalonia controls.** The bars, their theme, hover, auto-hide, thumb
  dragging, `ScrollBarVisibility` policy and touch panning all come from the toolkit.
- **Option A duplicates a fixed point that is genuinely hard.** Reserving a gutter narrows
  `WrapWidth`, which can make the content taller, which can make the vertical bar necessary,
  which narrows the width again. The naive version flickers, and the version that does not
  flicker has a latch and a second layout pass. A `ScrollViewer` has already paid for that
  (its bars are overlay by default in Fluent, so it does not even shrink the content).
- **Embedding stays honest.** A host that puts the control in its own `ScrollViewer` gets
  working bars with no code. A host that does not keeps today's behaviour exactly, because
  the interface is additive: the control scrolls itself either way.

[projection.md](projection.md) already names this path ("`ILogicalScrollable` is what makes
host scrollbars and smooth kinetic scrolling behave, so it is additive rather than a
rewrite"). This plan is that slice.

### Why not put the `ScrollViewer` inside the control

Tempting, and rejected. A control that wraps itself in a `ScrollViewer` cannot then be placed
in a host's `ScrollViewer` without nesting two scroll coordinate systems, it would force a
gutter on every host that did not ask for one, and it would move a host decision (is this
surface scrollable, and with which visibility) into the editor. The control implements the
protocol; the host decides whether to show bars.

## The mapping

Nothing here is new arithmetic - each property is a number the viewport already computes.

| `IScrollable` / `ILogicalScrollable` member | Value |
| --- | --- |
| `Extent` | `layout.Size` - the laid-out document, in layout units |
| `Viewport` | `Bounds.Size` - what is on screen, i.e. today's `ViewportSize` |
| `Offset` | `new Vector(_offsetX, _offsetY)` |
| `CanHorizontallyScroll` / `CanVerticallyScroll` | `true` (the layout is wider and taller than the viewport when the document says so) |
| `IsLogicalScrollEnabled` | `true` |
| `ScrollSize` | `new Size(measure.IndentWidth, measure.LineHeight)` - one step is one layout unit |
| `PageScrollSize` | `Viewport - ScrollSize`, per axis, floored at one step |
| `ScrollInvalidated` | raised whenever the offset or the extent changes from the editor's side |
| `BringIntoView(control, rect)` | `ScrollIntoView` for that rectangle |
| `GetControlInDirection(...)` | `null` - there are no child controls to navigate to |

Notes that are easy to get wrong:

- **`Extent` is the raw layout size, not `max(layout, viewport)`.** A document shorter than
  the window has `Extent < Viewport`, which makes `Maximum` zero and hides the bar under
  `Auto`. Clamping `Extent` up would erase the difference between "shorter than the window"
  and "exactly the window".
- **The step is a layout unit, not a pixel count.** `MonospaceTextMeasure` gives one line of
  height and `IndentWidth` of width; in the real editor `AvaloniaTextMeasure` gives the font's
  line height. A fixed 16-pixel horizontal step would creep instead of moving a cell.
- **A page keeps one line of overlap.** `Viewport - ScrollSize` rather than `Viewport`, so
  `PageDown` never loses the line the reader was on. This is the one place the plan invents a
  number, and it invents the conventional one.
- **`ScrollInvalidated` is the only notification the `ScrollViewer` gets.** It must be raised
  after an edit changes the document size (the extent moved), after `ScrollTo`, after
  `ScrollToCaret`, after a wheel scroll, and after `Session` is replaced. Missing one leaves
  the thumb stale until the next unrelated invalidation, which reads as "the scrollbar is
  broken".
- **No feedback loop.** The `ScrollViewer` reacts to `ScrollInvalidated` by reading the
  properties back and setting `Offset`; the `Offset` setter calls `ScrollTo`, which clamps and
  invalidates the visual. Do not raise `ScrollInvalidated` from inside the `Offset` setter
  unless the value the `ScrollViewer` would read actually changed - a guarded raise, not an
  unconditional one.

### The one thing to verify first

A `ScrollContentPresenter` arranges a *logical* scrollable at the viewport rectangle, not at
its extent. If it ever arranges the control at its extent, `Bounds` becomes the content size,
`ViewportSize` becomes the document, and the mapping above silently inverts. That is the first
thing to check in the slice that wires it up: put a document larger than the window in a
`ScrollViewer`, read back `control.Bounds.Size` and `session.Layout.Size`, and confirm the
first is the window and the second is the document.

## Wheel and keys

The wheel stays the editor's, and that is a decision rather than an oversight.

- **One scroll path.** Because `OnPointerWheelChanged` marks the event handled, the
  `ScrollViewer`'s own wheel handling never runs. That is what we want: our step is a line of
  the *editor's* layout (so a wheel notch moves exactly the number of lines the notation
  uses), not a fixed pixel amount, and the bars follow through `ScrollInvalidated`.
- **A short document hands the wheel back.** Today the handler marks the event handled
  unconditionally, so an editor showing a document that fits still swallows the wheel and a
  surrounding panel never scrolls. Fix: handle the event only when `ScrollTo` actually moved
  the offset, or when a modifier makes the gesture ours. This is a real embedding bug, not a
  polish item.
- **Shift+wheel is horizontal.** Today only a trackpad's horizontal delta moves X. On a mouse
  with one wheel, horizontal scrolling is otherwise impossible with a bar overlay.
- **Page keys move the caret, they are not scroll commands.** `PageUp`/`PageDown` are routed
  in `EditorKeyMap` as vertical caret moves of one page, and `Ctrl+Home`/`Ctrl+End` as moves to
  the first and last caret place, with the existing `ScrollToCaret` doing the scrolling. A
  page command that scrolled without moving the caret would introduce a second notion of
  "where the user is", and every editing action would have to ask which one is current.
  `Home`/`End` stay cell-wise as they are.
- **The `ScrollViewer` must not steal focus.** Bars are not focusable, but a `ScrollViewer`
  can take focus when clicked. The workbench sets `Focusable = false` on the wrapper so a
  click on the surface keeps the caret where it was.

## Hosts and embedding

- **The workbench wraps each tab's control.** `TabItem.Content` currently *is* the
  `ProjectionalEditorControl`, and three places in `MainWindow` rely on that
  (`tab.Content is ProjectionalEditorControl`). Those checks become one helper that unwraps
  the `ScrollViewer`, so a second shape of tab content does not spread through the file.
- **`ScrollBarVisibility` is the host's.** Default `Auto` on both axes. The control keeps no
  policy property: an embedded editor whose host wants `Disabled` says so on its own
  `ScrollViewer`, and the control keeps scrolling by wheel and by caret either way.
- **Bar colours come from the host theme, not from `SyntaxPalette`.** `SyntaxPalette` describes
  what a *cell's text* looks like; a scrollbar is chrome. A dark host sets
  `RequestedThemeVariant` on the `ScrollViewer`. Do not add a palette entry for it - the
  palette is frozen as a `FrozenDictionary` of text kinds and would gain a kind that is not
  text.
- **Bare hosting still works.** A host that puts the control straight into a `Grid` row keeps
  exactly today's behaviour. That is the compatibility promise of implementing an interface
  instead of adding a wrapper.

## What this plan does not do

| Not doing | Why |
| --- | --- |
| A scrollbar drawn by the editor itself | No minimap and no severity rail: [markers.md](markers.md) leaves both to the shell |
| Sticky table headers while scrolling | Open question 29 in [open-questions.md](../open-questions.md), and it is a layout feature, not a scroll feature |
| Zoom, or a soft-wrap toggle | Open question 42 keeps soft wrap for long text values and horizontal scroll for a grid; a toggle needs a width-dependent layout, which `CellLayouter` deliberately is not |
| Smooth pixel scrolling of the content | The offset is in layout units and the caret math assumes it; kinetic touch panning comes from the `ScrollViewer` for free |
| A scrollbar for the completion list | Its own open question in [completion.md](completion.md) (`VisibleRows` is twelve) |

## Slices

- **S1 - the numbers.** Add `ScrollRange` to `Projectional.Projection/Viewport/`: a
  `readonly record struct` holding `Extent`, `Viewport`, `Offset`, `Step`, `Page`, plus a
  factory `Of(layout, viewport, offsetX, offsetY, measure)` that reuses `LayoutViewport`.
  Headless tests for the step, the page overlap, a document smaller than the viewport, and a
  document exactly the size of the viewport. This exists so the arithmetic is testable: the
  control cannot be constructed in a unit test (its palette needs platform services), so
  anything left inside it is verified only by a probe.
- **S2 - the protocol.** `ProjectionalEditorControl` implements `ILogicalScrollable`, reading
  every property from the `ScrollRange` computed in `Render`/`Refresh`, and raises
  `ScrollInvalidated` from the five places listed above. No behaviour change without a host.
- **S3 - the workbench.** Wrap each tab's control in a `ScrollViewer` with `Auto` visibility,
  replace the three `tab.Content is ProjectionalEditorControl` checks with one unwrapping
  helper, and set `Focusable = false` on the wrapper.
- **S4 - gestures.** Hand the wheel back when nothing moved, add Shift+wheel, route
  `PageUp`/`PageDown`/`Ctrl+Home`/`Ctrl+End` as caret moves, and set `LargeChange` behaviour
  through `PageScrollSize`.
- **S5 - verification.** A probe with a document both taller and wider than the window: assert
  `Bounds` is the window and `Layout.Size` is the document, drag/click the bars and read the
  offset back, press `Ctrl+End` and confirm the offset reached the end, scroll to the end with
  the wheel and confirm the event is no longer handled (so a wrapping panel would get it), and
  rasterise the picture to confirm nothing is painted under a bar.

## Testing

| What | How |
| --- | --- |
| Step, page, extent, clamping | Headless tests on `ScrollRange` in `Projectional.Core.Tests` |
| The interface reports the layout, not a copy | Unit test asserting `Extent`/`Viewport`/`Offset` follow `LayoutViewport` after a session rebuild |
| A short document does not swallow the wheel | Probe: raise the wheel on a control whose document fits, read `args.Handled` |
| Bars move the content | Probe: set `ScrollViewer.Offset`, read `control.OffsetX/OffsetY` and the rendered picture |
| Nothing painted under a bar | Probe: rasterise with `Auto` bars visible and sample the gutter pixels |
| The caret still drags the view | Probe: `Ctrl+End`, read the offset; existing behaviour must survive S2 |

## Open questions

1. **Should the control expose `Extent` as a property?** The interface already does; a host
   that wants to draw its own minimap can read `ILogicalScrollable.Extent` without a new
   member. Recommend: no new public property.
2. **Does a nested `ScrollViewer` (editor inside a scrolling panel) misbehave?** Recommend:
   document that a host should give the editor a bounded row rather than a place inside an
   outer scroll surface; the inner control already clamps its own offset.
3. **Should `Home`/`End` also page?** They stay cell-wise. A `Ctrl+Home` to the document start
   is enough for now.
4. **Do bars need to be reachable by keyboard?** Recommend: no. Focus stays in the editor, and
   the page keys cover the same ground; a focusable bar would break Tab.
