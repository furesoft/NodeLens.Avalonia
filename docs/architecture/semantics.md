# Semantics

Everything that gives a model meaning beyond shape: constraints, types, scope
resolution, generation and interpretation. All of it is expressed as aspects on
concepts and evaluated against snapshots.

## Pass order

Fixed and documented, because aspects depend on each other:

```text
1. Structure      slots valid against concept          (always, cheap)
2. Scope          references resolvable                (needed by everything after)
3. Types          type of each expression              (needs scope)
4. Constraints    language rules                       (needs types)
5. Generation     text or model output                 (needs a valid model)
6. Interpretation run/evaluate                         (needs generation or direct eval)
```

Each pass produces diagnostics and never mutates the model. Only migrations mutate.

## Diagnostics

One diagnostics model for everything the user sees:

```csharp
sealed record Diagnostic(
    DiagnosticSeverity Severity,   // Hint | Info | Warning | Error
    NodeId Target,
    string? Role,                  // null = whole node
    int? Index,
    string Message,
    string? Code,                  // stable identity, e.g. "RULE_002"
    CharacterRange? Range,         // null = the whole target; otherwise part of one text
    IReadOnlyList<DiagnosticFix>? Fixes);  // null = nothing to offer

// A fix is an intention addressed to one diagnostic: it names the intention by id, and the
// diagnostic carries the label so the menu can be built without resolving it first.
sealed record DiagnosticFix(string IntentionId, string Label);
```

- Diagnostics are rendered as markers/underlines, never as blocking dialogs.
- Errors do not prevent editing, saving, or opening.
- Quick fixes are edit actions, so they are undoable and testable headlessly.
- Generation and interpretation refuse to run when errors exist in the affected
  subtree — but still run for unaffected subtrees, so a large model can be partially
  generated while being edited.

The record says *what is wrong*; it says nothing about where on screen that is or what it
looks like. Turning a diagnostic into a squiggle is the editing layer's job, and the plan for
it is [markers.md](markers.md). Two properties in the record exist for that: `Range` narrows
the complaint to a slice of one property's text when the constraint knows which characters
are wrong, and `Code` is what a test and a documentation page refer to without matching prose
that will be reworded.

## Constraints

```csharp
concept.Validate((node, ctx) =>
{
    var scope = ctx.Scope(node);
    if (node.Child("condition") is null)
        ctx.Error(node, "condition.required", "A rule needs a condition.");
    return ctx.Ok;
});
```

### Incrementality

Naive full revalidation does not scale to a million nodes. The mechanism:

- Each node has a **dependency set**: the nodes its check read (children, targets of
  followed references, properties).
- A change dirties the checked node plus every node whose dependency set contains the
  changed node.
- Dirty nodes are rechecked on the thread pool, in batches, cancellable, against a
  snapshot.
- Rechecks publish diagnostics only if their snapshot generation is still current.

Scope and type results are cached per snapshot, keyed by node, and invalidated the same
way. Caches are discarded, not patched, on generation change — simplicity beats
cleverness here until profiling says otherwise.

### Language of constraints

C# now, declared rules later (M6). The declared form lowers into the same `Validate`
delegate, so nothing downstream changes. Constraint order within a concept is
unspecified; constraints must not depend on each other's results.

## Types

`IType` is a runtime value, and in the declared stage types are nodes. Starting point:

| Kind | Examples |
| --- | --- |
| Primitive | `bool`, `int`, `string`, `decimal` |
| Named/declared | user-defined types in the model |
| Structural | tuple/record, list, optional |
| `Unknown` | result when an expression's type cannot be computed |

`Unknown` is a first-class type: it suppresses follow-on errors so one mistake does not
produce fifty diagnostics, and it never compares equal to anything, so it cannot leak
into generation unnoticed.

Rules are per-concept, not a global unification engine. There is no type inference
beyond local propagation; if a language needs more, its concepts provide it.

## Generation

Text generation is a template tree per concept:

| Node | Emits |
| --- | --- |
| `TextConstant` | literal text, with indentation control |
| `PropertyRef` | property value, optionally escaped/transformed |
| `ChildExpansion` | the child's generated text |
| `ChildrenExpansion` | all children, separated and indented |
| `Conditional`, `Loop` | control flow in the template |
| `LineBreak`, `Indent` | whitespace |

Properties:

- Output is deterministic: same model → same bytes.
- Indentation is computed by the template, not by inspecting child text.
- Every emitted region is traceable to a node (kept in a side map), so a later
  "go to source" from generated code is possible without reworking the generator.
- Generation is pure: it does not read the file system or environment.

Model-to-model generators (MPS' second generator stage) are deliberately excluded
before 1.0. They double the conceptual surface and the debugging cost.

## Interpretation

An `Evaluate` aspect per concept, walking the tree:

```csharp
concept.Evaluate((node, ctx) => node.Child("condition") is { } c
    ? ctx.Eval(c) is true
    : throw ctx.Error(node, "condition.required"));
```

Requirements that make it a debugger rather than an evaluator:

| Requirement | Reason |
| --- | --- |
| Explicit evaluation stack of `(node, depth)` | needed for step in/out and for mapping a runtime state back to a caret position |
| Trace of evaluated nodes with values | shows the user *why*, in their own model |
| Cancellation and a step budget | a runaway model must not hang the UI |
| Deterministic ordering | generation tests and interpreter tests must agree |

Because both a generator and an interpreter exist, they cross-check: for the pilot DSL,
a test asserts that generated code and interpretation produce identical results for the
same model. That is the cheapest correctness net available for a language framework.

## Error recovery during editing

- Unresolved reference → `Unknown` type, error marker, generation blocked for that
  subtree only.
- Missing mandatory child → slot rendered empty with a hint, constraint error.
- Deleted node with incoming references → references become unresolved in the same
  transaction (never dangling IDs), so the model remains loadable.

## Performance discipline

| Pass | Strategy |
| --- | --- |
| Structure | validated on mutation, not on read |
| Scope | resolved lazily per reference, cached per snapshot |
| Types | computed on demand, cached per snapshot |
| Constraints | incremented via dependency sets, batched |
| Generation | per subtree, on demand, cached by node version |
| Interpretation | always explicit, never cached |
