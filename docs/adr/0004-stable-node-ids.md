# ADR 0004 — Stable node IDs with `resolveInfo` fallback

- Status: Accepted
- Date: 2026-11-14
- Related: [model.md](../architecture/model.md#identity), [persistence.md](../architecture/persistence.md)

## Context

References between nodes need a storage form that survives real-world usage:

- nodes get renamed constantly (a rename must not break references)
- nodes move between models and directories
- files are edited in git, copied, and merged
- models are opened while partially broken

A pure name-based reference breaks on rename and on name collisions. A pure
ID-based reference breaks whenever a node is copied by a tool that does not remap IDs,
and it is unreadable in diffs.

## Decision

Store references as **`target` (GUID) plus `resolveInfo` (the target's name at the time
of writing)**, and resolve in this order:

```text
1. find node with target ID        → resolved
2. else find node named resolveInfo within the declared scope
                                   → resolved, marked "needs re-save"
3. else                            → unresolved reference + error diagnostic
```

Resolution never silently guesses when step 2 is ambiguous; ambiguity is a diagnostic.

## Consequences

### Positive

- Renames are safe: the ID resolves first, and `resolveInfo` is refreshed on save.
- File moves, copies between projects, and most git merges keep working.
- Human-readable diffs: reviewers see which name a reference pointed to.
- Broken models remain loadable and repairable instead of silently rewiring to a
  random node with the same name.

### Negative / costs

- Two fields must be kept in sync on every reference write; forgetting `resolveInfo`
  degrades the fallback silently. Guarded by a serialization test that walks all
  references.
- Step 2 can produce a wrong repair when a name is reused after a deletion. Mitigated
  by scoping the search to the declared scope and by reporting the rebind as an
  informational diagnostic.
- `resolveInfo` bloats files slightly and must be migrated when a language renames a
  concept's naming property.

### Rules

1. `resolveInfo` is written on every save, not only on creation.
2. A rebind in step 2 marks the model dirty so the correction is persisted.
3. A rebind never crosses a scope boundary, even if the name matches.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **ID only** | Unreadable diffs; any tool that copies nodes without remapping silently corrupts references with no recovery. |
| **Name only** | Breaks on every rename, which is the most common edit a DSL user performs. |
| **Path-based (index path from root)** | Breaks on any structural edit above the target; strictly worse than both. |
| **ID with index-based fallback** | Preserves structural position but not identity; after a reorder it points at the wrong node, which is worse than unresolved. |
