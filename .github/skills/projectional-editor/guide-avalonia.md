# The Avalonia control, highlighting and the standalone shell

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How the control paints and takes input

`Projectional.Avalonia` is one `Control` (`ProjectionalEditorControl`) plus one measure
(`AvaloniaTextMeasure`). Everything that decides *what* is on screen lives in
`Projectional.Editing`, which is why the control is small enough to trust without a UI test.

- **The control owns no editing rule.** Pointer → `HitTest.Caret`, key → `EditorKeyMap` →
  `KeyRouter` → `EditorSession.Apply`. If you are about to write `if (key == ...)` in the
  control, the rule belongs in `KeyRouter` instead.
- **Input arrives in two streams.** Characters come from `OnTextInput` (one edit for a whole
  typed string, and the place IME will hook in later); navigation and deletion come from
  `OnKeyDown`. A letter therefore never appears in `EditorKeyMap` - it is not a key the
  editor routes.
- **Keys held with Control/Alt/Meta are the host's**, except for a short allow-list. `EditorKeyMap.Map`
  returns `null` for them on purpose: Ctrl+Left is a word jump in the surrounding app,
  Ctrl+Delete deletes a file in a file manager. The list is Ctrl+Left/Ctrl+Right, which
  map to `EditorKey.ControlLeft`/`ControlRight` because the plain arrows move a whole cell,
  Alt+Up/Alt+Down, which reorder the node under the caret - the vertical arrows do nothing
  else, so the host loses no gesture it had - Ctrl+Space, which opens completion,
  Ctrl+Home/Ctrl+End, which jump to the document ends, and Alt+Enter/Ctrl+., which are one
  `EditorKey.QuickFix` because no host can repair a model it cannot see. Ctrl+PageUp/Ctrl+PageDown
  are *not* in it: a page jump that also changes tabs stays the host's. Shift is not in that list
  because Shift+Tab is the editor's.
- **Escape always reaches the editor and is always consumed**, with or without a list open:
  `EditorKey.Escape` routes to `DismissCompletionAction`, which closes an open list and is a
  no-op otherwise. A host that wanted "Escape leaves the editor" therefore has to check
  `session.Completion` itself; `args.Handled` cannot tell the two cases apart, because the
  control marks every mapped key handled.
- **The arrows move by cell, Ctrl+arrows by character.** `CaretMove.Left`/`Right` land on the
  neighbouring caret place at the end/start of its text, so a press never leaves the caret in
  the gap between two cells and never creeps through a seven-character property one letter at
  a time. `CaretMove.LeftCharacter`/`RightCharacter` are separate members (not a flag) so a
  host can rebind them. Vertical moves stay geometric - see `CaretNavigation.Move`.
- **A refused edit stays unconsumed.** `e.Handled` is set from the outcome, not
  unconditionally: `EditOutcome.Rejected` (a letter typed into a number) leaves the event
  open so the host can beep or reroute it.
- **The caret blinks only inside a property cell and only while focused.** `CaretBlink` (in
  `Projectional.Editing`) holds the phase and the predicate; the control owns a
  `DispatcherTimer` that calls `Advance(blinking)` each tick and repaints only when it returns
  `true`. `Render` skips the caret bar while `IsVisible` is false. Every edit/move/click path
  goes through `Show()`, which calls `Reset()` - do not invalidate the control directly from a
  new input path or the caret can be dark at the moment of typing.
- **The pointer is an I-beam over the page and a hand over anything a press acts on.**
  `UpdatePointer(session, point)` runs from `OnPointerMoved` and resolves three shapes from the
  same geometry the paint and the press use: `Hand` over the quick-fix bulb (`BulbOnScreen`) and
  over a fold twisty (`FoldAt`), `Arrow` over the gutter, `Ibeam` over the page. The constructor's
  `Cursor = new Cursor(StandardCursorType.Ibeam)` is only the resting shape. Two rules: the shape
  is kept as a `StandardCursorType` so a move that changes nothing sets no property and allocates
  no `Cursor`, and holding a `Cursor` in a field trips CA1001 (it is disposable, and the control is
  not) - allocate on the transition instead. `Cursor` exposes no public getter for the standard
  type (only `ToString`, which prints `Hand`/`Arrow`/`Ibeam`), and the control cannot be
  constructed headlessly at all - its completion palette builds `StreamGeometry` from platform
  services - so this is verified through a real-window probe, not a unit test. Do not add a test
  that compares `Cursor.ToString()`; it would fail in the constructor before reaching the
  assertion.
