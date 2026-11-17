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

```csharp
concept.Validate((node, ctx) =>
{
    var scope = ctx.Scope(node);
    if (node.Child("condition") is null)
        ctx.Error(node, "condition.required", "A rule needs a condition.");
    return ctx.Ok;
});
```

### What is implemented

`Projectional.Semantics/Constraints/` holds the engine, and the shape is the one above with the
collector made explicit:

| Type | Job |
| --- | --- |
| `IConstraint` | the aspect: `Check(ConstraintContext)`, reports and never mutates |
| `ConstraintContext` | the node, `ScopeOf(slot)`, and `Error`/`Warning`/`Hint`/`Report` |
| `ConstraintSet` | the language's rules, keyed by concept, immutable |
| `ConstraintEngine` | walks a document (or one subtree) and answers the findings |

- **A constraint reports; it never repairs.** The repair is a `DiagnosticFix` built by whoever knows how
  to fix the finding, which is why a language author can write a rule without knowing what the editor
  can do about it. A finding with no repair reaches the squiggles and the problems panel and not the
  quick-fix list - which is the right answer for "a rule needs a condition", where inventing a condition
  would be worse than saying one is missing.
- **A concept inherits its super concepts' constraints**, in declaration order - the same rule notation,
  triggers and scope providers follow, so a language has one answer to "which declaration applies".
- **The context is deliberately small**: the node, the scope and the collector. It cannot mutate, cannot
  reach the editor and cannot see a cell, so a rule that runs on a model nobody is drawing (a load, a
  batch check) works the same as one that runs on the open document.
- **`Check(Node)` is the incremental seam.** It answers that subtree's findings alone, so a caller that
  keeps the rest merges them itself - only the caller knows what it kept. The dependency-set machinery
  below is not implemented yet: today a caller rechecks the subtree that changed, which is correct and
  cheaper than the whole document, but it does not yet know that a *referencing* node's rule may have
  changed too.
- **The first consumer is `SampleConstraints`** in `Projectional.Samples.Checks` (a rule needs a
  condition), and the app merges its findings with the check catalogue's in `PublishFindings` - one list,
  because the panel and the markers must not be two answers to "what is wrong".

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

### What is implemented

`Projectional.Semantics/Types/` holds the model; the primitive and `Unknown` kinds are done, the
named and structural kinds are not.

| Type | Job |
| --- | --- |
| `IType` | `Name` and `Accepts(actual)` - assignability, asked in that direction |
| `PrimitiveType` | wraps a `PropertyType`, so a language declares its primitives where it already declares properties |
| `UnknownType` | the answer when nothing says; accepts and is accepted |
| `ITypeRule` | the aspect: `TypeFor(TypeContext)` |
| `TypeContext` | the node and `TypeOf(node)` / `TypeOfChild(slot)` - the whole of local propagation |
| `TypeRegistry` | the rules, keyed by concept, inherited by the concepts below it |

Four rules worth knowing:

- **`expected.Accepts(actual)` is the only question.** It reads the way the rule does, and a caller that
  got the direction wrong would be comparing two types for equality - a different and much weaker
  question.
- **`Unknown` accepts and is accepted**, which is what suppresses follow-on errors: one mistake in a
  condition must not produce a finding on every expression that reads it. It is therefore **not** a
  wildcard to rely on - a caller that must not proceed on a guess (a generator) asks
  `type is UnknownType` explicitly. That is the "cannot leak into generation unnoticed" rule: the type
  does not stop the caller, it makes the caller say what it does about not knowing.
- **A concept with no rule is `Unknown`**, not an error. Nobody said, so nothing is known, and every
  consumer is told that rather than handed a guess.
- **`ITypeRule.TypeFor`, not `TypeOf`**: CA1716 forbids a reserved word as an interface member, the same
  reason `ICheck.Find` is not called `Of`. The class member `TypeContext.TypeOf(Node)` keeps the natural
  name, because the rule does not apply to it.

**The first consumer is a constraint**: `SampleConstraints.ConditionMustBeBoolean` asks
`context.TypeOf(condition)` and reports a condition that is not a truth value. `ConstraintContext`
therefore carries the `TypeRegistry` beside the `IScopeProvider`, and `ConstraintEngine` takes both - a
rule that needs a type and a rule that needs a scope are the same kind of rule.

`SampleTypes` (in `Projectional.Samples.Checks`) declares the expression language's types: a literal is
its own type, a parenthesised expression is as wide as what it wraps, a comparison is a `bool`,
arithmetic is as wide as its left operand, and a name is `Unknown` because what it means depends on a
declaration this language does not have. Type rules are behaviour, so they live beside the language
rather than inside it - the same reason intentions and checks do.

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

Generation is only half of the text story. The other half is import, which is the single
place where a parser exists and the only path by which text becomes a model
([import.md](import.md)). The two are tested as a pair: `generate(import(text))` must equal
`text` for text the reference languages map completely, which is a property of the *text*, not
of the model - import mints fresh node ids, so the models are never equal.

Two consumers of the same template tree are planned on top of this: projected C# as a base
language, whose generator has to parenthesise by precedence ([csharp.md](csharp.md)), and the
JSON and XML reference languages, which exercise it in both directions.

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
