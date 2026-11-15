# Font size

A projectional editor draws text it did not store, so nothing in the model says how large that
text is. The size is a property of the *view*: the same document opened in a small side panel
and in a maximised window should be readable in both, and neither choice may reach the model,
the undo history or the file on disk.

This document states what a font size actually is in this architecture, why it is expressed as a
different measure rather than a multiplier applied at paint time, and what the host-facing
surface is.

Status: **delivered** for M1. The default is 14 px; the control does not zoom itself.

## A size is a different measure

Layout never sees a font. It asks `ITextMeasure` how wide a string is and how tall a line is, and
every bound in the document is derived from those two answers. So the only way to draw a document
larger is to hand the layout a measure that answers larger numbers:

```
FontSize 14 -> "rule" is 4 x 8.4 = 33.6 units wide, a line is 16.8 units tall
FontSize 28 -> "rule" is 4 x 16.8 = 67.2 units wide, a line is 33.6 units tall
```

Nothing else in the pipeline changes. The tree, the addresses, the cell order and the caret
position are all size-free, which is why a zoom is a re-layout and not a re-projection: the same
`EditorSession`, the same `EditorTree`, a different `CellLayout`.

**The measure is replaced, never mutated.** `AvaloniaTextMeasure` memoises its line height and
indent width and keys a shape cache on the text and brush, so a mutated size would leave a cache
that describes glyphs drawn at the old size. `ProjectionalEditorControl.FontSize` therefore
constructs a new measure and hands it to everyone who holds one:

| Holder | Why it has to follow |
| --- | --- |
| `ProjectionalEditorControl.TextMeasure` | measures the document, and paints the runs |
| `EditorSession.Measure` | rebuilds the `CellLayouter` and refreshes tree and layout |
| `CompletionPresenter.TextMeasure` | draws the list on the same surface, so its rows must keep the editor's line height |

That is the whole mechanism. The alternative - one measure that mutates and clears three caches -
would make "which size is this string shaped for" a question with no answer.

## The session takes the measure as view state

`EditorSession.Measure` is a settable property, next to `WrapWidth`:

```csharp
public ITextMeasure Measure
{
    get => _measure;
    set { /* rebuild the layouter with the current wrap width, then Refresh() */ }
}
```

It rebuilds the layouter with the **current** `WrapWidth` rather than with no width, so zooming
does not silently unwrap a wrapped document - a font change and a resize are two different
questions and one must not answer the other.

`Refresh()` collapses a range selection onto the caret. That is accepted, not overlooked: it is
exactly what `WrapWidth` already does on every resize, and a selection rectangle whose two ends
were computed under a different measure would describe a range the user cannot see.

## The order in the shell matters

The session takes its measure from the control when it is built, so a document opened at a
zoomed size must be created in this order:

```csharp
var editor = new ProjectionalEditorControl();
editor.FontSize = _fontSize;                       // before the tab exists
var document = new DocumentTab(model, notations, editor.TextMeasure, ...);
editor.Session = document.Session;
```

Applying the size afterwards works but costs a second layout pass of a document that was already
laid out at 14, and the tab's title and dirty flag would briefly describe a document at the wrong
size. `ScrollToCaret()` is null-safe, so setting `FontSize` before `Session` is assigned cannot
throw.

## What a font size does not scale

Everything the layout measured follows automatically; everything that is a constant does not, and
that is deliberate:

- **The caret bar** is `Thickness` wide in control units, so it stays one hairline at every size.
  A caret that grew with the font would look like a block cursor.
- **Marker underlines** are two thicknesses thick and their dashes are counted in thicknesses, so
  the wavy line stays a line and does not become a ribbon.
- **The gutter** is computed from the measure - `DigitsOf(rows) * Measure("0") + 2 * GutterGap` -
  so it widens by itself when the digits do, without a second code path.
- **The completion list** has a fixed row count, and its rows are the editor's line height, so it
  grows taller with the font and keeps matching the text it offers to fill.

## Surface

`Projectional.Editing` holds the rule and knows nothing about a font:

| Member | Meaning |
| --- | --- |
| `EditorSession.Measure` | the measure the session lays out with; setting it rebuilds the layout and keeps `WrapWidth` |
| `EditorSession.WrapWidth` | unchanged; a zoom neither sets nor clears it |

`Projectional.Avalonia` holds everything that needs a real typeface:

| Member | Meaning |
| --- | --- |
| `ProjectionalEditorControl.FontSize` | the size documents are drawn at; rejects zero and negative sizes; same value is a no-op |
| `ProjectionalEditorControl.TextMeasure` | read-only from outside, because a host that replaced it directly would desynchronise the session and the completion list |
| `CompletionPresenter.TextMeasure` | the list follows the editor; public so a host that reuses the presenter keeps it in step |

`AvaloniaTextMeasure` itself is unchanged: it takes its size in the constructor and is immutable
after that. A host that wants a different typeface or a non-uniform size supplies its own
`ITextMeasure`, which is the extension point the layout layer already had.

## What this is not

- **No per-document zoom.** The size is a shell preference, like the theme and the gutter: two
  tabs showing the same model are drawn the same size, and switching tabs does not resize the
  document. Per-document zoom would have to live on `DocumentTab`, and a session restored from
  disk would have nothing to restore it from.
- **No automatic DPI handling.** Avalonia scales the control, so a document is already correct on
  a scaled display; `FontSize` is expressed in the same units the measure reports and does not
  try to second-guess the platform.
- **No font family or weight in this feature.** Changing the typeface is a `TextMeasure`
  replacement, and a host that does it owns the consequences for the shape cache.
- **No zoom persistence.** `_fontSize` is a field in `MainWindow`; nothing writes it to a settings
  file yet. When settings arrive, this belongs there and not in the model.

## What was verified

- Eight tests in `EditorSessionMeasureTests` pin the editing half: changing the measure changes
  `Layout.Size`, the tree is rebuilt but the addresses stay, the caret survives, the wrap width is
  kept, the same instance is a no-op, and a null measure is rejected.
- A throwaway Avalonia probe rasterised the real control and read the numbers back: at 14 the
  document measured `311,458984375 x 55,86328125` with a `19.55 px` gutter; at 28 it measured
  `582,326171875 x 148,96875` with a `27,09 px` gutter; at 8 it measured `177,9765625 x 31,921875`;
  setting 14 again restored the original numbers exactly. The height grows about 2.67x for a 2x
  size because a line height is rounded per line and not multiplied.
- A second probe drove the real `MainWindow`: `Ctrl+=` and `Ctrl+-` step by one and set
  `Handled`, `Ctrl+Shift+=` is treated as the same key, `Ctrl+0` returns to 14, `Ctrl+A` does not
  zoom, the three View items were found by their header text and clicked, 60 further clicks clamp
  at 48 and 60 decreases clamp at 6, the status bar reports the size, and a document opened
  afterwards through `File > New` came up at the zoomed size. Screenshots at 18 px and after a
  reset were inspected.