- **Undo/redo report whether they did anything.** `Ctrl+Z`, `Ctrl+Shift+Z`/`Ctrl+Y` call
  `session.Undo()`/`Redo()` and only set `Handled` when the history had a step.
- **The context menu is a description, not a widget.** `EditorMenu.For(session)` (in
  `Projectional.Editing/Menu/`) is rebuilt on every opening, so no edit has to refresh it; it
  stores nothing and holds no reference to a toolkit. Its commands *are* the keyboard actions,
  so the gesture it prints cannot drift from the binding. Availability is decided before the
  entry is shown (`History.CanUndo`, "is there a character right of the caret"), and
  `EditorMenuItem.Execute` refuses a disabled entry as well, so a stale menu cannot edit.
  A host that renders this needs no editing knowledge - it maps labels, gestures, check marks
  and commands onto its own items and calls `Execute`. `EditorMenuPresenter`
  (`Projectional.Avalonia/Menu/`) is that mapping for Avalonia and is public, so the shell's
  menu bar and the control's context menu share one implementation. Two rules the control
  keeps: a right click moves the caret unless it lands inside a selection (otherwise the
  entries would describe a caret the user is not looking at), and after an entry runs the
  control repaints *and* closes the menu itself - the toolkit's own close only happens on a
  pointer release, so a keyboard-activated entry would leave it open.
