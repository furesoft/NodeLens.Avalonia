# Scrolling

The editor scrolls: the control keeps an offset, the wheel moves it, the caret drags it into
view, layout only places the cells the viewport can show, and a host gets real scrollbars by
wrapping the control in a `ScrollViewer` because the control implements `ILogicalScrollable`.

This document describes that, and it was deliberately short on new machinery: the arithmetic
scrollbars need already existed in `LayoutViewport`, and Avalonia already ships the widget and
the protocol for a control that scrolls itself. The one thing that was genuinely added is the
protocol implementation plus the wheel hand-back described below.

Status: **S1-S5 shipped**, verified against Avalonia 12.1.6 on a document taller and wider
than the window. See [roadmap.md](../roadmap.md).

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
returns the available size, so the control *is* its viewport, and `ViewportSize` reports the
text area - `Bounds` minus the gutter - because a bar's maximum is `Extent - Viewport` and
reporting the whole control would scroll one gutter-width too far.

That is a complete scrolling implementation with no scrollbar. It started with three gaps, and
all three are now closed; the only thing that stays out of scope is a bar the editor draws
itself, which is the decision in the next section rather than a gap:

1. **Nothing was drawn**, so the user could not see that there is more, or how much more, or
   drag. A `ScrollViewer` host now supplies the bars (S2, S3).
2. **There were no page keys.** `PageUp`/`PageDown` move the caret by one page, and
   `Ctrl+Home`/`Ctrl+End` reach the document's ends, with `ScrollToCaret` doing the scrolling
   (S4).
3. **The wheel was swallowed unconditionally**, so an editor whose document fits still ate the
   wheel and a surrounding panel never scrolled. It is handed back when nothing moved, and
   Shift+wheel scrolls horizontally (S4).

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
| `Viewport` | `ViewportSize` - the text area, so the gutter is excluded |
| `Offset` | `new Vector(_offsetX, _offsetY)`, clamped by `LayoutViewport.Clamp` |
| `CanHorizontallyScroll` / `CanVerticallyScroll` | `true` (the layout is wider and taller than the viewport when the document says so) |
| `IsLogicalScrollEnabled` | `true` |
| `ScrollSize` | `new Size(measure.IndentWidth, measure.LineHeight)` - one step is one layout unit |
| `PageScrollSize` | `Viewport - ScrollSize`, per axis, floored at one step |
| `ScrollInvalidated` | raised whenever the offset or the extent may have moved from the editor's side |
| `BringIntoView(control, rect)` | `ScrollIntoView` for that rectangle |
| `GetControlInDirection(...)` | `null` - there are no child controls to navigate to |

Notes that are easy to get wrong:

- **`Extent` is the raw layout size, not `max(layout, viewport)`.** A document shorter than
  the window has `Extent < Viewport`, which makes `Maximum` zero and hides the bar under
  `Auto`. Clamping `Extent` up would erase the difference between "shorter than the window"
  and "exactly the window".
- **`Viewport` is the text area, not `Bounds.Size`.** A bar's maximum is `Extent - Viewport`,
  so reporting the whole control would let it scroll one gutter-width past the last column of
  text. `ViewportSize` is the number the painter and the clamp already use; the mapping has to
  use it too.
- **The step is a layout unit, not a pixel count.** `MonospaceTextMeasure` gives one line of
  height and `IndentWidth` of width; in the real editor `AvaloniaTextMeasure` gives the font's
  line height. A fixed 16-pixel horizontal step would creep instead of moving a cell.
- **A page keeps one line of overlap.** `Viewport - ScrollSize` rather than `Viewport`, so
  `PageDown` never loses the line the reader was on. This is the one place the plan invents a
  number, and it invents the conventional one.
- **`ScrollInvalidated` is the only notification the `ScrollViewer` gets.** `UpdateFromScrollable`
  is the one path by which the presenter learns `Extent`, `Viewport` and `Offset`, so a missing
  raise leaves the thumb stale until the next unrelated invalidation, which reads as "the
  scrollbar is broken". The control raises from: `ScrollTo` (behind its early return, so a host
  writing back the offset we already had hears nothing), `Refresh`, the end of an edit that
  changed the document, a menu command about to be shown, the `Session`, `FontSize` and
  `ShowLineNumbers` setters, and the two resize checks - the `Bounds` hook, which runs before
  the measure pass, and the end of `MeasureOverride`, which runs after the new wrap width has
  re-laid-out the document.
