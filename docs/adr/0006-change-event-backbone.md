# ADR 0006 — Change events are the backbone

- Status: Accepted
- Date: 2026-11-14
- Related: [model.md](../architecture/model.md#change-events-the-backbone), [semantics.md](../architecture/semantics.md)

## Context

Five subsystems need to know that a model changed: undo, projection, validation,
persistence and the host application. There are two ways to build that:

1. each subsystem re-computes what it needs on demand (polling / full refresh), or
2. all mutation funnels through one place that publishes typed changes.

Option 1 is what a prototype naturally grows into. It fails at the target model size
(10k–1M nodes): full revalidation or full re-layout per keystroke is not affordable, and
there is no place to hook undo without an ad-hoc mechanism per subsystem.

## Decision

**All mutation goes through transactions that emit typed, fine-grained changes.**
Nothing mutates a node outside a transaction. Change records carry the affected node,
role and index so subscribers can be incremental.

```csharp
sealed record NodeAdded(NodeId Parent, string Role, int Index, NodeId Child);
sealed record NodeRemoved(NodeId Parent, string Role, int Index, NodeId Child);
sealed record PropertyChanged(NodeId Target, string Name, string? Old, string? New);
sealed record ReferenceChanged(NodeId Target, string Name, NodeId? Old, NodeId? New, bool WasResolved);
```

## Consequences

### Positive

- Undo is a generic consequence: inverse change lists, no per-operation undo code.
- Incremental validation and layout are possible at all; without the events they would
  have to be approximations.
- Bulk operations (migrations, generated insertions, refactorings) become atomic and
  single-step undoable by construction.
- Host applications get change notification for free, including the dirty flag.
- Transactions give rollback on exception, which makes failure paths safe.

### Negative / costs

- Every model API call must be transaction-wrapped; a leaked direct mutation is a
  correctness bug. Enforced by debug assertions on the mutation path.
- Change records must be *coarse enough* to stay cheap and *fine enough* to be useful.
  Getting this balance wrong (e.g. one record per character) makes the event stream
  itself the bottleneck.
- Nesting transactions and publishing only at the outermost commit adds bookkeeping.

### Rules

1. Change records describe **what happened**, never what should happen — no
   "invalidate layout" hints.
2. Subscribers must not mutate the model from a change handler. Deferred work goes
   through the transaction queue.
3. The event order within a transaction is the mutation order, so subscribers may rely
   on it.
4. Records are immutable value types so a background validator can hold them safely.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **No events, full refresh per edit** | Simple prototype, unusable at target size; also no sane undo. |
| **Per-subsystem diffing (compare snapshots)** | Requires two full model copies per keystroke; the diff cost exceeds the validation cost. |
| **Command pattern with replay for undo** | Replay breaks when an action depends on state that has since changed (completion lists, scopes, generated IDs). |
| **Reactive streams (IObservable) as the primary model** | Attractive, but subscriptions become ordering problems and leaks; a plain ordered event list is debuggable and deterministic. |
