# Line numbers

A projectional document has no lines, so a line number cannot be a fact about the document the
way it is in a text editor. What the layout *does* produce is rows: horizontal bands at a fixed
distance from each other, each holding whatever the projection put on it. Line numbers number
those bands. That is enough for the two things a reader uses them for - "which row am I on" and
"where is the thing the problem panel is talking about" - and it costs the model nothing.

This document states the rule that decides which cells share a number, why the gutter sits
outside the scroll transform, and what the host-facing surface is.

Status: **delivered** for M1. `ShowLineNumbers` is off by default.

## A row is a band of drawn text

`LineNumbers.Of(CellLayout, ITextMeasure)` walks the whole layout and answers one
`LineNumberRow` per band:

- a cell contributes one band per line of text it actually draws, so a wrapped cell is one row
  per drawn line and never one row per cell;
- a container contributes nothing, because `CellText.IsVisible` is false for it - a container
  covers the same area as its children, and counting it would number the same band twice;
- an unset property cell contributes nothing either, since it draws no text; it always shares
  its band with a visible cell, so no row is lost.

The gutter counts what the layout draws and has no opinion of its own, so it is exactly as
right as the wrapping is. A wrap that broke a word between two characters used to give a cell
starting one character inside the page edge six bands, and a three-line document eight numbers;
`TextWrapper` now breaks only at a space and draws a wider word whole, past the edge
([projection.md](projection.md) §"A word wider than the room left is drawn whole, past the
edge"). One test pins that shape: the payroll sample at a width that leaves `Accept` one column
of room is still three rows.

The bands are then sorted by top and deduplicated with a tolerance of half a line height. Two
cells on one line share a number; a cell that is half a pixel off because of rounding does not
get a number of its own. Numbers always run from 1 without a gap, so row *n* is the *n*-th band
and not a label that happens to be attached to it.

The tolerance is `ITextMeasure.LineHeight / 2` rather than a constant, because a measure that
makes a line taller than a character (the real one does) makes rounding noise larger too.

**Numbers are positions, not identities.** Inserting a two-row rule above row 7 renumbers
everything below it, and that is correct: the number answers "where in the picture", and the
picture just changed. Anything that has to survive an edit is a `NodeId`.

## Counted from the whole layout, not from the viewport

`Of` is given the `CellLayout`, which covers the entire document, and not the visible window.
Numbers therefore stay put while the reader scrolls - a number is a property of the document,
not of the scroll position. Only the painting step clips: a row whose top falls outside
`0 .. Bounds.Height` is skipped.

That is why the row list is cached on the control: counting walks every cell, and a caret blink
must not cost a pass over the document. The cache is keyed on the identity of `session.Layout`
and `session.Measure`, both of which are replaced wholesale on a rebuild, so "is this the layout
the numbers were counted from" is a reference comparison rather than a version number.

## Digits, and how wide the gutter is

```
GutterWidth = (ShowLineNumbers ? DigitsOf(rows) * Measure("0") + 2 * GutterGap : 0)
            + (ShowBulb ? Round(LineHeight) + BulbGap : 0)
```

Both halves of the number part matter: `Measure("0")` is the only way to learn how wide a digit is
in the current font, and the digits are counted by dividing the row count by ten (`9 -> 1`,
`10 -> 2`, `999 -> 3`). The width therefore grows by exactly one digit's worth the moment the
document crosses 9, 99 or 999 rows, and the numbers keep their right edge.

The right edge is what the numbers are aligned to - `GutterWidth - BulbMargin - GutterGap -
text.Width` - so `1` and `10` do not wobble, and so the numbers are never painted underneath the
quick-fix bulb that now occupies the outer margin ([quick-fixes.md](quick-fixes.md), slice Q6).

**This is the second-occupant form of the rule.** [collapsing.md](collapsing.md) predicted that a
second occupant would turn the sum into three terms and make the padding depend on whether the
gutter has any occupant at all rather than on `ShowLineNumbers`; the bulb is that occupant. Two
rules follow, and both are cheap now and expensive later:

- **Each occupant reserves its own term**, so a host can turn one off without recomputing the
  other's arithmetic. Do not fold the bulb's square into the number term.
- **An occupant's margin is reserved whether or not it has anything to draw.** The bulb's margin is
  there on a document with no findings at all, because a margin that appeared on demand would shift
  every cell of the page sideways when the caret moved onto a marked line.

A per-row fold arrow will be the third term, and it has to answer the same question the bulb does:
does the *row* have an arrow, or does the *gutter* always reserve the room?

## The gutter sits outside the scroll transform

A code editor's gutter does not slide sideways when the code scrolls; a line number that could
be scrolled out of view would be useless. The same is true here, and the cost is that every
place that converts between layout coordinates and control coordinates has to know about the
gutter:

| Member | What it does with `GutterWidth` |
| --- | --- |
| `ViewportSize` | subtracts it from the room the document may use |
| `MeasureOverride` | passes `WrapWidth = availableSize.Width - GutterWidth` to the session, so wrapping happens where the text is, not under the gutter |
| `CaretAt` | refuses points left of it, and shifts the ones right of it back into layout coordinates |
| `CompletionBounds` | shifts the list right, so it hangs under the caret rather than under the gutter |
| `ToControl` | adds it to every painted rectangle |

Vertical scrolling is untouched: the gutter scrolls with the document, because a number belongs
to a row and the row moved.

**The width depends on the rows and the rows depend on the width**, since wrapping a cell can
produce more rows. The loop converges in one or two measure passes because the digit count is a
step function that stops changing as soon as the width is wide enough for the longest number;
`ShowLineNumbers` starts it with `InvalidateMeasure()`.

Clip safety comes free: the control sets `ClipToBounds`, so neither the gutter nor the shifted
content can paint over the host's neighbours, and an embedded editor that turns the gutter on
loses exactly `GutterWidth` pixels of text room.

## Surface

`Projectional.Editing` holds the rule and nothing about drawing:

| Member | Meaning |
| --- | --- |
| `LineNumbers.Of(CellLayout, ITextMeasure)` | the numbered rows of a laid-out document |
| `LineNumbers.DigitsOf(IReadOnlyList<LineNumberRow>)` | how many digits the widest number needs |
| `LineNumberRow(int Number, LayoutRect Bounds)` | one row; `Bounds.Width` is 0 because the gutter's width belongs to the painter |

`Projectional.Avalonia` holds everything that needs a font:

| Member | Meaning |
| --- | --- |
| `ShowLineNumbers` | off by default; turning it on re-measures, because the text room shrank |
| `GutterWidth` | read-only, computed; 0 when the gutter is off |
| `LineNumberBrush` / `GutterBackground` | colours; the background may be unset, which leaves the page showing |
| `LineRows` | the numbered rows as last laid out, for a host that wants to name a position |

`GutterBackground` defaults to unset on purpose: a host that already draws the document on a
surface of its own should not get a second shade reading as a panel next to the page. The
standalone shell sets both from `SyntaxTheme.Gutter` / `SyntaxTheme.LineNumber`, so the dark
theme does not paint grey numbers on a dark page.

## A gap between the gutter and the page

Planned, and small: **two device pixels** of blank between the right edge of the gutter and the
first column of text, so a row number does not touch the document it labels.

The gap is **chrome, not layout**, so it is added to `GutterWidth` and nowhere else:

```csharp
public double GutterWidth => _showLineNumbers
    ? (LineNumbers.DigitsOf(Rows) * TextMeasure.Measure("0", TextEmphasis.None))
      + (2 * GutterGap) + PageMargin
    : 0;
```

`GutterWidth` is the page's left edge: painting translates by `gutter - offsetX`, and both the
marker band hit test and the hover conversion translate back with `point.X - gutter + offsetX`. A
gap introduced anywhere else - inside the page transform, as a spacer cell, or as padding on the
control - would move the painted text without moving the rectangles that the caret, the markers
and the hit test are computed from, and the symptom would be a caret that is two pixels off near
the gutter. Adding it to `GutterWidth` keeps one number for "where the page starts".

It applies **only while the gutter is shown**. With `ShowLineNumbers` off there is no gutter to
separate from, so the page starts at the control's own edge and `GutterWidth` stays `0`.

## What this is not

- **No current-row highlight, no breakpoint margin, no click to select a row.** Each of those is
  a margin feature; the gutter is currently a painting and not an input surface, and `CaretAt`
  returns null inside it, so a click there does nothing rather than putting the caret in the
  wrong place. A fold arrow ([collapsing.md](collapsing.md), planned) is the first exception worth
  making: it is genuinely an input surface, and it is why the plan puts a hit test *in front of*
  `CaretAt` rather than relaxing `CaretAt` itself.
- **No "go to line".** A line number is derived, so jumping to one means finding the address of
  the cell at that band - a different feature with a different failure mode (the row may move
  between the menu opening and the key press).
- **No numbers in the snapshot renderer.** `LayoutSnapshot` renders a document for a test, and a
  test that had to skip a gutter would be testing the gutter.

## What was verified

- Fifteen tests in `LineNumbersTests` pin the rule: one band per drawn line, one number per
  band, no gaps, a wrapped cell counting twice, cells sharing a number, an empty slot still
  producing a band, the first band at the top of the layout, digit growth at
  0/1/9/10/99/100/1000, and a document that stays three rows when a word only just does not fit.
- A throwaway Avalonia probe rasterised the real control and sampled pixels: the gutter occupies
  `19.55 px` for a one-digit document (one `0` plus two gaps), the text moves right by exactly
  that, the numbers line up on their right edge, `GutterBackground` fills the full height,
  `LineNumberBrush` arrives at the glyphs (`#2B91AF` sampled back out of the PNG), and turning the
  gutter off restores a width of 0 and the original text position.
