# ADR 0010 — Public API limited to `Core` and `Avalonia`

- Status: Accepted
- Date: 2026-11-14
- Related: [integration.md](../architecture/integration.md), [overview.md](../architecture/overview.md#projects)

## Context

This is a framework that other applications embed. Every public type becomes a
compatibility promise. At the same time, most of the interesting types (cells, layout
boxes, change records, registries) are exactly the ones that will change while the
project matures.

Two failure modes to avoid:

- **Everything public** (the default in most .NET projects): every refactoring is a
  breaking change; the package accumulates accidental contracts.
- **Nothing public but the control**: host applications then cannot read the model,
  inspect diagnostics, or trigger generation, which defeats the purpose.

There is also a structural concern: a host that only wants to edit a model should not
have to reference the language-definition assembly at all.

## Decision

| Assembly | Surface |
| --- | --- |
| `Projectional.Core` | public: `Project`, `ModelHandle`, `INodeView`, `Diagnostic`, change/save events |
| `Projectional.Avalonia` | public: `ProjectionalEditorControl`, `EditorSession`, theming entry points |
| `Projectional.Language` | **internal** (`InternalsVisibleTo` tests and the workbench) |
| `Projectional.Projection` | **internal** |
| `Projectional.Editing` | **internal** |
| `Projectional.Semantics` | **internal** |
| `Projectional.Workbench` | library surface is internal; shipped as an app |

Additional rules:

1. A public API baseline file is committed and validated in CI; any change requires an
   explicit baseline update in the same PR.
2. `node.Mutate(...)`-style internals are never public. Hosts create content through
   language-provided commands, or via `INodeView` for read-only access.
3. Types in `Core` that exist only to serve the editor are internal even though the
   assembly is public.

## Consequences

### Positive

- The language and projection layers can be refactored freely — where most of the churn
  will be — without breaking hosts.
- The runtime surface stays small enough to document completely.
- Headless hosts (generation without UI) reference only `Core` and `Semantics`, with no
  Avalonia dependency.
- Forced discipline: if a host needs something, it must be a designed API, not a
  reach into internals.

### Negative / costs

- Genuine advanced use cases (custom cells, custom aspect providers) are not reachable
  from a host without a designed extension point. That extension point must be designed
  deliberately, per case, rather than being the accidental default.
- `InternalsVisibleTo` across many assemblies plus strong naming is fiddly; the project
  must not enable strong naming (or must use public key variants consistently).
- Internal types still need documentation for contributors, so XML docs are written for
  internal members too.

### Open follow-up

Extension points (custom cell primitives, custom scope providers for hosts, custom
theming) are the expected pressure valve. Each gets an ADR when the first concrete need
appears — not before.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Everything public** | Guarantees a permanent accidental API and a breaking-change treadmill. |
| **Only the control public** | Hosts cannot read models or diagnostics; forces reflection hacks, which is exactly what [ADR 0007](0007-reflection-free-language-definition.md) avoids. |
| **Defer the decision** | The API surface is cheapest to constrain before the first release; retrofitting `internal` is a breaking change for early adopters. |
