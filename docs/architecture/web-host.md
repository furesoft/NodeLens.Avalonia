# The browser editor

Status: **delivered** (sample). The code is `samples/web-app/`; the shell it hosts is `src/Projectional.Workbench`.

This document is the reference for the browser sample: how the Avalonia shell reaches a page, which decisions the
page makes as a host, and what still differs from the desktop. Read it before changing either head.

## One shell, two heads

ADR [0015](../adr/0015-shell-is-a-library.md) says the shell is *content plus a window*. The browser made that a
fact before M6: Avalonia's browser backend has a **single view** and no `Window`, so the content had to become a
`Control`.

```
Projectional.Core -> Language -> Projection -> Editing -> Avalonia -> Workbench
                                                                        |
                                          WorkbenchView (UserControl) <-+
                                             |                    |
                              MainWindow (Window, desktop)   samples/web-app (browser)
```

| Type | What it is |
| --- | --- |
| `WorkbenchView` | the shell: menus, docking layout, project explorer, outline, problems, usages, status bar, documents |
| `MainWindow` | the desktop head: a `Window` hosting the view, following its title, interrupting a close for the unsaved prompt, disposing the shell |
| `App` | the process's `Application`: creates `MainWindow` for a desktop lifetime and `WorkbenchView` for a single view |
| `samples/web-app` | the browser head: `Program.Main`, `wwwroot/index.html`, `wwwroot/main.js` |

The extraction is the ADR 0015 shape taken one step early but kept in the app: `WorkbenchView` is not yet the
library's `ShellView` (it still lives beside the workbench's own documents, modules and dialogs). When the M6
extraction lands, the browser consumes the library control unchanged.

`Projectional.Workbench` multi-targets:

- `net10.0` - `WinExe`, `Avalonia.Desktop`, the desktop `Program.Main`;
- `net10.0-browser` - `Library` for the head, no desktop backends, `Program.cs` excluded.

## How the page runs it

- Avalonia.Browser **12.1.6** (same version as Avalonia), `BrowserSingleViewLifetime`, host element `#out`.
- The shell asks its host through `App.SingleViewOptions` (storage, dialogs, capabilities) and reports back
  through `App.SingleViewCreated` (a saved file).
- Skia and HarfBuzz ship as **static WebAssembly libraries**, so the build relinks the runtime with them:
  `WasmBuildNative=true`, which requires `dotnet workload install wasm-tools`.
- **Trimming is off** (`PublishTrimmed=false`). The export compiles generated C# with Roslyn, whose reflection the
  trimmer cannot see; the size that costs is the price of shipping the whole workbench.
- **No JS fingerprinting**: `main.js` is named by the page and `js/shell.js` is imported by .NET, and neither side
  can discover a hashed name. The module import is `../js/shell.js` because the runtime resolves it relative to
  `_framework/` - which is also what makes the page work under `/editor/`.

## What the browser head supplies

| The shell asks | The page answers |
| --- | --- |
| `IShellStorage` (recent list, preferences) | `BrowserShellStorage`: `localStorage`, keys prefixed `projectional:` |
| a file or folder to open | `BrowserShellDialogs` + `BrowserFiles`: Avalonia's `BrowserStorageProvider` pickers, whose answer is copied into the .NET in-memory file system, and the path of that copy is returned |
| where to save | no picker - a path under the page's download folder; the shell raises `FileSaved` and the head sends the bytes to the browser (`js/shell.js`) |
| "Unsaved changes?" | `BrowserUnsavedPrompt`: a `Flyout` anchored to the shell; dismissing it answers `Cancel` |
| the examples menu | `ExampleLanguages`: embedded `samples/declared-*` written into `AppContext.BaseDirectory/Examples` at startup |
| capabilities | `ShellCapabilities.Standalone & ~ShellCapability.Terminal` |
| module settings | needs a modal `Window`; the shell reports that this host has none |

The terminal is the one module a browser cannot have: it starts a child process, and `Process` is unsupported on
WebAssembly. `TerminalProcess` guards every member with `OperatingSystem.IsBrowser()`, so the type compiles for the
browser target and refuses at run time instead of failing inside the runtime.

## What still differs from the desktop

| Area | Desktop | Browser |
| --- | --- | --- |
| Terminal | yes | no - no child processes |
| Module settings, and any modal dialog | yes | no (a message says this host has none) |
| Unsaved prompt | modal window | flyout |
| Save | writes a file | downloads a file |
| Open | reads the chosen path | copies the chosen file into the in-memory file system |
| Documents across a reload | files on disk | lost - only the recent list survives (in `localStorage`) |
| File ▸ Exit | closes the window | asks the host; nothing answers, because a tab is closed by the browser |
| Everything else - tabs, docking, outline, problems, usages, find, completion, quick fixes, intentions, run, tests, export | same code | same code |

## Deployment

The CI `docs` job publishes the head beside the documentation:

```bash
dotnet workload install wasm-tools
dotnet publish samples/web-app/Projectional.Browser.csproj --configuration Release --output _editor
test -f _editor/wwwroot/index.html
mkdir -p _site/editor && cp -R _editor/wwwroot/. _site/editor/
```

`wwwroot/index.html` uses `<base href="./" />`, so the same build works at `/` in development and at `/editor/`
on GitHub Pages.

## Tests

The headless suite is unchanged (`dotnet test` covers every layer the browser reuses). There is **no automated
browser test**: the published page is checked by hand - it paints, the menus open, `Recent`/`Save` come from
`localStorage`, and the examples open.

## Where the code is

| Path | What |
| --- | --- |
| `src/Projectional.Workbench/WorkbenchView.axaml(.cs)` | the shell content; `FileSaved`, `CloseRequested`, `ShellTitle` are its host seams |
| `…/MainWindow.axaml(.cs)` | the desktop head |
| `…/App.axaml.cs` | both lifetimes, and the two static hooks a single-view head uses |
| `…/Shell/WindowShellDialogs.cs` | the desktop's `IShellDialogs` |
| `samples/web-app/Program.cs` | the browser head: options, hooks, `StartBrowserAppAsync` |
| `…/BrowserShellStorage.cs` | `IShellStorage` over `localStorage` |
| `…/BrowserShellDialogs.cs` | pickers, virtual-file-system copies, the save path |
| `…/BrowserFiles.cs` | copy in, download out |
| `…/BrowserUnsavedPrompt.cs` | the flyout prompt |
| `…/ExampleLanguages.cs` | embedded examples to `Examples/` |
| `…/wwwroot/main.js` | the .NET bootstrap and the boot-screen removal |
| `…/wwwroot/js/shell.js` | `localStorage` and the download, as the module .NET imports |