- **`EditorSession` raises no event.** The host invalidates: `InvalidateMeasure()` when
  `result.Changed` (the document's size changed), `InvalidateVisual()` plus
  `ScrollToCaret()` always. There is no observable collection to subscribe to, by design.
- **`Refresh()` collapses a range selection** onto the caret - documented in
  `EditorSession.Refresh` and easy to misread as "highlights are broken" while debugging.
  Re-select after calling it.
- **Text, not glyph runs.** Each run is shaped by `AvaloniaTextMeasure.Shaped(text)` and
  cached per string; the cache is emptied wholesale at 8192 entries. `FormattedText` is used
  rather than `TextLayout` because one run is one *cell's* text, not a line.
- **`Refresh`-free layout.** A changed model rebuilds tree *and* layout in
  `EditorSession.Apply`; the control only schedules a measure/visual invalidation.
- **Only some pointer input can be synthesised in a probe.** `PointerPressedEventArgs` has no
  public constructor, so click/drag is verified through `HitTest` directly. The **wheel** is
  different: `PointerWheelEventArgs` is constructible, so `OnPointerWheelChanged` - and with it
  "the wheel moves the offset" and "a fitting document hands the event back" - can be driven
  end to end with `control.RaiseEvent(args)` and asserted on `args.Handled`. See the
  verification habits below.
- **A pointer *move* is synthesizable too, but through the base class.** Avalonia 12.1.6 has
  **no** `PointerMovedEventArgs` type; a move is a `PointerEventArgs` raised with
  `InputElement.PointerMovedEvent`, whose public constructor is
  `(RoutedEvent routedEvent, object source, IPointer pointer, Visual rootVisual, Point rootVisualPosition, ulong timestamp, PointerPointProperties properties, KeyModifiers modifiers)`.
  Passing the control as `rootVisual` makes `e.GetPosition(control)` answer `rootVisualPosition`,
  which is how "hovering the bulb gives a hand" was proven: raise the move, then read
  `control.Cursor.ToString()`. Raising the real event rather than invoking the private method
  proves the `OnPointerMoved` wiring as well as the decision. `Pointer` is ambiguous with
  `System.Reflection.Pointer` once `System.Reflection` is imported - qualify it.

## How highlighting works

Three layers, and the seam between them is the whole design:

| Layer | Owns | Types |
| --- | --- | --- |
| `Projectional.Projection` | what a cell's text *means* | `TextStyleKind`, `ICellStyler`, `CellStyler` (in `Tree/`) |
| `Projectional.Editing` | which run carries which kind | `TextRun.Style`, `EditorFrame.Build(..., ICellStyler?)` |
| `Projectional.Avalonia` | what a kind *looks like* | `SyntaxPalette` (in `Text/`), `ProjectionalEditorControl.Palette` |

Invariants that are cheap now and expensive later:

- **No colour ever enters the model, the language or `Projection`.** `TextStyleKind` is a
  vocabulary of meanings; a saved model carries no formatting, so it cannot go out of sync
  with the concept that produced it. `Projection` must not reference Avalonia (ADR 0010) and
  colour is an Avalonia type, so the layering enforces this by itself - keep it that way.
- **`ICellStyler` must be pure.** Layout measures a cell's text *before* the frame is built,
  so a styler that consulted the model, or answered differently on a second call, would
  produce a frame whose colours describe text other than what its bounds were computed from.
  Read the cell, never the node.
- **`SyntaxPalette` varies the brush only - never weight, style or size.** A bold keyword
  would be wider than the box the layout handed it, and the caret, hit test and selection
  rectangles would all sit a few pixels off. Colour is the one property that changes no number.
  Emphasis that *does* change metrics lives on the cell instead (see below).
- **Swapping a palette is a repaint, not a re-projection.** `Palette` only invalidates the
  visual; the model, tree, layout and every bound stay as they were. A change that needed a
  re-projection would mean the palette had leaked into meaning.
- **A theme carries the overlays too, not only the text.** `SyntaxTheme` is the one object a host
  passes for "make this dark", so it holds `Palette`, `Background`, `Chrome`, `Caret`, `Highlight`,
  `Gutter`, `LineNumber`, `Markers`, `Lists` (the completion and quick-fix lists), `Tip` (the marker
  tooltip) and `Bulb`. Anything painted *over* the document is read against it: a host that themes
  the page and forgets an overlay ships a white box on a dark page, which is what happened before
  `Lists`, `Tip` and `Bulb` were added. `Lists` is built lazily (`field ??=`) because a
  `CompletionPalette` carries vector icons and a `StreamGeometry` needs a rendering platform -
  constructing it eagerly made `SyntaxTheme`'s static initializer require a window and broke every
  headless test that compares themes. Keep `SyntaxTheme` free of anything that needs a platform.

### Three tooltips, one pointer

The control can show three boxes: the finding's message over a squiggle (`MarkerTip`), a concept's
description over any node (`ConceptTip`) and a slot's description over a property cell (`PropertyTip`) -
over an empty one always, over a filled one when the slot declared `showDescriptionWhenFilled`. All three
share `MarkerTipPresenter` and `MarkerTip.Place`, and they differ in exactly
two ways that must not be "tidied" away:

- **The marker wins.** A finding is specific and asks for an action; a description is ambient. If both
  would apply under one pointer, only the finding's box is painted. Never paint both.
- **Only the concept tip waits.** A squiggle is a shape a user aims at, so its tip is immediate; text
  is what the pointer crosses, so a description appears after 600 ms. A delay on the marker tip would
  make a hover feel broken.

All three are dismissed through the one path that already existed (`ClearHover`, reached by pointer exit,
any edit and any caret move). `ConceptTip.Of(session, nodeId)` and `PropertyTip.Of(session, position)`
answer `null` for something that no longer exists - a pointer can outlive its node, and a tooltip must
never throw.

**Both tips carry one shape**: `TipContent` is a heading plus a sentence, `TipLines.Of(content, width,
measure)` is the one wrapper, and the control has one painter for both. A second content type would mean
a second painter path.

- **The concept tip is anchored to the node's own address** (`new CellAddress(nodeId)`), not to the
  caret place the pointer resolved to, or it jumps while the pointer crosses a line of a node that spans
  several.
- **The property tip is anchored to the slot's cell**, and the property's sentence beats the concept's.
  A slot tip is about that one box - and a node drawn only as a property (an `IntLiteral` is one cell)
  has **no cell carrying its node address at all**, so anchoring a slot tip to the node would draw
  nothing. `ConceptTipBounds` therefore asks the slot address first and falls back to the node address
  (a fallback the real-window probe forced: the first implementation drew no box for a bare literal).
- **`PropertySlot.description` shows over an empty value without being asked for**, because an empty box is a
  question the user is asking; a filled one shows it only when the slot opted in
  (`PropertySlot.ShowDescriptionWhenFilled`, off by default), for text that does not explain itself. A slot born
  with a `defaultValue` reads as filled, since the model cannot tell a default from something the user typed -
  which is why the built-in languages opt in on their defaulted slots. The hover keys on **(node, slot)**, so
  moving between two properties of one node restarts the delay and moving inside one does not.

### Emphasis (bold, italic, underline, strikethrough)

A second, orthogonal axis: `TextEmphasis` is a `[Flags]` enum in
`Projectional.Language/Style/TextEmphasis.cs`. Both axes travel together on a run
(`TextRun.Style` + `TextRun.Emphasis`), but only one of them may be decided late.

- **Emphasis must be known when the tree is built, not when it is painted.** Bold and italic
  change glyph advances; a paint-time-only decoration would desynchronise drawing from the
  rectangles the caret and hit test were computed from. So it sits on `TextCell.Emphasis` and
  is threaded through every `TextCell` subclass, `CellLayouter.TextSize` and
  `ITextMeasure.Measure(string, TextEmphasis)`.
- **`ITextMeasure.Measure` has no default for the emphasis parameter, on purpose.** A host
  must not be able to forget that a cell's width depends on how it is set. Adding a default
  would silence the compiler precisely where it is useful.
- **Two sources, composed at build time.** The notation declares permanent emphasis
  (`NotationEditor.Keyword("rule", TextEmphasis.Bold)`); a host-supplied `ICellEmphasis`
  resolves per node per rebuild. `EditorTreeBuilder.BuildNode` computes
  `inherited | (resolver.EmphasisOf(node) ?? None)`, and a leaf ORs the notation's own
  emphasis on top - hence a struck-through rule still shows a bold `rule` keyword
  (`Bold | Strikethrough`).
- **A resolver's answer inherits into the whole subtree.** Name the node you care about, not
  every descendant.
- **`ICellEmphasis` is not `ICellStyler`, and lives in `Projectional.Language`, not
  `Projection`.** It is a statement about how a language reads, like the notation, so a
  language package (`Projectional.Samples`) must be able to declare one without referencing
  the projection layer. `Projectional.Samples` references only `Expressions` and `Language` -
  that is the whole reason for the file's location; do not "tidy" it back into `Projection`.
- **Nothing is stored or persisted.** The sample decorates a rule whose `outcome` is
  `Reject`; changing the outcome back removes the decoration on the next rebuild. Verified
  with a rendered probe: every word keeps its exact x and width, only the replaced outcome
  word differs.
- `MonospaceTextMeasure` ignores emphasis deliberately (a monospaced face keeps its advance
  when bold) so headless snapshots stay in agreement with the real editor. Tests use
  `WideBoldTextMeasure` (bold char = 2 units) to prove geometry actually follows emphasis.

`CellStyler.Default` reads only the cell: `ConstantCell` uses the notation's `ConstantKind`,
`PropertyCell` switches on `PropertyType.Name` (`"int"`/`"float"`/`"date"` -> `Number`, `"string"` ->
`Text`, bool and every enumeration -> `Keyword`), references use `ReferenceState`, an empty
child/collection is `Placeholder`, `MissingNotationCell` is `Missing`. It never emits
`Identifier` - that kind exists for a host styler that knows a cell is a name, and the
palettes are expected to be complete anyway.

Two performance facts worth keeping:

- **The shape cache is keyed on `(text, brush, emphasis)`** (`ShapedTextKey`), not on text
  alone. The same word is a keyword in one place and an identifier in another, and the same
  word set bold is a different shape again, so text alone stopped being a correct key the
  moment either axis existed.
- **Measuring shapes at the default brush.** `ProjectionalEditorControl.Palette` also points
  `TextMeasure.Foreground` at the palette's `Default` brush, so a `Default` run is painted
  from text that has already been shaped for measuring. Only genuinely coloured runs pay for
  a second shape. Do not drop that line as redundant.

`SyntaxPalette` is `FrozenDictionary`-backed with `ImmutableSolidColorBrush` values (cheap
reference equality for the cache key), `Light`/`Dark` presets, `Create` (requires `Default`,
rejects a null brush and a kind mentioned twice - checked by hand, because
`ToFrozenDictionary` will not tell you which of two brushes won), `BrushOf` (falls back to
`Default`) and `With` (returns a new palette, never mutates).

## How the standalone app fits

`Projectional.Workbench/Documents/` also owns the **recent list**: `RecentFiles` is the value (newest first, no
repeats, ten entries, case-insensitive because a Windows path is) and `TextRecentFilesStore` is a text file under
`%LOCALAPPDATA%` with a path per line. The split is the point - the list owns the rules so "what counts as the same
file" is answered once, and the store owns the bytes and **never throws**: a missing, unreadable or unwritable file
answers with what is still true, because a shell that refused to start over its own convenience file would be losing
work to keep a menu. What the store reads back is validated by `RecentFiles.From`, since a file a user can edit is one
they will edit. There is deliberately **no `IRecentFilesStore`**: it existed for one commit, had exactly one
implementer, and the analyzer was right that it was an abstraction waiting for a second caller that never came - the
tests exercise the list and the store directly.

`Projectional.Workbench/Outline/` is the model browser: `Outline.Of(model)` lists every node, depth first, and the
window fills a flat list. It walks the **model**, not the projection - the opposite of `Finder`, and deliberately:
a browser answers "what is this document made of", so a concept with no notation is still part of it, while find
answers "where is this text on the page", which only the projection knows. Depth is counted from the root rather
than stored (a node can be moved), and the list is rebuilt on every model change like the findings. Both shell
features follow one pattern worth copying: **derive the rows in a non-Avalonia type, test it, leave the window as
wiring** - the problems panel is `ProblemList`, the browser is `Outline`.

`Projectional.Editing/Find/` is the search: `Finder.Find(session, query)` answers `FindMatch` values in document
order, `FindNextAction(query, direction)` selects the next one **with wraparound**, and the app's strip
(`Ctrl+F`, Enter/Shift+Enter/Escape) only counts and scrolls. Three rules are decisions, not details:
the search walks **caret places** (so a property the notation hides cannot be found - the rule the caret obeys)
and only **property cells** (a keyword is drawn from the notation, so a match in one is an offer no edit can
keep); matching is **ordinal**, never culture-aware; and the occurrence the last press selected is **skipped**
(`IsCurrent`), because a second press must move rather than find what the first one did - a test caught that rule
missing for backward search. `FindNextAction` is a navigation: no undo step, and a query that matches nothing
leaves the caret alone. **Replacing is an edit and therefore an action**, not bar logic: `ReplaceMatchAction`
rewrites one value's text, `ReplaceAllAction` does every occurrence in one transaction - one cell rewritten once,
replacements applied **right to left** so offsets stay valid, refused values skipped and counted, and a run that
wrote nothing rolled back so the history gains no empty step.

The problems panel is the model for a shell feature: `Problems/ProblemList.Of(model, findings)` turns the
diagnostics the editor is already drawing into rows, headless and tested, and `MainWindow` only fills a `ListBox`
and scrolls. Two rules in it are decisions, not formatting: **the order is the document's** (a severity-sorted
list renumbers itself while the user fixes things) and **a row carries a `CellAddress`** - the finding's slot when
it named one, else its node - because "jump to it" is `session.Select(Selection.At(row.Place))` and nothing more.
A finding whose node is gone still gets a row and sorts last; hiding it would claim the document is clean.
Add the next panel the same way: derive the rows in a non-Avalonia type, assert them, and leave the window as
wiring.

