# Architecture overview

## What makes this editor *projectional*

Text editors store characters and recover structure with a parser. A projectional
editor inverts this: the **model is a tree of nodes** and the visible representation
is computed *from* the model on every render. There is no text buffer, no whitespace
with meaning, and no grammar to parse.

Consequences that drive the whole design:

- Any notation is possible for the same model. A rule table and the equivalent
  `if` chain are two projections of the same nodes.
- Input cannot be "just inserted". Each keystroke must be routed to a structural
  edit: replace a property value, insert a child, create a sibling, wrap a subtree,
  or open a completion menu.
- A model is *always* syntactically valid by construction. Only semantic problems
  (unresolved references, violated constraints) can exist, and those are reported as
  markers rather than blocking edits.
- Half-finished models are a normal state, not an error case. Persistence,
  validation and rendering must all accept incomplete trees.

## Layers

```text
┌──────────────────────────────────────────────────────────────┐
│  Projectional.Workbench        standalone editor (host)      │
│                                built only on public API      │
└───────────────────────────────┬──────────────────────────────┘
                                │ public API only
┌───────────────────────────────▼──────────────────────────────┐
│  Projectional.Avalonia         custom control, text layout,  │
│                                hit testing, IME, clipboard   │
├──────────────────────────────────────────────────────────────┤
│  Projectional.Projection       editor tree → layout boxes    │
│  Projectional.Editing          caret, actions, substitutions │
│  Projectional.Semantics        constraints, types, scopes,   │
│                                interpreter, text generation  │
├──────────────────────────────────────────────────────────────┤
│  Projectional.Language         concepts, notation, aspects   │
├──────────────────────────────────────────────────────────────┤
│  Projectional.Core             node, model, project, changes │
│                                XML persistence, migrations   │
└──────────────────────────────────────────────────────────────┘
```

Dependency rule: **arrows point downward only.** `Projection` and `Editing` must not
reference Avalonia, so that layout and editing behaviour are testable headlessly and
a second front end (the Avalonia browser head) stays possible. `Core` must not reference `Language`;
languages are registered into Core's registries from above. `Workbench` sits *above*
the public boundary: it sees exactly what any host application sees, which is what
makes it a usable test of the integration surface
([ADR 0011](../adr/0011-standalone-editor.md)).

## Projects

| Project | Responsibility | UI reference |
| --- | --- | --- |
| `Projectional.Core` | Node/model/project containers, change events, XML persistence, migrations, registries | none |
| `Projectional.Language` | Concept descriptors, notation descriptors, aspect registration (typing, emphasis, types), built-in bootstrap languages | none |
| `Projectional.Expressions` | Shared expression/name sub-language (literals, operators, parentheses) that a DSL composes instead of re-declaring, with its own typing rules, types and its intention (`Intentions/`) | none |
| `Projectional.Projection` | Editor tree construction, measurement, layout boxes, hit testing, caret positions | none |
| `Projectional.Editing` | Structural edit actions, substitution/transformation menus, completion, undo | none |
| `Projectional.Semantics` | Constraint evaluation, scope resolution, generation, interpretation | none |
| `Projectional.Avalonia` | `ProjectionalEditorControl`, rendering of layout boxes, input, IME, clipboard, accessibility | Avalonia 12 |
| `Projectional.Workbench` | Standalone desktop editor: document tabs, browsers, panels. **Consumer** of the public API — no internal access | Avalonia 12 |
| `Projectional.Samples` | The toy rules language and its sample document, plus the behaviour that belongs to it: its constraints and duplicate-name check (`Checks/`) and its intention (`Intentions/`) | none |
| `Projectional.Checks` | The standard catalogue a language includes by naming it: name, structure, value and expression checks, each with its repair | none |
| `Projectional.Toolkit` | The surface a language package is written against: one reference that brings `Core`, `Language`, `Projection` and `Persistence`, plus `LanguageSnapshot` - the drawing a notation test asserts on ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)) | none |
| `Projectional.LanguageExport` | A declared language becomes the C# a package is made of: its declaration and notation calls, a README naming what the export cannot write, and - on request - the compiled assembly ([language-authoring.md](language-authoring.md#export-declared-to-c)) | none |
| `tests/*` | Unit and snapshot tests | none |