- **No feedback loop.** The `ScrollViewer` reacts to `ScrollInvalidated` by reading the
  properties back and setting `Offset`; the `Offset` setter calls `ScrollTo`, which clamps and
  invalidates the visual. Do not raise `ScrollInvalidated` from inside the `Offset` setter
  unless the value the `ScrollViewer` would read actually changed - a guarded raise, not an
  unconditional one.

### Arranged at the viewport, not at the extent

The whole mapping rests on one assumption: a `ScrollContentPresenter` arranges a *logical*
scrollable at the viewport rectangle, not at its extent. If it arranged the control at its
extent, `Bounds` would become the content size, `ViewportSize` would become the document, and
the table above would silently invert.

Verified against Avalonia 12.1.6 with a probe window of 240x120 holding a document that lays
out to 963x319:

| Read back | Value |
| --- | --- |
| `control.Bounds.Size` | 240x120 - the viewport |
| `session.Layout.Size` | 963x319 - the document |
| `ScrollViewer.Extent` | 963x319 |
| `ScrollViewer.Viewport` | 240x120 |

Setting `ScrollViewer.Offset` to `(60, 40)` moved `OffsetX`/`OffsetY` to exactly `(60, 40)`,
and resetting it to `(0, 0)` moved them back, so the two-way path works in both directions.
A document shorter than the window on one axis is clamped to zero on that axis and no bar
appears, which is the `Extent < Viewport` behaviour the mapping relies on.

### A control with no viewport does not scroll to the caret

A host sets `Session` before the control has bounds - there is no other order available - and
the `Session` setter calls `ScrollToCaret()`. At that moment `Bounds` is empty, so `ViewportSize`
is `0x0` and `ScrollIntoView` has nothing to decide.

It used to decide anyway, and wrongly: with an extent of zero, every target is "wider than the
window", so the start alignment parked the layout at the caret's own position. A control that
was then arranged kept that offset, because it is inside the scrollable range, so a document
wider than the window opened already scrolled sideways - the first keyword of the first line was
cut off. Measured on the S5 probe: a session set before the arrange left `OffsetX` at `48.65`,
the width of the `ruleset` keyword, and the first raster of the document started with `Payroll`
instead of `ruleset Payroll`.

The rule is now: **an axis whose window has no extent is left alone**, in `LayoutViewport` rather
than in the control. It is a geometric statement, not a toolkit one - no part of the layout is
on screen, so no offset shows more than another - and it keeps the control free of a "have I
been laid out yet" test. The first arrange does not have to undo anything, and a host that sets
a session on a control it never shows gets the document's start rather than an arbitrary
position inside it.

### Bar space is automatic

The control never reserves room for a bar and does not need to. With `AllowAutoHide` at its
default the Fluent bars are overlaid on the content, so the control keeps the full viewport;
with `AllowAutoHide = false` the presenter is arranged 16 pixels narrower and shorter and the
control's `Bounds` shrinks with it. Either way the resize is picked up by the `Bounds` hook, the
`WrapWidth` re-lay-out happens in the same measure pass, and the raised `ScrollInvalidated`
carries the new extent and viewport. Nothing in this plan has to know which mode the host's
theme uses.

Measured on a 240x300 window: with `AllowAutoHide = false` the editor's `Bounds` became
`224x284` and `ViewportSize` `196.91x284` - exactly what the two bars took - and rasterising
the window showed **no glyph pixel under either bar**. The bar bands held only the neutral grey
of the bar chrome (`0` of `3962` and `0` of `3136` sampled pixels were coloured), while the
content area was `18.9%` coloured.

The overlaid default is the other case, and it must not be described as "space is reserved":
with `AllowAutoHide = true` the control keeps the whole viewport, so the document is drawn
*underneath* the bar and the bar paints translucent chrome over it. The same band then sampled
`22.1%` coloured pixels. A host that wants nothing hidden by a bar sets `AllowAutoHide = false`;
one that prefers the floating bar accepts that the last few characters of a long line sit
behind it, which is what every overlaid scrollbar does.

### Measured: a document taller and wider than the window

S5 is the probe that exercises all of the above against a real window, because the control
cannot be constructed in a unit test. The document is the payroll sample with its rules read
through `Rules`' table variant, so it overflows on both axes: `266.67x782.09` laid out, 40 rows,
inside a `240x300` window.

