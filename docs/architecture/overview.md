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
│  Projectional.Workbench        language workbench app        │
├──────────────────────────────────────────────────────────────┤
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
a second front end (Blazor) stays possible. `Core` must not reference `Language`;
languages are registered into Core's registries from above.

## Projects

| Project | Responsibility | UI reference |
| --- | --- | --- |
| `Projectional.Core` | Node/model/project containers, change events, XML persistence, migrations, registries | none |
| `Projectional.Language` | Concept descriptors, notation descriptors, aspect registration, built-in bootstrap languages | none |
| `Projectional.Projection` | Editor tree construction, measurement, layout boxes, hit testing, caret positions | none |
| `Projectional.Editing` | Structural edit actions, substitution/transformation menus, completion, undo | none |
| `Projectional.Semantics` | Constraint evaluation, type checking, scope resolution, generation, interpretation | none |
| `Projectional.Avalonia` | `ProjectionalEditorControl`, rendering of layout boxes, input, IME, clipboard, accessibility | Avalonia 12 |
| `Projectional.Workbench` | Host application for authoring languages and models | Avalonia 12 |
| `tests/*` | Unit and snapshot tests | none |
| `samples/*` | Example languages and host integrations | varies |

Only `Projectional.Core` and `Projectional.Avalonia` expose public API. Everything
else is `internal` with `InternalsVisibleTo` for tests — see
[ADR 0010](../adr/0010-public-api-surface.md).

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
Projection: build editor tree → measure → layout boxes for the viewport
  │
  ▼
Avalonia control: render boxes, place caret, scroll into view
```

Re-rendering is scoped to the visible viewport. Everything outside it is measured
lazily and cached.

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