**One project per language** ([ADR 0021](../adr/0021-one-project-per-language.md)), grouped by folder:
`src/Languages/` holds the language packages (`Projectional.Expressions`, `.Json`, `.Xml`, `.Structures`,
`.Editors`, `.TypeRules`, `.TextGen`, `.ConstraintRules`, `.ScopeRules`, `.TypingRules`, `.IntentionRules`), and
`src/Languages/Samples/` nests the sample languages (`Projectional.Samples`, `.StateMachine`, `.FeatureModel`,
`.SqlSchema`) - a subfolder rather than a second `Samples/` beside the repository's own `samples/`, which is
where the host application lives. Each language project carries its own checks and intentions in `Checks/` and
`Intentions/` folders. The namespaces keep their old names (`Projectional.Json.Checks` and so on), so where a
project sits is a question about the repository, not about the API. What stays in `src/` itself is the framework
and the host: `Core`, `Language`, `Projection`, `Semantics`, `Editing`, `Checks`, `Intentions`, `Toolkit`,
`Avalonia`, `Workbench`, plus the two libraries a host calls - `Languages` (opening a language folder) and
`LanguageExport`.

**Every project under `src/` is a NuGet package** ([ADR 0022](../adr/0022-every-project-is-a-package.md),
[packaging.md](packaging.md)): 27 packages today, being the thirteen framework assemblies above, the ten
languages and the four sample languages. `IsPackable` is `true` by default and the shared metadata lives in
`Directory.Build.props`; `tests/`, `tools/Projectional.Screenshots`, `samples/host-app` and the language
template opt out, because a package is a thing to depend on and those are not. The version is derived from the
repository by GitVersion (`GitVersion.yml`), and only a `v*` tag publishes - so an embedder's first step is a
`PackageReference` rather than a folder to add to its solution.

Three assemblies expose the *editor* API: `Projectional.Core`, `Projectional.Avalonia` and - since
[ADR 0019](../adr/0019-toolkit-is-the-language-surface.md) - `Projectional.Toolkit`, which is what a
language package references instead of reaching into four assemblies. `Language`, `Projection` and
`Persistence` are public as a consequence of being aggregated there; `Editing` and `Semantics` stay
internal, which is what keeps a language *authored against the toolkit* headless. A shipped language
package carries its own checks and intentions, so it references them - that is the arrow ADR 0021 adds,
and the tower a host inherits is the same one it had when those packages were separate. Libraries a
*host* calls for one capability -
`Projectional.Languages` (opening a language folder) and `Projectional.LanguageExport` (turning one
into a package) - are public for the same reason the control is: they are the surface of that
capability rather than an internal another layer happens to name. Everything else is `internal`
with `InternalsVisibleTo` for tests and for the one friend each layer names —
see [ADR 0010](../adr/0010-public-api-surface.md). `Projectional.Workbench` deliberately
has no such exemption: it is the reference consumer that keeps the public API honest
([ADR 0011](../adr/0011-standalone-editor.md), [standalone.md](standalone.md)).

## Data flow of one keystroke

```text
key press
  │
  ▼
Avalonia control ── hit test ──▶ caret (NodeId, role path/index, offset)
  │
  ▼
Editing: pick action for (caret, key) via the touched editor cell
  │        may open completion, may wrap subtree, may insert sibling
  ▼
Core: transaction → node mutations → change events
  │
  ├─▶ Undo stack records inverse changes
  ├─▶ Semantics: dirty markers → background validation → error markers
  ├─▶ Projection: invalidate editor tree for touched nodes and ancestors
  └─▶ Persistence: mark model dirty
  │
  ▼
Projection: build editor tree → measure and arrange → flat placements
  │
  ▼
Avalonia control: render placements, place caret, scroll into view
```

Re-rendering is scoped to the visible viewport once the control exists; until then the
whole tree is laid out, which is what the snapshot renderer draws.

## Three notions of "position"

Keeping these apart prevents most of the subtle bugs in this kind of editor:

| Notion | Representation | Stable against |
| --- | --- | --- |
| Model position | `NodeId` + role + index | edits elsewhere, re-layout |
| Caret | model position + offset inside one cell's text | re-layout, scrolling |
| Pixel position | `Rect` in the rendered canvas | nothing — derived value only |

A caret is never stored as a character offset into a document, because there is no
document.

## Threading model

- **One writer.** All model mutations happen on the UI thread inside a transaction.
  No locks in the model.
- Validation, generation and interpretation run on the thread pool against an
  immutable snapshot, tagged with a generation counter. Results whose generation is
  stale are discarded, not applied.
- Reading a model from a background thread is only allowed through a snapshot taken
  inside a transaction.

## Testability

Because `Projection` and `Editing` are UI-free, the primary regression test is a
**layout snapshot**: render a model to an ASCII grid and assert on the text. This
tests notation, layout and caret positions in one assertion without a window.

```text
ruleset Payroll {
  ▸ rule Approved when amount > 1000 then Accept
  ▸ rule Reject otherwise then Deny
}
```

Editing tests drive the same headless pipeline by feeding actions at caret addresses
and asserting on the resulting model XML.
