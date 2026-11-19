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

Two creators exist for a child, and the difference is a position: `NewChild` appends, `NewChildAt`
places. The second one is what `Enter` in the middle of a list uses, because appending there puts what
the user just made at the far end of a list they were not looking at. Both publish exactly one
`NodeAdded`, so undo takes the node out of the place it actually went into.

A **replacement** takes a node's *place* rather than filling a slot: `Replace(node, conceptKey)` deletes
the old subtree and hangs a new node of another concept where it stood, in one transaction, so one undo
brings the old subtree back with its own identities. The new node is born detached and attached only
after the old one has left, because a single-valued slot cannot hold both - and what the old node held
does not travel, since no declaration says which slot of one concept corresponds to which slot of
another. The deletion is a real one: references into the old subtree are cleared, exactly as they are
when a node is deleted.

A **reorder** is the mirror image: `ReorderChildren(parent, slot, order)` takes the order rather than
deciding it. The model owns what must not vary - the slot holds the same children afterwards, no more
and no fewer, and the whole rearrangement is one transaction - while "sorted by what" stays a rule of
whoever asks, which is why `ChildOrder` lives in the editing layer. An order that already holds
journals nothing.

An **unwrap** is the inverse of the wrap: `Unwrap(node, slot)` lets the only child of that slot take the
node's *place* and deletes the node, in one transaction. The child keeps its identity, so every reference
into its subtree and every address that names one of its cells survives; the node's properties go with it,
because they are what made it a wrapper (`operator = "!"` on a negation). It refuses a model root, a slot
that does not hold exactly one child, a child that may not stand where the node stood, and a node that
still holds another child list or a reference - losing structure nobody asked about would make "unwrap" a
delete in disguise. The three placements it performs are each a step the journal already takes back, which
is why one undo restores the wrapper with the child inside it.

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

### Who points at a node

`ReferenceIndex.For(project).UsagesOf(node)` answers the reverse question: every place the node is
pointed at, as `ReferenceUsage(Owner, Slot, Target)` values in **document order** - models in project
order, nodes depth first, and the reference slots of one node in declaration order. The index is
public because "who else refers to this" is a fact of the model rather than of the editor: a rename
must rewrite every usage before it may change a name, and a host's usages panel lists the same answer.

- **It is per project, not per model.** A reference may cross a model boundary but never a project
  one, so a per-model index would have to fall back to a walk for exactly the case a rename cares
  about most.
- **It is invalidated by `Model.Version`, never by a change handler.** Subscribing to `Model.Changed`
  would switch change journalling on for every model in the project (`IsRecording`); comparing the
  versions costs one read each and sees a new reference, a retargeted one, a cleared one and a
  deletion alike. The per-project table is weak, so a closed project takes its index with it.
- **An unset slot is not a usage.** A dangling reference is indexed under the id it names - the index
  is faithful about what the model stores - but no live node answers that id, so a question about a
  node never returns it.

A rename asks this index and refreshes each fallback by re-writing the reference through the editing
layer: the index is what makes that one transaction and one undo step, and what the reverse question was
for in the first place.

## Model, project

| Level | Contains | Persisted as |
| --- | --- | --- |
| Node | data | inside a model file |
| Model | roots + imports, `ModelId`, language versions it uses | one file |
| Project | models, languages, generators, settings | a directory |

A model is the unit of import, of reference scope visibility, and of file locking.
Model granularity is the user's decision — too small means hundreds of files, too
large means merge conflicts.

### Copying a model

`Model.CopyTo(Project target)` is how a preview's sandbox is built, and it is exact: the same node ids,
concepts, property values, reference targets and fallback text, the same child order, the same roots in
the same order. Identity is the point, not an implementation detail - an edit action addresses ids and
slots rather than positions, so an action value means the same thing in the copy as in the original.
That is what makes "show what this edit would do" answerable by *doing it to a copy* instead of by a
per-action description ([intention-preview.md](intention-preview.md)).

Three consequences worth stating:

- **The copy needs its own project.** `Project` indexes nodes by id, so a copy carrying the original's
  ids can only live where those ids are still free. Copying into the source project throws
  `ModelInvariantException`, which is the shape of the mistake rather than a limitation.
