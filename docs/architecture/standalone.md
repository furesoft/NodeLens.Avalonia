# Standalone editor

[ADR 0011](../adr/0011-standalone-editor.md) decided that a desktop application ships
alongside the embeddable control and is an ordinary consumer of the public API. This
document describes what that application is, how it is composed, and what it must not
become.

The application is `Projectional.Workbench`. It is the same executable from M1 to 1.0;
what changes is how much of a workbench it is.

## Four jobs, in this order

| Job | Why it matters | Milestone |
| --- | --- | --- |
| **Reference host** | Exercises the public API exactly as a customer would, so integration gaps surface during development | M1 |
| **Try-before-embed** | Someone evaluates the framework by running an editor, not by writing a host | M1 |
| **Daily DSL tool** | Ordinary use of a shipped language: open, edit, validate, generate | M2 |
| **Language workbench** | Authoring languages in their own notation, with live preview | M5 |

The order is a constraint on scope, not just priority: a feature that serves the fourth
job before the first two exist is a premature feature. Every milestone adds to the same
shell.

## What the shell is made of

```text
┌─ Workbench ──────────────────────────────────────────────────────┐
│ menu bar        File · Edit · View · Language · Help             │
│ toolbar         open · save · undo · redo · validate · generate   │
├──────────────┬───────────────────────────────────────────────────┤
│ side panel   │  document tabs                                    │
│              ├───────────────────────────────────────────────────┤
│ model        │                                                   │
│ browser      │  editor pane  (ProjectionalEditorControl)          │
│              │                                                   │
│ language     │                                                   │
│ browser      │                                                   │
├──────────────┴───────────────────────────────────────────────────┤
│ problems · output · find            ln 3, col 12   Rules  UTF-8  │
└──────────────────────────────────────────────────────────────────┘
```

| Part | Responsibility | Appears |
| --- | --- | --- |
| `App` / `Program` | lifetime, service wiring, theme, unhandled exception reporting | M1 |
| `MainWindow` | layout skeleton, menus, the panel area and its splitter, status bar | M1 |
| `DocumentTab` + `DocumentSet` | open documents, active document, per-document session and undo | M1 |
| `ProjectDriver` | open a project, enumerate models, save, watch files | M1 |
| `CommandRegistry` | named commands with `CanExecute` and a shortcut table | M1 |
| model browser | tree of models and their root elements - **delivered** as a resizable left column (a splitter and a `MinWidth`) listing every node of the active document, its header naming that document, clicking a row puts the caret on it (`Outline/`) | M2 |
| language browser | declared languages in the project, open their definitions | M5 |
| problems panel | diagnostics of the active document, in document order, jumping to the cell the finding is about - **delivered** in `MainWindow` + `Problems/` (M2) | M2 |
| recent files | the last ten opened or saved files, remembered between runs - **delivered** in `Documents/RecentFiles` + `TextRecentFilesStore` | M2 |
| output panel | generator and interpreter results, traces | M4 |
| live preview | split a language definition against a model using it | M5 |

Everything in that table is a plain class with no Avalonia dependency except the
window and the two controls, so the shell's behaviour is testable headlessly. The
window is wiring.

