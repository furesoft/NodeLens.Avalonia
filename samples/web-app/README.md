# The browser sample

The Avalonia WebAssembly head: it hosts the **same shell** the desktop app shows - `WorkbenchView` from
`src/Projectional.Workbench` - so the page has the tabs, docking layout, project explorer, outline, problems,
menus and status bar, not a second, thinner shell. It is a **sample**, not a product: it is not packed, and it is
deployed beside the language documentation at `/editor/`.

Read [docs/architecture/web-host.md](../../docs/architecture/web-host.md) before changing it - it records how the
Avalonia shell reaches a page, which decisions this head makes, and what still differs from the desktop.

## Run it

```powershell
dotnet workload install wasm-tools   # Emscripten, for relinking Skia and HarfBuzz
dotnet run --project samples/web-app/Projectional.Browser.csproj
```

The development server prints the URL. Building for the browser requires the `wasm-tools` workload, and the
publish relinks the runtime (`WasmBuildNative`); trimming is off because the export compiles generated C# with
Roslyn.

## What is in the box

- **The desktop shell**: menus, docking panels (project explorer, outline, problems, usages), tabs, status bar -
  the same `WorkbenchView` the window hosts.
- **The editor**: caret, selection, completion, quick fixes, intentions, find usages, undo/redo, clipboard.
- **Languages**: the built-in ones (Rules, StateMachine, FeatureModel, SqlSchema) and the declared samples
  (Atlas, DeclaredExpressions, Invoice, Pipeline, Units, Workflow), embedded in the head and written into the
  runtime's file system at startup.
- **Storage**: the shell's recent list lives in `localStorage`; documents live in the in-memory file system.
  Opening a file copies it in; saving downloads it.
- **Not offered**: the terminal (`Process` is unsupported in WebAssembly) and modal dialogs such as module
  settings. The unsaved-changes prompt is a flyout.

## Publish

```powershell
dotnet publish samples/web-app/Projectional.Browser.csproj --configuration Release --output _editor
```

The served site is `_editor/wwwroot/`; a static server rooted there (or a copy to `_site/editor/`) is enough.