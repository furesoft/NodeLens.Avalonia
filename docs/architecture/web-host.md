# The browser editor

Status: **delivered** (sample). The code is `samples/web-app/`, its tests are
`tests/Projectional.Web.Tests/`.

This document is the reference for the browser sample: how a page paints a projectional document, how
input reaches the editor, what the shell around it does, and - deliberately - **what it does not do yet**
compared with `Projectional.Workbench`. Read it before extending the sample, so a second frontend does
not quietly grow a second set of rules.

## Why a second frontend exists

ADR [0001](../adr/0001-ui-stack-avalonia.md) chose Avalonia for the *desktop* editor and rejected
Blazor/WebAssembly as the primary UI stack. It did not forbid a second frontend; the architecture
overview says the layering exists so that "a second front end (Blazor) stays possible"
([overview.md](overview.md)). The browser sample is that sentence made executable.

The sample therefore reuses the headless layers unchanged and reimplements only the chrome:

```
Core → Language → Projection → Editing         ← reused as-is (no Avalonia)
                              ↘
                               Projectional.Web   ← the browser host (this document)
                              ↗
Projectional.Web.App (Blazor WASM)              ← the page, the menus, the input element
```

It references `Projectional.Editing` and `Projectional.Semantics` directly. That is allowed for a host
(the toolkit excludes both on purpose); it is *not* a package and must not become one.

**Avalonia in the browser was considered.** `Avalonia.Browser` can host a control (Skia → canvas) and
would give the editor's fine interaction almost free. It was not chosen because:

- the request was a Blazor WebAssembly example, and the sample's job is to prove the headless seam;
- the desktop shell is still a window, not a library (ADR [0015](../adr/0015-shell-is-a-library.md)):
  `MainWindow` depends on the docking layout, file dialogs, `FileShellStorage`, the terminal's
  *processes*, and Velopack. None of that runs in a browser, so an Avalonia target would need a
  browser shell anyway - it would save the editor's interaction, not the shell;
- a canvas has no DOM text: browser find, zoom, accessibility and text selection behave differently;
- the measured published bundle is 19.5 MB on disk, ~6 MB compressed (241 files). An
  Avalonia + Skia + .NET bundle is larger.

The trade-off is accepted: the browser sample is lightweight and embeddable, and it pays for that with
the parity gaps listed at the end.

## How the page paints a document

`Projectional.Web.EditorMarkup` is the only painter.

1. `EditorDocument.Frame()` builds an `EditorFrame` over the **whole** document: `EditorFrame.Build(layout,
   fullSize, 0, 0, selection, MonospaceTextMeasure.Instance)`. No culling - the scroll container owns
   scrolling, so a frame that culled by a viewport nobody scrolled would hide reachable text.
2. `EditorMarkup.Canvas` writes absolutely positioned `<span class="pn-run pn-keyword">` per `TextRun`,
   `<div class="pn-selection">` per highlight and `<div class="pn-caret" data-role="caret">`.
3. Layout units are the monospace measure's: one unit is one character column, one line is one unit
   high. `CellMetrics` multiplies by the *measured* pixel advance - never a constant - so the painted
   glyphs and the rectangles the caret and hit test use cannot drift.
4. `wwwroot/js/editor.js` `measure()` reads the computed style of `.pn-canvas`, measures 100 digits and
   returns `[advance, lineHeight]`. Zoom changes `--pn-font-size` and re-measures; nothing else changes.

Markup rules that are easy to break:

- text from the model is escaped (`&`, `<`, `>`, `"`, `'`);
- a tab or newline inside one line becomes **one space**, because the monospace measure counted it as
  one column and a browser would paint a tab as a variable gap;
- Bold and italic are *not* painted: the monospace measure treats them as the same advance, so a wider
  face would desynchronise the caret. Underline and strikethrough are painted - they change no metric.

The canvas is a `MarkupString` inside a stable `.pn-layer`; the completion popup is a Razor sibling of
that layer, so Blazor can re-render the popup and replace the painted HTML independently.

## How input reaches the editor

A projectional editor has no text buffer, so the browser's text model is only an intake:

| What the user does | Path |
| --- | --- |
| printable character, IME, dead key | hidden `<textarea class="pn-composer">` → `input`/`compositionend` → `OnTextInput` → `KeyRouter.Type(session, text)` |
| a key the editor handles | JS checks `WebKeyMap.HandledChords` **synchronously** and calls `preventDefault`, then `OnKeyDown(key, ctrl, shift, alt)` → `WebKeyMap.Map` → `EditorDocument.Press` → `KeyRouter.For(session, key)` |
| anything else | not swallowed - browser shortcuts, F5, DevTools stay usable |
| Ctrl+V | deliberately **not** in `HandledChords`: the browser raises a native `paste`, JS prevents it, reads `clipboardData` and forwards `OnPaste(text)` |
| copy/cut | `CopyAction`/`CutAction` fill the shared clipboard; the shell pushes `clipboard.Text` to the OS through an off-screen textarea + `execCommand('copy')` (fallback: `navigator.clipboard.writeText`) |
| pointer | `@onpointerdown` on `.pn-paint` (children are `pointer-events: none`) with `preventDefault`, then `HitTest.Caret` with `offsetX / cellWidth` |

