# Model

The model layer is the foundation everything else depends on. It has no UI
knowledge and no language knowledge; languages describe it from the outside.

## Node

A node is a typed map of named slots. There are exactly four kinds of slot:

| Slot kind | Semantics | Cardinality | Stored as |
| --- | --- | --- | --- |
| **Property** | value of a primitive type | exactly 1 | text |
| **Child** | owned subtree, removal deletes it | per concept declaration | ordered list |
| **Reference** | pointer to a node owned elsewhere | per concept declaration | target ID + `resolveInfo` |
| **Node ID** | identity | exactly 1 | GUID |

```csharp
var rule = model.NewNode("Rules.Rule");       // concept is a string key into the registry
rule.Set("name", "Approved");                  // property
rule.SetRef("condition", expressionNode);      // reference, target may be unresolved
rule.AddChild("outcome", actionNode);          // child
```

Deliberate restrictions:

- **No free-form dictionaries.** Every slot is declared by the concept, so typos and
  drift are compile/runtime errors instead of silently stored junk.
- **No node type inheritance in the storage shape.** Concepts have superconcepts for
  *semantics* (aspects, allowed slots), but a node's data is always a flat slot map
  validated against the concept's full inherited slot set.
- **Children are ordered, references are not.** Ordering exists because notation and
  generation depend on it; references have no user-visible order.

### Property types

`string`, `int`, `bool`, `enum(symbols)`, `date`. Everything else — numbers with
units, expressions, structured values — is modelled as a child node, not a property.
This keeps the renderer and the XML small and forces domain concepts into the model
where they can be validated.

## Identity

Every node carries a stable GUID assigned at creation and never changed, including
across renames, moves between models, and copy/paste (copy assigns new IDs).

Identity is what makes the rest of the system work:

- References are stored as target ID.
- Caret positions and undo entries are addresses by ID.
- Diffing and merging operate on IDs, not paths.

### Reference resolution

Stored: `targetId` + `resolveInfo` (the target's name at the time of writing).

```text
resolve(targetId) → found in loaded models        → done
                 → not found, resolveInfo matches → rebind, mark needs-save
                 → neither                        → unresolved reference, error marker
```

The `resolveInfo` fallback absorbs the two cases that otherwise destroy models:
renamed targets after a broken refactor, and moves between models by other tools.
It is a recovery path, never the primary lookup.

## Model, project

| Level | Contains | Persisted as |
| --- | --- | --- |
| Node | data | inside a model file |
| Model | roots + imports, `ModelId`, language versions it uses | one file |
| Project | models, languages, generators, settings | a directory |

A model is the unit of import, of reference scope visibility, and of file locking.
Model granularity is the user's decision — too small means hundreds of files, too
large means merge conflicts.

## Change events: the backbone

Every mutation goes through a transaction. A transaction yields a list of typed
changes; nothing mutates a node outside one.

```csharp
public interface IModelChange { NodeId Target { get; } }

sealed record NodeAdded(NodeId Parent, string Role, int Index, NodeId Child)      : IModelChange;
sealed record NodeRemoved(NodeId Parent, string Role, int Index, NodeId Child)    : IModelChange;
sealed record PropertyChanged(NodeId Target, string Name, string? Old, string? New): IModelChange;
sealed record ReferenceChanged(NodeId Target, string Name, NodeId? Old, NodeId? New, bool WasResolved) : IModelChange;
```

Subscribers, all of which are mandatory from day one:

| Subscriber | Reacts by |
| --- | --- |
| Undo | pushing inverse changes, coalescing per typing burst |
| Projection | invalidating editor trees of target and ancestors |
| Semantics | dirtying constraint checks of dependent nodes |
| Persistence | marking the model dirty |
| Host application | raising `ModelChanged` for its own dirty state |

Design rules:

- Changes are **coarse enough to be useful and fine enough to be incremental**:
  a child list reorder is one change with the affected range, not N add/remove pairs.
- Transactions are nestable; only the outermost commits and publishes.
- A transaction that throws leaves the model untouched (mutations are journaled and
  rolled back).
- Bulk operations (migrations, generated insertions) run in a single transaction and
  publish one change set, so undo treats them as one step.

## Undo

Inverse changes, not command replay. Replay breaks as soon as an operation depends on
external state (completion candidates, scope contents, generated IDs). Inverse
changes are exact and cheap.

Coalescing: consecutive property edits on the same node and property within a typing
burst collapse into one undo step, as do repeated sibling insertions of the same
concept.

## Snapshots

Semantics runs against an immutable snapshot so validation never sees a torn model:

- Snapshot = frozen set of model versions + a copy-on-write node index.
- Taking a snapshot is O(1) via a version counter; only mutated nodes are copied.
- A snapshot is only valid within one generation; stale results are dropped.

## Invariants (asserted in debug builds)

1. Every node's ID is unique within a project.
2. A node belongs to exactly one child slot of exactly one parent, or is a model root.
3. A child slot only contains nodes whose concept satisfies the declared type.
4. References never point into a node's own subtree unless the concept allows it.
5. Removing a node removes it from its parent and clears all incoming references
   within the same transaction.
6. Every stored property value parses against its declared type.