**One finding, one row.** The shell adds its own pair of checks (`DefaultNames`, `DuplicateRuleNames`) only to a
document whose language states nothing: `MainWindow.PublishFindings` prefers the language's `Checks`, then its
`Constraints`, and the shell's pair only when both are absent. The middle case is the one a picture caught - a
declared language has no check set, so asking only for `Checks` let `SHELL001` run beside the language's own
"a stage is still called unnamed" rule and every stage reported the same hint twice. A language that declares
findings has said what it wants a name to be, whichever of the two ways it said it.

**An import's answer is not a panel row.** `ImportDiagnostic` describes the file - a line and a column, and for a
refusal no node at all - while a panel row carries a `CellAddress` and is re-derived from the model on every edit.
So both import outcomes go to the status line in the importer's own words (`ImportSummary` for a success, the
importer's refusal for a failure), and a success **with warnings** is still a sentence the user must see: the
importer reports "kept as raw text" rather than failing, so the host is the only place that can say it.

The repository ships two front ends and **one** public surface:

- `Projectional.Avalonia` is the embeddable control. Hosts reference it (which brings `Core`).
- `Projectional.Workbench` is the standalone desktop editor. It references `Projectional.Avalonia` **and** the language
  packages it composes (Samples, Structures, Editors, the reference languages and the three worked examples), each of
  which carries its own checks and intentions since
  [ADR 0021](../../../docs/adr/0021-one-project-per-language.md) - so the list is one entry per language rather than
  two or three. Its own types are `internal` and
  the project grants `InternalsVisibleTo` **to the test project**, so the shell's logic is testable headlessly without
  widening its public surface. What stays forbidden is the app *reaching past* the control into a layer it does not sit
  above by needing a reference for a type no public member mentions ([ADR
  0011](../../../docs/adr/0011-standalone-editor.md), `docs/architecture/standalone.md`).

