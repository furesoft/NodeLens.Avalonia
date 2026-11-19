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
sealed record Diagnostic
{
    Diagnostic(
        DiagnosticSeverity severity,   // Hint | Info | Warning | Error, in that order
        NodeId target,
        string message,
        string? role = null,           // null = the node's own text
        int? index = null,             // which child of a many-valued role
        string? code = null,           // stable identity, e.g. "RULE_002"
        CharacterRange? range = null); // null = all of the value; otherwise part of one text

    DiagnosticSeverity Severity { get; }
    NodeId Target { get; }
    string Message { get; }
    string? Role { get; }
    int? Index { get; }
    string? Code { get; }
    CharacterRange? Range { get; }
}
```

A quick fix is *not* on the record. Fixes are intentions — edit actions that change the model —
and a diagnostic that carried them would make "what is wrong" and "what to do about it" one
type, decided by the same check. The mapping from diagnostic to fix list lives beside the set
and is keyed by this record; see [quick-fixes.md](quick-fixes.md). That is the part of
[context-actions.md](context-actions.md) which lands **first**: repairs for the findings that
already exist, before the general intention list they will later join.

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

The language's own validation: a rule reports, never repairs, and the repair is a `DiagnosticFix` built by
whoever knows how to fix the finding.

[semantics/constraints.md](semantics/constraints.md)

## Types

`IType.Accepts` is the only question, `Unknown` accepts and is accepted so one mistake does not produce fifty
findings, and a declared rule answers one question per node.

[semantics/types.md](semantics/types.md)

## Generation

Templates are data and the walk is `Generator`: eleven closed parts, output that carries a traceability map,
and a language that declares as many generators as it prints formats.

[semantics/generation.md](semantics/generation.md)

## Interpretation

One function per concept says what a node means; the interpreter owns the budget, the stack and the trace,
and a failure is a result rather than an exception escaping the host.

[semantics/interpretation.md](semantics/interpretation.md)

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