Why `preventDefault` and not a later check: `preventDefault` has to happen during the event, so the page
cannot wait for a .NET answer. `WebKeyMap` therefore answers two questions from one table - *is this a
key the editor acts on* (`Map`) and *must the page swallow it* (`IsHandled`). Paste is the one entry
where the answers differ, and that is documented on the property.

Pointer gestures:

- drag extends the selection from the anchor;
- Shift+click extends from the current anchor;
- double click → `SelectWordAction`, triple click → `SelectValueAction` (`@ondblclick`, `@onclick` with
  `Detail >= 3` - the pointer event's `Detail` is not reliable once `pointerdown` is prevented);
- after a click the hidden text area is focused through JS in a `setTimeout(0)`, because the browser's
  pointer default would otherwise move focus back to `body` one step later.

Folding, `Ctrl+B` (go to definition), `Ctrl+D` (duplicate), `Alt+↑/↓` (move node), `Ctrl+A` (the
value → node → document ladder) and `Ctrl+Space` (completion) all exist because they are the editing
layer's own keys. The completion popup accepts a row by `SelectCompletionAction(delta)` **only when the
delta is non-zero** - the first row is already selected when the list opens.

## The shell around the editor

`Shell/EditorShell.razor` + `.razor.cs` own the chrome and nothing the framework already owns:

- **tabs** with dirty dots, close, and a `+` menu;
- **menus** (File, Edit, View, Help): new document / example per language, open `.mps`, save, download,
  rename, close; undo/redo, clipboard, select all, delete, completion, go to definition; project
  explorer, problems, theme, zoom;
- **project explorer** (`Projectional.Web/DocumentOutline`): the active document's **model browser** and
  the languages with their concept counts. It walks the *model*, not the projection, so a concept
  without notation is still a row; rows are `Concept "name"`, indented by depth, with chevrons, Expand
  all / Collapse all / Reveal caret, and a click puts the caret on the node. Each row carries the
  concept's initial in the concept's colour - the desktop outline's palette and FNV-1a hash
  (`ConceptBadge`), so the same concept reads the same in both frontends. Open documents are **not**
  listed there: they are the tabs, exactly as on the desktop;
- **problems panel** from the language's constraints plus the framework's ambiguity rule
  (`DocumentProblems`; the desktop's `DocumentFindings` join lives in Avalonia and cannot be referenced),
  with rows that jump to the finding;
- **status bar**: language, node count, problem count, saved/unsaved, last message, zoom.

There is no capability registry here: `ShellCapability`/`CommandRegistry` live in
`Projectional.Avalonia` and a WASM host cannot load that assembly. The menus are methods on the
component. If the shell is ever extracted (ADR 0015, M6), the web shell should consume that library
instead of growing its own copy of the rules.

## Languages

Ten languages are offered:

- **code-declared** (document builders in `Projectional.Web`): Rules, StateMachine, FeatureModel,
  SqlSchema;
- **declared** (the folders under `samples/declared-*`): Atlas, DeclaredExpressions, Invoice, Pipeline,
  Units, Workflow.

The declared samples travel as **embedded resources** (`LogicalName="Examples/..."`). On first use they
are written into the runtime's in-memory file system
(`Path.GetTempPath()/projectional-web/examples`) and opened with `LanguageProject.Open(folder, resolver)`
- the same loader the desktop uses. A dependency is resolved by the language *name* in its
`language.mps`, so `Invoice` opens `Units` first. Because a dependent language's notation is spread over
its chain, `DeclaredSamples` merges the chain's concepts, notations (skipping a duplicate
concept+variant), constraints, types and scopes. Without that merge a base concept would draw as "no
notation".

C# is **not** offered as a standalone language: every C# concept is `isRoot: false`, so there is no
document root to open. It is a base language inside the declared projects.

## Persistence

`EditorWorkspace` + `DocumentStore` over `IDocumentStorage`; the browser implements it with synchronous
`localStorage` (`IJSInProcessRuntime`).

| Key | Content |
| --- | --- |
| `Projectional.Web.Documents` | one line per document: `id \t name \t language`, each URI-escaped |
| `Projectional.Web.Active` | the id of the tab in front |
| `Projectional.Web.Document.<id>` | the model's XML, byte-stable from `ModelXml.Serialize` |

- Autosave (`PersistActive`) runs after every edit, so a reload loses nothing; the dirty dot still means
  "not saved by the user" because it is derived from `History.Revision` against the last `Save()`.
- First visit opens the Rules and StateMachine examples.
- File ▸ Download `.mps` writes a Blob; File ▸ Open `.mps` reads through `InputFile` and tries the active
  language first, then every other language.
- An unknown stored language falls back to Rules rather than failing the restore; a corrupt index line is
  skipped.

## Deployment

The CI `docs` job builds `_site` (the language documentation) and publishes the sample beside it:

```bash
dotnet publish samples/web-app/Projectional.Web.App --configuration Release --output _editor
test -f _editor/wwwroot/index.html      # a Blazor publish nests the site in wwwroot/
mkdir -p _site/editor && cp -R _editor/wwwroot/. _site/editor/
```

`wwwroot/index.html` uses `<base href="./" />`, so the same build works at `/` in development and at
`/NodeLens.Avalonia/editor/` on GitHub Pages. The documentation pages link to the editor only when the
generator is given the href (`--docs` passes `editor/`); a packaged folder - which has no editor - gets
no dead link.

## Tests

`tests/Projectional.Web.Tests` (42 tests) cover what is easy to get wrong without a browser:

- `WebKeyMapTests` - the table, the swallowed set, and that `Ctrl+V` is mapped but not swallowed;
- `DocumentStoreTests` - the index codec, escaped names, corrupt lines;
- `EditorMarkupTests` - escaping, the caret element, a tab becoming one column;
- `EditorWorkspaceTests` - close/activate, save/restore (including the active tab), unique names;
- `EditorDocumentTests` - typing changes the model and dirties, undo clears dirty, accepting the
  already-highlighted completion does not throw, word selection;
- `WebLanguagesTests` - every offered language projects its example without a missing-notation cell and
  round-trips byte-identically;
- `DocumentOutlineTests` - document order, depth, collapse, ancestors.

Browser-only behaviour (focus, gestures, clipboard, deploy path) is verified by hand in the page; a
Playwright suite is not part of the repository.

## What is missing compared with the desktop shell

The desktop is the full workbench. The browser sample is a shell, not a port. Missing today:

| Area | Desktop | Browser |
| --- | --- | --- |
| Docking layout (drag/float/close panels) | `Dock.Avalonia` | fixed explorer + problems |
| Output, Usages, Terminal panels | yes | no |
| Find/Replace, Find Usages, Rename dialog | yes | no (Go to Definition exists) |
| Quick fixes / offers (`Alt+Enter`), intention preview | yes | no |
| Hover tooltips (concept, property, marker) | yes | no |
| Line numbers, fold gutter | yes | no (the session's folding API exists, no gesture) |
| Run, generate, language tests, import, export (C#/docs/package) | yes | no |
| Module settings, onboarding tour, recent files | yes | no |
| Unsaved-changes prompt on close | yes | **no** - closing a dirty tab discards it |
| C# as a standalone document | n/a (base language) | n/a (same) |
| Editing a declared language and re-opening it | yes | no |

Cheap next steps, in order of value:

1. an unsaved-changes prompt before closing a dirty tab (the only data-loss gap);
2. a find bar and `F3` using `Projectional.Editing.Finder`;
3. folding gestures wired to `EditorSession.ToggleCollapsed`, drawn in the model browser;
4. hover tooltips from `PropertyTip`/concept descriptions;
5. Run/Generate for languages whose `EvaluationRegistry`/generators are public - a browser cannot write
   files, so the output belongs in a panel rather than on disk.

## Where the code is

| Path | What |
| --- | --- |
| `samples/web-app/Projectional.Web/WebLanguage(s).cs` | what a language needs, and the ten offered |
| `…/DeclaredSamples.cs` | embedded declared folders → in-memory file system → `LanguageProject.Open` |
| `…/EditorDocument.cs` | one document: session, dirty, problems, frame, node selection |
| `…/EditorWorkspace.cs` | tabs, active document, save/restore, shared clipboard |
| `…/EditorMarkup.cs` | frame → HTML (runs, highlights, caret) |
| `…/WebKeyMap.cs` | browser key/chord → `EditorKey`, and what must be swallowed |
| `…/DocumentOutline.cs` | the model browser's rows |
| `…/DocumentProblems.cs` | constraints → rows + markers |
| `…/DocumentStore.cs` | the `localStorage` codec |
| `samples/web-app/Projectional.Web.App/Shell/EditorShell.razor` | the shell |
| `…/Shell/EditorInterop.cs` | the JS seam (measure, attach, focus, scroll, download, clipboard) |
| `…/wwwroot/js/editor.js` | listeners, font measurement, clipboard and file helpers |
| `tests/Projectional.Web.Tests/` | the headless tests |