- **Nothing is shared afterwards.** Mutating the copy touches no node of the original, and the two
  projects answer `FindNode` with different objects for the same id.
- **A failed copy leaves nothing behind.** The nodes built so far are deleted without recording and the
  half-built model is discarded from the target project, the way a failed load cleans up. Each node is
  attached to its place *before* it is filled, so a failure always leaves everything reachable from the
  copy's roots - a bottom-up copy would strand registered nodes with nothing pointing at them.

## Change events: the backbone

Every mutation goes through a transaction. A transaction yields a list of typed
changes; nothing mutates a node outside one.

```csharp
public interface IModelChange
{
    NodeId Target { get; }
    IModelChange Invert();      // the record that undoes this one
}

public sealed record NodeAdded(NodeId Target, NodeId? Parent, string? Role, int Index)        : IModelChange;
public sealed record NodeRemoved(NodeId Target, NodeId? Parent, string? Role, int Index)      : IModelChange;
public sealed record PropertyChanged(NodeId Target, string Name, string? Old, string? New)     : IModelChange;
public sealed record ReferenceChanged(NodeId Target, string Name, NodeId? Old, NodeId? New, bool WasResolved) : IModelChange;
```

`Parent` and `Role` are nullable because a node can be placed as a model root, which
has no owning node and no slot. `null` therefore means "at the top of the model",
never "unknown". `Index` is the position among the node's siblings in that slot (or
among the model's roots). The same records describe creation and moves: moving a node
is one `NodeRemoved` at the old place followed by one `NodeAdded` at the new one, so
subscribers never need a third change kind.

`WasResolved` records whether the *old* reference resolved to a node when it was
replaced, which undo needs in order to restore the very same reference.

`Invert()` is what makes a restoration audible. `NodeAdded` and `NodeRemoved` swap into
each other, `PropertyChanged` swaps old and new, and `ReferenceChanged` swaps old and
new and treats whatever it puts back as resolved — a cleared reference comes back
together with the subtree that held it.

A committed transaction publishes exactly one payload:

```csharp
sealed class ModelChangedEventArgs : EventArgs
{
    public ModelId Model { get; }
    public long Version { get; }                  // Model.Version after the commit
    public IReadOnlyList<IModelChange> Changes { get; }
}
```

A transaction that changes nothing publishes nothing.

Alongside the published records the commit hands the journal's *steps* to the
`UndoHistory` — one entry per mutation, each with an undo closure and a redo closure.
Those steps are internal to `Projectional.Core`: a host application that could reach
them could rewrite a model without going through a transaction and lose the records
that projection, semantics and persistence depend on. They are empty when the change
set comes from an undo or a redo, because the history already owns those steps.

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
- Deleting a subtree is **one** `NodeRemoved` for the whole tree, not one per node,
  followed by one `ReferenceChanged` for every reference that was cleared in the
  survivors. Undo restores the tree in one step and the references independently.
- Bulk operations (migrations, generated insertions) run in a single transaction and
  publish one change set, so undo treats them as one step.

## Undo

`UndoHistory` listens to `Model.Changed` and owns two stacks of `UndoStep`. It never
wraps the edit API, so nothing in the model has to know undo exists, and every path
that journals an edit is undoable for free — a keystroke, a generated block of a
hundred nodes, a drag that moved a subtree, a transaction the host defined itself.

Undo is **captured closures, not replay of the published records**. Replay breaks as
soon as an operation depends on external state, and it breaks on ordinary edits too: a
move publishes `NodeRemoved` + `NodeAdded`, so replaying the records would delete the
subtree (unregistering it) and then add it again by id. Instead each journal entry
holds the way back *and* the way forward from the mutation site, where all the state is
still at hand:

```csharp
readonly record struct JournalEntry(IModelChange? Change, Action Undo, Action? Redo);
```

- `Change` is `null` for a step that is an implementation detail (creating a detached
  node); such a step is undoable but not reportable.