| Question | Read back |
| --- | --- |
| Is `Bounds` the window and `Layout.Size` the document? | `Bounds` `240x300`, `Layout.Size` `266.67x782.09` |
| Does the interface report the same numbers? | `Extent` `266.67x782.09`, `Viewport` `212.91x300` (the window minus the gutter), offset `0,0` - so the furthest offset is `53.76, 482.09` |
| Do the bars move the content? | `ScrollViewer.Offset = (100, 200)` gave `OffsetX/OffsetY = 53.76, 200`: `Y` exactly, `X` clamped to the horizontal maximum |
| Does the caret still drag the view? | `Ctrl+End` put the offset at `53.76, 482.09` - both maxima exactly, and the caret landed on the document's last line; `Ctrl+Home` returned it to `0, 0` |
| Does the wheel step by lines? | One notch inside the host moved `Y` from `200` to `255.86`, which is `3 x 18.62` - `WheelLines` times the line height |
| Is the wheel handed back at the end? | On a detached control with a `212.91x120` viewport, 12 notches moved the offset and the 13th came back `Handled = false` with `Y` at its maximum - so a wrapping panel would receive it. One notch up was handled again. Shift+notch reached `X = 53.76`, its maximum |
| Does a short document swallow the wheel? | Detached, viewport `1372.91x900` against the same `266.67x782.09` document: `ScrollTo(0, 0)` returned `false` and a notch came back `Handled = false` |
| Is anything painted under a bar? | The solid-bar raster, sampled as described above: no coloured pixel in either band |

Two things this run settled:

- the session-before-arrange bug in
  [the section above](#a-control-with-no-viewport-does-not-scroll-to-the-caret), which the
  first raster exposed;
- the wheel assertions have to be made on a control that is in **no** visual tree. `RaiseEvent`
  bubbles, so inside a `ScrollViewer` the event comes back handled by the presenter even when
  the editor declined it, which would make "does not swallow" pass for the wrong reason.

## Wheel and keys

The wheel stays the editor's, and that is a decision rather than an oversight.

- **One scroll path.** Because `OnPointerWheelChanged` marks the event handled, the
  `ScrollViewer`'s own wheel handling never runs. That is what we want: our step is a line of
  the *editor's* layout (so a wheel notch moves exactly the number of lines the notation
  uses), not a fixed pixel amount, and the bars follow through `ScrollInvalidated`.
- **A short document hands the wheel back.** `ScrollTo` answers whether the offset actually
  moved, and the handler marks the event handled only when it did. An editor showing a
  document that fits therefore leaves the wheel to a surrounding panel, which is what makes
  the control safe to embed. This was a real embedding bug, not a polish item.
- **Shift+wheel is horizontal.** One notch is `WheelLines` lines on X instead of on Y, so a
  mouse with a single wheel can still reach the right-hand columns under an auto-hiding bar.
  A trackpad's own horizontal delta moves X without the modifier.
- **Page keys move the caret, they are not scroll commands.** `PageUp`/`PageDown` are
  `CaretMove.PageUp`/`PageDown`, a vertical move of one page, and `Ctrl+Home`/`Ctrl+End` are
  `CaretMove.DocumentStart`/`DocumentEnd`. The existing `ScrollToCaret` does the scrolling. A
  page command that scrolled without moving the caret would introduce a second notion of
  "where the user is", and every editing action would have to ask which one is current.
  `Home`/`End` stay cell-wise as they are.
- **A page is the viewport minus one line.** `EditorSession.PageHeight` (set from
  `MeasureOverride`) is the visible height, and `CaretNavigation` aims at
  `PageHeight - LineHeight` below the caret, so a `PageDown` never loses the line the reader
  was on. Two details follow: the target is an aim, not a count, so the move clamps to the
  nearest reachable line in that direction rather than refusing near the bottom, and a page
  with no viewport (`PageHeight == 0`, the unbounded-height case) degrades to the single-line
  move.
- **`Ctrl+PageUp`/`Ctrl+PageDown` stay with the host.** The unmodified page keys are the
  editor's; the combination is not listed in `EditorKeyMap`'s allow-list, so a host keeps
  whatever it did with it. `Ctrl+Home`/`Ctrl+End` are the editor's, because the document ends
  are the only thing they could mean here.
