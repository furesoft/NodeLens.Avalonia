# The browser sample

A Blazor WebAssembly page that edits the same projectional documents the desktop workbench edits, built
on the same headless layers. It is a **sample**, not a product: it is not packed, and it is deployed
beside the language documentation at `/editor/`.

Read [docs/architecture/web-host.md](../../docs/architecture/web-host.md) before changing it - it
records how the page paints and takes input, and the features that are still missing compared with the
desktop shell.

## Run it

```powershell
dotnet run --project samples/web-app/Projectional.Web.App
```

The development server prints the URL. Everything (documents, tabs, the active tab) is kept in the
browser's `localStorage`; `File ▸ Download .mps` and `File ▸ Open .mps…` move a document in and out.

## What is in the box

- **10 languages**: Rules, StateMachine, FeatureModel, SqlSchema (built in code), and the declared
  samples Atlas, DeclaredExpressions, Invoice, Pipeline, Units, Workflow (embedded resources, opened with
  `LanguageProject.Open` in the runtime's file system).
- **The editor**: painted from `EditorFrame`, caret and hit test from the same layout, completion,
  go to definition, undo/redo, drag/word/value selection, copy/cut/paste with the system clipboard.
- **The shell**: tabs, menus, status bar, project explorer with a model browser, problems panel.
- **Persistence**: autosave, explicit save, download/upload.

## Test it

```powershell
dotnet test tests/Projectional.Web.Tests
```

The tests cover the host's pure logic (keys, markup, storage, workspace, languages, outline). The page's
browser behaviour - focus, gestures, clipboard, the deploy sub-path - is checked by hand.

## Publish it

```powershell
dotnet publish samples/web-app/Projectional.Web.App --configuration Release --output artifacts/editor
```

A Blazor publish nests the site in `wwwroot/`, so the served folder is `artifacts/editor/wwwroot`.
`wwwroot/index.html` uses `<base href="./" />`, which is what lets the same build work at `/` and under
`/editor/`.

## Layout

| Folder | What |
| --- | --- |
| `Projectional.Web/` | the headless host: languages, documents, workspace, markup, key map, storage, outline |
| `Projectional.Web.App/` | the Blazor page: shell component, interop seam, CSS and `js/editor.js` |