- `Redo` is `null` when the step has no forward direction of its own. A detached
  creation is only the undo of an unregistration, because the placement that follows
  it is a separate step with its own redo.
- Redo re-runs the operation — the same `AttachPlacement`, the same `UnlinkFromPlace`,
  the same reference write — so redo is exactly as faithful as undo.

Closures are **raw**: they never call `Record`. A replay therefore journals nothing,
and `UndoHistory` publishes the change set itself, as `Change.Invert()` per entry in
reverse order for undo and as the original `Change` in forward order for redo. That is
why a projection invalidates views twice for the same edit and only once for a
restoration, and it is `IModelChange.Invert()`'s reason to exist.

Coalescing groups steps; it is not a time window. A step joins the previous one when
`UndoHistory.CoalescingKey` is non-null and equals the previous step's key, so typing
into a slot becomes one undo instead of one per keystroke. Consequence for the caller:
clear the key when the burst ends, otherwise the next transaction that forgets to set
a key of its own joins the burst. `null` never merges.

`Clear()` forgets both directions and the key, which is what loading a model needs. Any
new committed transaction drops the redo stack, as usual.

## Snapshots

Semantics runs against an immutable snapshot so validation never sees a torn model:

- Snapshot = frozen set of model versions + a copy-on-write node index.
- Taking a snapshot is O(1) via a version counter; only mutated nodes are copied.
- A snapshot is only valid within one generation; stale results are dropped.

## Diff

`ModelDiff.Of(before, after)` answers what one model has that the other does not, matched by node id. It
exists for the preview's ghost ([intention-preview.md](intention-preview.md)), and it is a structural
diff on purpose: two documents can read identically and be different trees, and one node can move
without a character changing, so a text diff would answer a different question.

| Kind | What it says |
| --- | --- |
| `Added` | the node, and everything below it, exists only in the later model |
| `Removed` | the node, and everything below it, exists only in the earlier one; `OldValue` carries its name so what went can still be named |
| `Moved` | the node exists in both and stands in another parent, slot or index |
| `PropertyChanged` | the same property holds another value |
| `ReferenceChanged` | the same reference points at something else, or its fallback text does |

A node appears once per change, so a moved node with two rewritten properties is three entries. Entries
come in document order of the later model, then the removals in the order they had - a renderer can walk
the list once, marking the document it is drawing and collecting removals for a badge on the way.

Same id with a different concept is reported as `Removed` plus `Added`, never compared: ids are unique
per project, not per universe, so two unrelated models can share one and comparing them would describe an
edit nobody made.

## Invariants (enforced on every mutation)

These are not debug-only assertions. Identity, placement and reference rules are checked by the
operation that would break them and reported as `ModelInvariantException`, because a projectional
editor builds its tree from user gestures rather than from a parser — a rejected edit is a normal
outcome that the UI has to explain, not a programming error to hide behind an assert.

Two of them need no separate check because a data structure already decides them: invariant 1 is
what the project's node index asserts on insert, and invariant 2 is what a single parent link per
node makes impossible to violate.

1. Every node's ID is unique within a project.
2. A node belongs to exactly one child slot of exactly one parent, or is a model root.
3. A child slot only contains nodes whose concept satisfies the declared type.
4. References never point into a node's own subtree unless the concept allows it.
5. Removing a node removes it from its parent and clears all incoming references
   within the same transaction.
6. Every stored property value parses against its declared type.
7. A node only stands where its concept's `Applicability` allows - a concept may name the places it may
   appear in, and a placement anywhere else is refused ([language.md](language.md#applicability)).

**A reference may cross a model boundary, but not a project one.** Two documents of one project share
the node index, so a link between them resolves; the scope provider already walks up to the project to
answer what a name may mean, and a reference that refused those nodes would make the completion list
offer what the model rejects. The SQL schema example
([worked-examples.md](worked-examples.md#3-sql-schema---projectionalsamplessqlschema)) is what found
this: the code compared the *model* of the target and refused every cross-document key, while
invariant 5's clearing already walked `Project.Nodes` and the section above calls a move between models
a case `resolveInfo` has to absorb. A target of another project is still refused, because the ids in
two projects are unrelated.