- **The `ScrollViewer` must not steal focus.** Bars are not focusable, but a `ScrollViewer`
  can take focus when clicked. The workbench sets `Focusable = false` on the wrapper so a
  click on the surface keeps the caret where it was.

## Hosts and embedding

- **The workbench wraps each tab's control.** `TabItem.Content` was the
  `ProjectionalEditorControl` itself, and six places in `MainWindow` relied on that
  (`tab.Content is ProjectionalEditorControl`, plus one `as`). They are now one helper that
  unwraps the `ScrollViewer`, so a second shape of tab content does not spread through the
  file. The helper answers `null` rather than throwing, because a tab whose content is
  something else is not an error - it is a tab the theme loop should skip.
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
  every property from a `ScrollRange` computed on demand from the current `Layout`, viewport and
  offset, and raises `ScrollInvalidated` from the places listed above. Computed rather than
  cached on purpose: the offset moves far more often than the layout, and a cached range would
  need invalidating at exactly the raise sites where a stale number would hide. No behaviour
  change without a host.
- **S3 - the workbench.** Wrap each tab's control in a `ScrollViewer` with `Auto` visibility,
  replace the `tab.Content is ProjectionalEditorControl` checks with one unwrapping helper, and set
  `Focusable = false` on the wrapper.
- **S4 - gestures.** Hand the wheel back when nothing moved, add Shift+wheel, route
  `PageUp`/`PageDown`/`Ctrl+Home`/`Ctrl+End` as caret moves, and set `LargeChange` behaviour
  through `PageScrollSize`.
- **S5 - verification.** A probe with a document both taller and wider than the window: assert
  `Bounds` is the window and `Layout.Size` is the document, drag/click the bars and read the
  offset back, press `Ctrl+End` and confirm the offset reached the end, scroll to the end with
  the wheel and confirm the event is no longer handled (so a wrapping panel would get it), and
  rasterise the picture to confirm nothing is painted under a bar.

S1-S5 are delivered. The numbers from the S5 run are in
[Measured: a document taller and wider than the window](#measured-a-document-taller-and-wider-than-the-window).

## Testing

| What | How |
| --- | --- |
| Step, page, extent, clamping | Headless tests on `ScrollRange` in `Projectional.Core.Tests` |
| The page keys move the caret a page | Headless tests on `CaretNavigation` with a page height |
| Every `EditorKey` has a route | `KeyRouterTests` asks the table for every declared member |
| A session set before the first arrange does not scroll the document | Unit: `LayoutViewportTests` asks for the caret before the control has bounds |
| The interface reports the layout, not a copy | Probe: read `ILogicalScrollable.Extent`/`Viewport`/`Offset` back after an edit changed the document's size (done: see the arranged-at-the-viewport table above) |
| Bars move the content | Probe: set `ScrollViewer.Offset`, read `control.OffsetX/OffsetY` (done) |
| A short document does not swallow the wheel | Probe: raise the wheel on a control whose document fits, read `args.Handled` (done: `Handled = false`, offset unchanged) |
| Nothing painted under a bar | Probe: rasterise with `Auto` bars and sample the gutter pixels (done: solid bars are clean at `0` coloured px; the overlaid default draws text under the bar on purpose, see [above](#bar-space-is-automatic)) |
| The caret still drags the view | Probe: `Ctrl+End`, read the offset (done: offset reached both maxima exactly) |

## Open questions

1. **Should the control expose `Extent` as a property?** The interface already does; a host
   that wants to draw its own minimap can read `ILogicalScrollable.Extent` without a new
   member. Recommend: no new public property.
2. **Does a nested `ScrollViewer` (editor inside a scrolling panel) misbehave?** Recommend:
   document that a host should give the editor a bounded row rather than a place inside an
   outer scroll surface; the inner control already clamps its own offset.
3. **Should `Home`/`End` also page - or jump to the document ends?** They stay cell-wise, and
   the document ends are a separate move on `Ctrl+Home`/`Ctrl+End`. Home/End inside a wrapped
   table cell would otherwise be ambiguous, and a host that wants the other binding can bind
   `CaretMove.DocumentStart` itself.
4. **Do bars need to be reachable by keyboard?** Recommend: no. Focus stays in the editor, and
   the page keys cover the same ground; a focusable bar would break Tab.