**Where the extraction stands.** `ShellCapability`/`ShellCapabilities` and `ShellOptions` with its
`Capabilities`, `InitialModel`, `Storage` and `Dialogs` are public in `Projectional.Avalonia/Shell/`,
together with `IShellStorage`, `IShellDialogs`, `ShellFileType`, `UnsavedChoice`, `ShellCommand` and
`CommandRegistry`; the file-system and window implementations stay in the app. `CommandRegistry` is the
shell's **one enforcement point**: `MainWindow` declares its action surface in `RegisterCommands` and its
handlers dispatch through the registry, so a menu entry, a shortcut and a host's own button agree by
construction. `ProjectDriver`, `ShellView` and `ShellWindow` still do not exist - the window is still the
shell ([embedded-shell.md](embedded-shell.md#delivered-so-far-the-environment-seams),
[ADR 0015](../adr/0015-shell-is-a-library.md)).

## Composition rules

1. **The app never mutates a model directly.** It goes through `EditorSession` and the
   public command API. A model change from the app that cannot be made through the
   public API is a missing public API, not a reason for a back door
   ([ADR 0011](../adr/0011-standalone-editor.md)).
2. **The shell knows about documents, not trees.** Nothing in the app inspects editor
   cells except the control itself.
3. **Sessions outlive tabs, tabs outlive windows.** A document keeps its session and
   undo history while it is open; closing the document disposes the session. This is
   the disposal rule hosts get wrong most often, so the app demonstrates it.
4. **One writer.** Every model mutation happens on the UI thread. Background work
   (validation, generation) goes through the snapshot API
   ([overview.md](overview.md#threading-model)).
5. **No feature exists only in the app.** If the app can do something a host cannot,
   either the public API grows or the feature is removed. This invariant is the whole
   point — without it the app silently becomes the only supported front end.
6. **The shell is a library; the window and the application are its configuration.**
   The shell's content is a `ShellView` control, `ShellWindow` wraps it, and the standalone app is
   `ShellWindow` with every capability and no language lock. That is what lets a host embed the
   whole shell, locked to one language and saving into its own storage, instead of rebuilding it
   ([embedded-shell.md](embedded-shell.md), [ADR 0015](../adr/0015-shell-is-a-library.md)).
7. **No process-wide state, and no capability enforced only by hidden UI.** A shell instance
   owns its documents, its preferences and its capability set; the menu reflects a capability and
   the command refuses it. Two shells in one process must not see each other, and an embedded
   instance must not write a settings file the app did not ask for
   ([embedded-shell.md](embedded-shell.md#lifetime)).
8. **A type the app uses must arrive through a public member, not through a reference.**
   The app names `MarkerSet` and `Diagnostic` when it publishes findings, and it does so
   with **no** `ProjectReference` of its own: `ProjectionalEditorControl.Markers` is
   public and is typed with them, so `Avalonia` must reference `Editing`, which must
   reference `Semantics`. `Semantics` sits in the same band as `Projection`/`Editing`,
   below Avalonia and therefore below the app, so the arrow points down and is legal.
   The test is whether a `ProjectReference` would have to be *added*: if the app can
   express the feature with members `Projectional.Avalonia` already makes public, it is
   allowed; if it needs a reference to see a type, it is reaching past the control and
   the public surface is missing something.

### The languages the app knows

`WorkbenchLanguage` (`Documents/`) is the one registry and the one notation registry every document of the app is
built and loaded against, and it composes **three** languages: the sample rules language, and the two the editor is
written with - `Projectional.Structures` and `Projectional.Editors`.

- **Composition, not a mode.** A project holds one registry, so a document the app can open has to be covered by
  one list. That is why the definition languages are registered up front rather than switched to: a
  a concept document opens, edits and saves by the same code path as the payroll sample, with no second editor and no
  flag.
- **The transitive closure is the host's job.** `RulesLanguage.ConceptDeclarations` is one language's *own*
  concepts; the rules language borrows `Expressions.Expression`, so a host composing it has to bring
  `ExpressionsLanguage.ConceptDeclarations` too. Forgetting it is a `ConceptDefinitionException` at startup naming
  the concept, which is a better failure than a file that only breaks when someone opens it.
- **`EveryConceptOfAllThreeHasANotation`** guards the other half: a registered concept with no notation still
  opens, as a `MissingNotationCell`, and that is the one state a user cannot work around. The test walks the
  concrete concepts of the composed registry and demands a notation for each - it found `Editor.Collection`
  straight away.
- **File ▸ New language definition** and **New notation** open an empty document of each, so the declared path can
  be tried without setup. They are conveniences: the languages are loaded either way.
- **The set is per shell and grows.** `WorkbenchLanguage` is instance state, so two shells in one process never see
  each other's languages (ADR 0015). **File ▸ Open language…** loads a folder and takes it in
  ([language-projects.md](language-projects.md)), and **New document of an opened language** creates a document of
  its first rootable concept. A refused language leaves the shell untouched, and the menu only offers languages that
  actually joined - the shell must never create a document it would then be unable to load.
- **File ▸ Export language to C#…** writes an opened language as the package it would ship as: the generated
  `…Language.cs`, its README and the assembly they compile to, into the folder the user picks
  ([language-authoring.md](language-authoring.md#export-declared-to-c)). The submenu offers the opened languages by
  name, because which language a document belongs to cannot be read off the document - a document may borrow
  concepts. `LanguageExportWriter` does the writing and is tested headlessly; the window only picks the folder and
  reports the result, including a compilation failure, which means the export cannot spell something the language
  declares.
- **The references this adds are language packages**, which is the one category rule 8 above allows: `Structures`
  and `Editors` sit on `Language` exactly like `Samples`, and the app already references three sample packages. No
  reference to `Core`, `Projection`, `Editing`, `Semantics` or `Persistence` was added for this.

## Document lifecycle

```text
closed ──open──▶ loading ──▶ clean ⇄ dirty
                                │  │
                        save ───┘  └─── prompt ──▶ discard ──▶ closed
                                              └──▶ cancel
```

| State | Meaning | Transition |
| --- | --- | --- |
| `loading` | project and model read from disk | success → `clean`; failure → error dialog, back to `closed` |
| `clean` | session exists, no unsaved change | first change → `dirty` |
| `dirty` | the history is at a state no save has recorded | save → `clean` |
| `prompt` | close requested on a dirty document | save / discard / cancel |

The dirty flag is **derived from the session**, not set by a flag the app maintains, so
undo back to the saved state returns to `clean`. A boolean "dirty" flag that is set on
any edit and cleared on save is the classic bug: undo to the original state still claims
the document is modified.

The derivation compares `UndoHistory.Revision` against the number recorded at the last
save. The count of undo steps is not enough: edit, save, undo, then edit something
different leaves the depth where the save left it while the document is no longer the
saved one. Revision identifies the state instead of counting changes to it, so a redo
back onto the saved step is clean again.

A transaction that coalesces into the last step moves that step's revision on, so a save
in the middle of a typing burst is not mistaken for a clean document: the burst goes on
to change content the file never saw. Saving clears the pending coalescing key as well,
so the next keystroke starts a step of its own instead of silently joining the saved one
— a save is an undo boundary like any other.

External changes are detected by a file watcher and answered with a prompt ("reload
from disk / keep my version"), never silently — see
[open questions](../open-questions.md#standalone-editor) #23.

## Commands

A command has a name, a shortcut, an `Execute`, and a `CanExecute` that reads the
active document. The menu, the toolbar and the shortcut table all bind to the same
command object, so a command cannot be enabled in one place and dead in another.

| Command | Shortcut | `CanExecute` |
| --- | --- | --- |
| New project / New model | `Ctrl+N` | always |
| Open project / Open model | `Ctrl+O` | always |
| Save / Save all | `Ctrl+S` / `Ctrl+Shift+S` | active document dirty |
| Close document | `Ctrl+W` | a document is open |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` | session reports availability |
| Find | `Ctrl+F` - **delivered** as View ▸ Find..., a menu `HotKey` in the app; the command it runs (`"find"`) is a registry entry | a document is open |
| Validate project | `F5` | a project is open |
| Generate | `Ctrl+G` | language provides a generator |
| Next / previous problem | `F8` / `Shift+F8` | diagnostics exist |

Routing rule: the **focused session** receives editing commands; application commands
(open, save, close) act on the active document. A key that the session handles must not
also trigger a command, which is why the control reports unhandled keys rather than
swallowing them.

### Recent files

**File ▸ Recent** lists the files opened lately, newest first. `Documents/RecentFiles.cs` is the list and
`Documents/TextRecentFilesStore.cs` is where it is kept: a text file under `%LOCALAPPDATA%\Projectional\recent.txt`,
one path per line.

- **The list owns the rules, the store owns the bytes.** De-duplication, order and the ten-entry cap are the
  record's, so "what counts as the same file" is answered once; comparison is case-insensitive because a Windows path
  is, and a list that showed `C:\a.mps` twice would lie about what the user did.
- **What comes back is validated, not trusted.** A file a user can edit is one they will edit, so `RecentFiles.From`
  drops blanks, repeats and anything past the cap.
- **Every store failure is silent** - missing file, unreadable file, failed write - and answered with what is still
  true. A shell that refused to start over its own convenience file would be losing work to keep a menu.
- **One open path.** The picker and the recent entry both call `TryOpenPath`, so a missing file, an already-open
  document and a language mismatch are handled in one place. A file that can no longer be opened is **dropped** from
  the list: a menu that keeps offering a click that fails is worse than a shorter menu. Saving puts the file at the
  top.
- **The entry is disabled, not hidden, when the list is empty**, so the menu does not change shape depending on what
  the user did last.

### The model browser

A left panel lists the active document's structure as a tree: every node, depth first, indented by its depth, named by
its concept and - when it has one - its name ("Rule \"Approved\""). Clicking a row puts the caret on the node.
`Outline/Outline.cs` builds the rows and `Shell/Panels/OutlinePanelView.axaml` draws them; the window keeps the set of
shut nodes and decides what a press means.

- **It walks the model, not the projection** - the exact opposite of the find bar, and for the opposite reason. A
  browser answers "what is this document made of", so a concept whose notation is missing is still part of it;
  hiding it would make the browser disagree with the tree it describes. Find answers "where is this text on the
  page", which only the projection knows.
- **Depth is counted, not stored.** Depth is a property of a place, and a node can be moved.
- **A tree, not a flat list.** A document the size of a ruleset is read by pattern, and folding the parts that are
  already understood is how a reader keeps the part they are working on in view. A collapsed node's children are not
  built at all, so the rows cannot drift from the tree they describe; the set of shut nodes is the window's view state,
  keyed by node id and reset when the document in front changes, because an id means nothing in another model.
- **A row is drawn as the concept it is**: the concept's initial in a colour that concept owns, both derived from its
  key by a written-out hash - the same concept is the same colour in every document, and in every process, which is what
  makes the colour worth having. It is a grouping cue rather than an identity: the palette is small, so two concepts can
  share a colour.
- **Rebuilt on every model change**, like the findings. A browser that had to be refreshed by a command is one the
  user forgets to refresh; the cost is one walk of the active model, the same order as the checks that already run
  there.

The header is the tool window's own, and the tab above it names the tool: the header names the **document** the rows
describe and carries the two actions the browser has. The eye moves the tree to the node the caret is in - opening the
nodes above it, because a collapsed ancestor is exactly when the row is not there to be found - and it does **not** move
the caret, which is why the selection it makes is flagged. The kebab offers **Expand all** and **Collapse all**, and
`Outline.NodesWithChildren` answers the second from the model rather than from the rows, since a collapsed tree no longer
names what is below it.

A press that lands on a chevron folds and does not navigate: the list selects on the press and the button fires on the
release, so the selection change in between is the one the browser ignores. Without that guard, folding a row would move
the caret - the same bug the project explorer cannot have, because a selection there has no side effect.

### The project explorer

The other tab of the left column answers the question one level above the browser's: what the languages opened from a
folder are made of. `Project/ProjectExplorer.cs` derives the rows - a heading per language, its aspect folders and the
definition files inside them - and `Shell/Panels/ProjectPanelView.axaml` draws them; the window only decides what a
press means.

**An open document is not a row.** The document strip and the title bar already say what is open, and a second list of
the same thing only pushes the folders out of view. What the open documents are still needed for is a *file* row, which
learns from them that it is open and whether it is modified - that is where the star beside a name comes from. A panel
with nothing in it says so and names **File ▸ Open language…**, because the next step is a menu entry the user would
otherwise have to guess.

- **A row says what it is drawn as.** One colour per aspect, the aspect's own glyph on a folder, and a letter badge on
  a file naming the kind of definition that aspect holds - a green `C` under `structure`, an amber `E` under `editor`.
  A project is drawn heavier than its rows and a heading carries no mark at all. It is the convention the tools this
  editor is modelled on use, and it is why the tree can be read without being read word by word.
- **The marks are tables of strings, not of geometries.** `Project/ProjectIcons.cs` holds the glyph paths, the colours
  and the letters and parses a geometry only when a row is first painted: a row has to answer "what will I be drawn
  as" while the tests build it headlessly, and a `StreamGeometry` needs a rendering platform. An aspect the table does
  not know falls back to the page the row really is rather than to somebody else's badge.
- **A folder row carries the aspect, a file row carries the folder it sits in**, which is what lets a definition read
  as "a concept of the structure" rather than as "a file called `Pipeline.Stage.mps`".
- **Expansion state is the window's.** A row is a value and a collapsed node's children are not built at all, so
  flipping one key and deriving again is what keeps the panel from drifting away from its own state.

**The context menu belongs to the panel, not to a row**, because a module is what the whole tree is about - and
`Module settings…` is its entry. A right press selects the row it landed on before the menu opens
(`OnProjectPointerPressed`), so the command is about what the user pointed at rather than about whatever happened to be
selected; the module is then the row's own, or the language a document row's model was written in, and only when
neither answers does the single opened module stand in.

### Module settings

**Module settings…** opens a dialog about the language the tree is pointed at, with four pages: the module's identity
and version numbers, a dependencies table (which aspect is written in which language, the absent aspects included),
what the shell lowered out of the definitions, and where its documentation goes.

- **Almost all of it is derived, and that is the point.** `Modules/ModuleFacts.cs` reads the folder and asks the
  `LanguageProject` what it built: a language with two hundred declarations and no notation cannot be drawn, and
  `Notations 0` is how an author finds that out without opening a document and wondering why the page is empty.
  The contents page opens with what the module *declares* about itself - the version in `language.mps` and the two
  lists it stands on - because a declared version that nothing enforces still has to be visible somewhere.
- **Every aspect is listed, empty ones included.** The explorer hides an absent folder because a tree is about what is
  there; settings are about what could be, and "no constraints" is a fact worth reading.
- **Three settings are the author's**: the language version, the module version and the documentation target. They are
  written to `module.settings` inside the module's folder - so a folder copied to another machine carries them - in the
  same `key = value` spelling the recent list uses: a file a user can fix by hand, read tolerantly, written with LF and
  UTF-8 without a BOM so two machines produce the same bytes.
- **A refused write keeps the dialog open and says so.** Closing on OK after a write the disk rejected would be the
  shell reporting a change that lives only in a control nobody will look at again.
- **The palette is shared, not copied.** `Styles/WorkbenchPalette.axaml` holds the surfaces and the chrome's type size
  and is merged into both windows; two windows carrying their own copies would drift apart at the first change. The
  dialog also takes the theme variant of the window it belongs to, or it would flash white over a dark shell.

### Find

**View ▸ Find...** (`Ctrl+F`) opens a strip above the document with a query box, a match-case toggle, the match
count and Previous/Next. `Projectional.Editing/Find/` is the whole of it:
`Finder.Find` answers the occurrences, `FindNextAction` selects the next or previous one, and the app only counts
and scrolls.

- **A strip, not a dialog.** A search is repeated, so it must not take the caret away from the document it
  searches; the count stays visible while the user reads the result.
- **The search walks caret places, not the model**, because "where is this text" is a question about the page -
  and a property the notation hides is therefore not found, which is the rule the caret already obeys. Only
  property cells are searched: a keyword is drawn from the notation, so a match in one would be an offer no edit
  can keep.
- **Enter is forward, Shift+Enter backward, Escape gives the keyboard back** - the same three gestures as
  everywhere else, and the reason the bar needs no buttons to be usable.
- **The count is asked on every change**, never remembered: the query and the document both move.

Replace is part of it, and it is an **edit**, which is why it lives in `Projectional.Editing/Find/` and not
in the bar: `ReplaceMatchAction` rewrites one value's text and keeps everything else about the node, and
`ReplaceAllAction` does every occurrence as one transaction - one undo press, not one per occurrence. Two rules
in them are decisions:

- **Each cell is rewritten once**, with its replacements applied right to left, so offsets counted against the
  original text stay valid and three occurrences in one cell cost one property write.
- **A value the property type refuses is skipped and counted, and a run that could not write anything is rolled
  back.** Aborting a whole run because of one cell would be worse, but an empty run must not leave a step in the
  history.

The bar's Replace replaces the selection when it is exactly an occurrence and otherwise moves to the next one,
because a button that stays dead until the user has searched is one they cannot use to start.

### Find usages

**View ▸ Find usages** (`Alt+F7`) answers "who points at this node" and lists the rows in a tab of the panel area.
`FindUsages.ReportOf(session)` is the whole question; `UsageList` turns the answer into rows and
the window only fills a list, moves the caret and says why the panel closed.

- **The gesture is the window's**, like F5: the answer is a shell panel, not something the control can paint over
  the document. A host embedding the control calls the same `FindUsages` entry point and owes its own list.
- **The panel stays up only while its target exists.** An edit re-asks about the *same node* (the id is kept, not
  the caret), a deleted target closes it, and switching to a document of another project closes it too. Its tab's
  close button is the deliberate way to give the page its height back, and **View ▸ Panels** brings the answer
  back without asking the caret again.
- **An empty list is shown.** Unlike the problems panel, "nothing points here" is the answer the user asked for.
- **A usage in another model says which model**, because a project is several documents; choosing the row opens
  that document's tab when it is open, and says so when it is not. A reference the notation hides has no cell, so
  the row falls back to the owner node rather than doing nothing.

### Rename

**Edit ▸ Rename...** (`Shift+F6`) opens a bar prefilled with the name of the node the caret names - the same reading
Find usages uses, so a caret on a reference renames the declaration it points at. Enter applies it, Escape and Cancel
close the bar, and a refusal leaves the bar open with the reason in the status bar so the value can be corrected.

The edit is `RenameNodeAction` (`Projectional.Editing/`): the name and every reference fallback in the document are
one undo step. A node of another model is refused, because this document's history is what would have to undo it.
`INT001` in the context menu stays the caret jump into the name slot; the project-wide rename is the shell command,
because an intention cannot ask for a value.

### Go to definition

`Ctrl+B` and `Ctrl+click` on a reference move the caret to the declaration it names, with the name selected; the
context menu offers the same entry, disabled where the declaration is not drawn in the document. The action is
`GoToDefinitionAction` (`Projectional.Editing/`), so a host gets the same navigation from its own gesture - and a
target in another document is a rejection the host may answer by opening that document.

### Representations

The one command the shell has that is a projection and not a document operation lives in
**View ▸ Representation**. Its submenu is built from the active document, not from the
registry: `EditorSession.Alternatives()` answers which concepts the document actually uses
that resolve to more than one notation variant, so a menu never offers a table form for a
concept nobody wrote. Each concept gets one submenu, each variant one radio item, and the
selected one is checked. Picking another calls `ChooseVariant` and refreshes that document's
control; the model, the history and the caret are untouched, because a variant is view state
and the shown slots keep their addresses.

Consequence for the shell: two tabs can show the same concept differently, and nothing in
the document has to be saved to remember it. A future slice may persist the preference per
file, which is a session-level setting rather than a model property — the same reason
dark/light is not written into the model.

### Theme

**View ▸ Follow system theme / Light theme / Dark theme** is one group of three radio items,
and the shell starts on the first of them: a window that has just opened should look like the
desktop it opened on, not like a guess. The status bar names the decision rather than the
result, so `dark theme (system)` says the window is dark *because the desktop is* - the user
who wonders why the editor turned dark at sunset finds the answer where they were already
looking. A manual choice pins the variant and keeps it through an OS switch, which is the
point of offering the choice at all.

Two rules make this cheap enough to be a shell preference rather than a document property:

- **The shell decides the variant, the editor decides the colours.** `RequestedThemeVariant` is
  set on the window, so the toolkit's own chrome follows along, and the editor is then asked
  for the palette that matches the *resolved* variant: `SyntaxTheme.Of(ActualThemeVariant)`.
  Reading the resolved variant rather than the requested one is what makes pinning light on a
  dark desktop produce a light window **and** a light document from one call.
- **Nothing about a theme reaches the model, the history or the file.** A theme is view state
  for the same reason a notation variant is, so switching one repaints and does not dirty the
  document. `SyntaxTheme.Of` is public in `Projectional.Avalonia`, so a host that embeds the
  control gets the same mapping - including the inheritance walk that reads a host-defined
  `new ThemeVariant("Midnight", ThemeVariant.Dark)` as dark.

A theme carries **every colour the control paints**, including the ones that are not the document:
`Palette`, `Background`, `Chrome`, `Caret`, `Highlight`, `Gutter`, `LineNumber`, `Markers`,
`Lists` (the completion and quick-fix lists), `Tip` (the marker tooltip) and `Bulb`. The last three
were added after the first version shipped a white popup on a dark page: the surfaces that are
*painted over* the document are read against it, so a host that themes the page and not its overlays
has a bug the user can see. Applying a theme is one method on the window, and a tab opened later is
themed before it is shown - both paths go through the same call, so a new member cannot be applied
to one and forgotten in the other.

### Line numbers

**View ▸ Line numbers** is a checkable item, off at first, that sets `ShowLineNumbers` on
every tab's control; a document opened afterwards inherits whatever the flag currently is.
It is a shell preference and not a per-document one, so switching tabs does not switch the
gutter on and off, and nothing about it reaches the model or the undo history - see
[line-numbers.md](line-numbers.md). The colours come from `SyntaxTheme`, like the palette, so
the dark theme does not paint the numbers in light-theme grey.

### Font size

**View ▸ Increase / Decrease / Reset font size** step one shared size by one pixel between 6 and
48, and `Ctrl+=`, `Ctrl+-` and `Ctrl+0` do the same. Like the gutter and the theme it is a shell
preference: one field on `MainWindow`, applied to every open tab and to any document opened
afterwards, never written into the model. A document created while zoomed is given the size
before its `DocumentTab` is built, so it is laid out once at the right size instead of twice -
see [font-size.md](font-size.md).

The gesture is written into the menu header rather than declared as a `HotKey`. The key that
types `+` is `OemPlus` with `Shift`, so one registered gesture cannot match both the shifted and
the unshifted press, and `KeyGesture` would print the enum name to the user. The handler is
registered for the tunnelling phase, because the editor control claims keys of its own and a
bubbling handler would race it.

### Column widths

A table's columns can be dragged wider by grabbing the right edge of a column in the header row;
the pointer turns into a horizontal-resize arrow over it. **View ▸ Reset column widths** puts every
column in the active document back to the width its content asks for.

The width is view state, like the representation and the fold set: it is never saved, it creates no
undo step and it does not mark the document modified. It is keyed by the node id and the column, so
it survives a rebuild and an undo that moves nodes around. The width is a **floor** - a column
narrower than its widest cell is drawn at its widest cell, because clipping a value would hide what
the model says. The rule that finds the boundary is `ColumnResize.HandleAt` in
`Projectional.Editing`, not in the control, so a host that draws its own surface gets it for free;
see [projection.md](projection.md#tables).

### Findings

The app publishes its own findings into the editor, and it is the reference for how a host does it.
Two checks run: `Checks/DefaultNames.cs` marks every node still called its concept's placeholder
(`"unnamed"`), and `Projectional.Samples.Checks` reports two rules of one ruleset that share a name.
`MainWindow.PublishFindings` hands the result to the control's public `Markers` and `Fixes` properties
when a document opens and again from the model's `Changed` handler.

Both checks answer with fixes rather than with bare diagnostics, and the window derives the marker set
from them: a fix carries the finding it repairs, so one list produces both sets and they cannot
describe different states of the document.

### The docked window

The window's work area is a docking layout rather than a grid of splitters: the project explorer and the model browser
are tools of a left dock, Output, Problems and Usages are tools of a bottom dock, and the open documents are the
layout's document dock. `Shell/ShellLayout.cs` builds that tree, and the window's markup holds the docking control and
nothing else - a tree of dockables is a model, so it is built where a model belongs.

- **The panels are not a special case any more.** Dragging a tool to another edge, floating it as its own window and
  closing it are the library's gestures, so a panel behaves the way every other panel behaves rather than the way its
  own tab strip was written to. The three bottom answers still share one strip, because that is a *default*, not a
  constraint.
- **A closed tool is hidden by the layout.** Dock keeps what a user closed in the root's hidden list and restores it
  where it was, so View ▸ Panels is a restore rather than a rebuild - and the flags the window keeps (`_outputClosed`,
  `_problemsClosed`, `_usagesClosed`) are the content's opinion, not the presentation's. That split is the one the
  previous tab strip needed and this one still needs: a re-derived list must not reopen a panel the user put away.
- **The document area never collapses.** A dock that may collapse is removed from the layout when its last document
  closes, and the empty state it was given never shows - the area simply disappears and the neighbouring tool takes its
  width. The document dock therefore keeps its place, and `EmptyContent` is the "no document open" page instead of a
  control the window switches on and off.
- **Which panels exist is decided once.** The layout is composed from the capability set: a module a host did not grant
  is not built, not merely hidden, and a dock with no tools takes no width because it is not in the tree at all. That is
  the same rule the menus follow - hiding is presentation, refusing is the guarantee (ADR 0015).
- **The window reads the layout instead of a tab strip.** Which document is in front and which tool the user closed are
  notifications from the docking model (`ActiveDockableChanged`, `DockableClosing`), so the window no longer keeps a
  parallel idea of either. A close that would lose work is *cancelled* and re-issued after the prompt, because a
  question cannot be awaited inside a closing handler.
- **The theme is declared on the application**, not on the window: a floated panel is a window of its own, and the
  screenshot harness has to load it too - a dock control with no dock theme has no template at all and renders as an
  empty rectangle, which is exactly what the first picture of this layout was.

### The problems panel
### The problems panel

`Problems/ProblemList.cs` turns that same list into rows, and `MainWindow` shows them in a tab of the panel area.
The panel reads the findings the editor is drawing rather than running the checks again, so a squiggle and a
row cannot disagree about what was found.

- **The order is the document's, not the severity's.** A list sorted by severity renumbers itself while the
  user fixes things - fixing one row moves every row after it - so the severity travels with each row
  (`ProblemEntry.SeverityLabel`) and the position decides the order.
- **The row carries a `CellAddress`, not a node id.** A finding about a slot is a finding about a place, and
  the panel's whole job after reporting is to put the caret there: choosing a row is `session.Select(...)`
  and nothing else, which is why that half is tested headlessly and only the scrolling needs a window.
- **A finding whose node is gone still gets a row, at the end.** The set is a snapshot taken when the model
  changed; a panel that hid a stale finding would be claiming the document is clean at the moment it is
  least known to be.
- **An empty list hides the tab.** "No problems" is the state of a document most of the time, and a tab that
  said so would take a strip of the window to say nothing; the count is in the tab's header for the one glance
  that needs it.

Three things that follow, and that a host has to do as well:

- **The sets are a snapshot, so they must be republished on every change.** The editor keeps no
  reference to a check; a set published once describes the document as it was then. Republishing
  is a repaint, not a rebuild — the tree and the layout stay as they are - and republishing only half
  of it is how a squiggle keeps offering a cure for something the user already fixed.
- **A check reads the language, not the app.** `DefaultNames` compares a name against
  `Concept.NameSlot.DefaultValue`, so it needs no per-concept table and no knowledge of the
  toy rules language beyond what the declaration already says. A check written against strings
  copied out of the sample would go out of date the moment a language default changed.
- **The severity is a decision.** The placeholder is a `Hint`, not a `Warning`: an unnamed rule
  is a document that is not finished, not a document that is broken. The palette draws `Hint`
  and `Info` as a dotted line and `Warning`/`Error` as a wave, so the severity is visible
  before the tooltip is opened.

Nothing about this needs a reference of its own — see rule 8 under
[Composition rules](#composition-rules). The app is using `Projectional.Avalonia`'s public
surface exactly as a host would.

### Intentions

`MainWindow.RegisterIntentions` is the whole of what the app writes for the light bulb: one catalogue
provider and three language providers, registered once per document.

```csharp
document.Session.AddIntentions(StandardIntentions.Provider);
document.Session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);
document.Session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);
document.Session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
```

The first is `Projectional.Intentions`: the framework's own catalogue, which derives rename, duplicate,
delete, the moves, both insertions, sorting, the two selections, wrap, replace and reset from the shape of the
caret's place, so a document gains them without its language doing anything. It is registered **as the host's**
because the app is what chose it, and host entries are listed before a language's - the generic operations above
the specific ones. The other three are the languages' answers: `Projectional.Expressions.Intentions` (negate),
`Projectional.Samples.Intentions` (accept this rule) and `Projectional.Structures.Intentions` (make abstract,
make concrete). None of the packages knows about the shell, and the shell knows nothing about what they offer
- which is the point of the slice: a language ships its intentions, a host registers them and the popup, the
menu and the bulb follow. A provider that is not about the open document answers `Absent` at every caret, so
the app registers all four for every document rather than deciding which language a tab holds.

The expression package used to ship "wrap in parentheses" as well; it does not any more, because the catalogue
derives `Wrap in "Parenthesized"` from the same declarations and one operation must not appear twice. "Negate"
stays where it is, and that asymmetry is the interesting part: the catalogue can find a wrapper concept, but it
cannot know that a negation is a `Unary` born with `operator = "!"` - that value has to come from the language
until a language can declare an intention of its own (M5).

Two things a host has to get right, both of them the mirror of the findings rules:

- **Register once, at open.** A provider is a source, not a snapshot; `PublishFindings` runs again on every
  change, so registering there would list every entry twice.
- **The list is derived, the sets are published.** Intentions need no republishing because they are asked
  afresh at the caret; `Markers` and `Fixes` are snapshots and must be replaced.

## What the thin shell implements

The M1 shell is the smallest window that can open, edit and save a model, and it is
deliberately narrower than the tables above. What exists:

| Command | Shortcut | What it does |
| --- | --- | --- |
| New | `Ctrl+N` | a scratch document with one empty root, not yet on disk |
| Open | `Ctrl+O` | reads one model file into a new tab, or selects the tab already showing it |
| Save | `Ctrl+S` | writes to the file the document came from, asking for a place the first time |
| Save as | `Ctrl+Shift+S` | writes to a place the user picks and adopts it as the document's file |
| Close document | `Ctrl+W` | closes the tab, prompting when it is dirty |
| Exit | — | prompts once per dirty document, then closes |

`Ctrl+Shift+S` is Save As here rather than the Save all of the table above, because with
one model per file and no project yet, "save all" is the same operation as "save" repeated
and there is nothing for it to do that closing the window does not already do. Save all
comes back with projects in M3.

Two things the tables promise are not in the thin shell, and both are decisions rather than
omissions:

- **No file watcher.** A model edited by something else while a tab is open is not
  noticed, so the "reload from disk / keep my version" prompt of
  [open questions](../open-questions.md#standalone-editor) #23 has nothing to answer yet.
- **Errors go to the status bar, not to a dialog.** A file that will not parse, a path the
  app cannot write, a save that failed: each leaves a sentence at the right-hand end of the
  status bar and the document exactly as it was. A modal box would be the heavier answer
  for the one case a user can act on by picking another file, and the shell has no
  message-box vocabulary of its own.

The rules the shell must not get wrong are in `DocumentFile`, which is why it is a type of
its own rather than four statements in the window: **a save marks the document clean only
after the write returned**, so a failed save leaves the changes recoverable, and **a load
reports a broken file as a message**, so the window never has to catch the same exception
twice. A save that fails answers "do not close" to a close prompt, because the user asked
to keep changes that are still only in memory.

Opening a file that is already open selects its tab instead of reading it twice: two tabs
on one file would each believe they were the document, and whichever saved last would
silently discard the other's work.

## Command line

| Invocation | Behaviour |
| --- | --- |
| `projectional` | empty shell, open-project prompt |
| `projectional <project-dir>` | open that project, restore its last active document |
| `projectional <model-file>` | open the project containing the model, activate that model |
| `--readonly` | no mutating commands; useful for reviews and generated documentation |
| `--language <name>` | open a scratch document of that language without a project |
| `--new <language>` | create a scratch document with one empty root |

Exit code is `0` on clean exit and non-zero on an unhandled startup error, so the app
can be used from scripts. `--readonly` plus an exit code is what makes it usable in a
review or documentation pipeline.

## Packaging

| Artifact | Form | Milestone |
| --- | --- | --- |
| Developer build | `dotnet run --project src/Projectional.Workbench --framework net10.0` | M1 |
| Self-contained single file | one executable, no .NET install required | M2, done |
| Per-OS installer | `.msi`/`.dmg`/`.AppImage`, file association for `.prj` | M7 |
| Scoop / winget / Homebrew | package manifest | M7 |

Self-contained single file is the M2 target because it is what makes
"try before embedding" work for someone without a .NET SDK. Installers wait for M7
because an installer implies a stable file format and update policy, neither of which
should be frozen before the language format settles.

### The single file

```powershell
dotnet publish src/Projectional.Workbench -c Release -f net10.0 -r win-x64 -p:PublishSingleFile=true -o publish
```

The bundle is a **publish** shape, not a build shape: a normal build stays framework-dependent and
multi-file so the inner loop is fast, and the single-file properties in the csproj are guarded by
`'$(PublishSingleFile)' == 'true'`. Three things make "one file" true rather than nearly true:

- `IncludeNativeLibrariesForSelfExtract` puts Avalonia's native dependencies inside the bundle
  instead of a folder of `.dll`/`.so` beside it.
- `DebugType=none` and `AllowedReferenceRelatedFileExtensions=none` stop a portable PDB per
  referenced project from being copied next to the bundle.
- A `RemovePublishPdbs` target deletes the native PDBs SkiaSharp and HarfBuzz ship, which the
  native-asset copy step brings along whatever the reference settings say.

`Projectional.Workbench --self-check` is what CI runs against the bundle. It builds the shell's
language registry - the sample language composed with both definition languages - projects a
document through it and prints the concept and cell counts, then exits. It runs **before Avalonia
starts**, so it needs no display and works on a build agent, and it fails if a language package or
the projection layer is missing from the bundle. The `single-file` CI job publishes for `win-x64`
and `linux-x64`, asserts that `publish/` holds exactly one file, and runs the check.

AOT is validated at M7 like the libraries
([roadmap](../roadmap.md)), and the app is the right place to validate it: it is the
artifact users install, so trim and AOT problems surface there first.

## Testing the app

The shell is tested at two levels:

| Level | What is covered | How |
| --- | --- | --- |
| Headless shell logic | document lifecycle, dirty state, commands and `CanExecute`, command-line parsing, recent files, settings | plain unit tests, no window |
| UI wiring | menus and shortcuts are wired to the right command, tabs bind to sessions | `Avalonia.Headless.XUnit` |

The headless level is the default and covers the majority. A UI harness is introduced
when the shell first has wiring worth asserting on, not in advance.

## Limits of this document

This is a plan and a record, not a specification of every current behaviour. What is
delivered is the M1 shell described under
[What the thin shell implements](#what-the-thin-shell-implements): a tabbed window with a menu
bar and a status bar, dirty prompting, Save / Save as / Open / Close, the View preferences
(theme choice, line numbers, font size, representation) and one published set of findings, all on top
of the Avalonia control, which paints real glyphs, hit-tests clicks, routes keys and draws the
markers.

The rest is scheduled rather than described as existing:

- **Not wired at all.** The `## Command line` table above is a target: the args reach
  `StartWithClassicDesktopLifetime` and nothing reads them, so `projectional <model-file>`
  opens an empty shell. There are no recent files either.
- **M2.** Model browser, problems panel, self-contained single-file build - all three done.
- **M3.** Projects — everything here is still one model per file.
- **M4.** Output panel, and the validation that produces the markers a reader would want a
  panel for.
- **M5.** Language browser and live preview. From M2 a language can be declared in the project
  without C#, but only in the provisional shape; M5 is where the definition languages exist in
  their own notation, so a language is edited the way MPS edits one
  ([language-authoring.md](language-authoring.md)). Declaring a language in C# stays a
  first-class path after that, because it is the only one that carries behaviour and ships as a
  package ([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)).
- **M7.** Installers and file association.

Where a table row is implemented, the prose says so; where it is not, the roadmap's milestone
is the answer to when.