Rules that follow from that, and are easy to break by accident:

1. **Never add a `ProjectReference` from `Projectional.Workbench` to `Core`-adjacent internal
   projects** (`Language`, `Projection`, `Editing`, `Semantics`, `Persistence`). It used to
   reference `Semantics`; that was removed when ADR 0011 was written. If the app seems to need
   an internal type, the public API is missing something — widen `Projectional.Avalonia`'s
   surface instead.
   **Transitive types are not a reference.** The app names `MarkerSet` and `Diagnostic`, which
   reach it through `Projectional.Avalonia`'s own public `Markers` property - a public surface
   has to mention the types it is written in, so `Avalonia` must reference `Editing`, which
   must reference `Semantics`. The layer diagram puts `Semantics` in the same band as
   `Projection`/`Editing`, i.e. *below* Avalonia and Workbench, so the arrow is downward and
   legal. What is forbidden is the app *reaching past* the control into a layer it does not
   sit above; using a type that a public member is typed with is what a reference consumer is
   for. Rule of thumb: if the app can express the feature with only members Avalonia makes
   public, it is allowed; if it needs a `ProjectReference` to see a type, it is not.
2. **Never grant the workbench `InternalsVisibleTo`.** It is the reference consumer; an
   exemption makes it prove nothing.
3. **Never put a feature in the app that a host cannot reach.** Shell concerns that legitimately
   belong to the app: windows, tabs, menus and dialogs, recent files, settings, file watching,
   dirty prompting, command registry and shortcut table, command-line parsing.
4. **The shell is plain classes plus wiring.** Documents, `CanExecute` and lifecycle live in
   non-Avalonia types so they are testable headlessly; only `App`, `MainWindow` and the two
   controls touch Avalonia. A UI test harness (`Avalonia.Headless.XUnit`) is added when there is
   wiring worth asserting on, not before.
5. **The dirty flag is derived from the change journal**, never a boolean the app sets on edit and
   clears on save — otherwise undo back to the saved state still claims "modified".
6. **Undo belongs to the session, not to the tab.** A document keeps its session while open and
   disposes it when the document closes; forgetting the dispose leaks undo state.
7. **View ▸ Representation is built from the active document, not from the registry.** It binds
   `EditorSession.Alternatives()` (concepts the document uses that have more than one variant) to
   one submenu per concept with radio items, and clicking one calls `ChooseVariant` + `Refresh()`
   on that tab's control. The menu is refreshed from `UpdateStatus()`, holds no state of its own
   and must stay disabled when there is nothing to switch.

8. **The shell is a library, not only an app.** Its content is `ShellView` (a `Control`),
   `ShellWindow` wraps it, and the standalone app is that window with every capability and no
   language lock. A host embeds the shell locked to one language, with its own `IShellStorage`
   and `IShellDialogs` ([embedded-shell.md](../../../docs/architecture/embedded-shell.md),
   [ADR 0015](../../../docs/adr/0015-shell-is-a-library.md)). So write shell code as if a host
   already owns it: no statics holding documents or preferences, no `Application.Current`, no
   settings file, and never a capability enforced only by hiding a menu entry - the command
   registry refuses it.

Scope by milestone, so the app does not compete with the libraries: M1 = open project, tabs,
edit, save, dirty prompt. M2 = model browser, problems panel, single-file build. M4 = output
panel. M5 = language browser, live preview, language creation (the MPS moment). M6 = the shell
as an embeddable, language-locked component. M7 = installers and file association.

Work that touches any of this also touches a doc; the app's plan lives in
`docs/architecture/standalone.md`, and the milestone scope in `docs/roadmap.md` M1/M2/M4/M5/M6/M7.

