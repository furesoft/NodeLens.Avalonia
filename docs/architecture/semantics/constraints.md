# Constraints

> Part of [semantics.md](../semantics.md), the design of record for the semantic aspects. The pass order,
> the diagnostics, the error recovery and the performance numbers stay in the parent.

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
| `ConstraintSet` | the language's rules: keyed by concept, or declared for every node, immutable |
| `ConstraintEngine` | walks a document (or one subtree) and answers the findings |

- **A constraint reports; it never repairs.** The repair is a `DiagnosticFix` built by whoever knows how
  to fix the finding, which is why a language author can write a rule without knowing what the editor
  can do about it. A finding with no repair reaches the squiggles and the problems panel and not the
  quick-fix list - which is the right answer for "a rule needs a condition", where inventing a condition
  would be worse than saying one is missing.
- **A concept inherits its super concepts' constraints**, in declaration order - the same rule notation,
  triggers and scope providers follow, so a language has one answer to "which declaration applies".
- **A rule about any node is declared with `Everywhere`, not with a concept key**, and runs before the
  concept's own. That is not a convenience: "this reference no longer identifies one element" is a fact
  about references in general, and a language's concepts need not share a root, so a rule declared over
  one root would silently skip every concept that does not inherit from it. `With(set)` composes two
  sets in a predictable order, which is how a host adds the framework's scope rules to its own.
- **The context is deliberately small**: the node, the scope and the collector. It cannot mutate, cannot
  reach the editor and cannot see a cell, so a rule that runs on a model nobody is drawing (a load, a
  batch check) works the same as one that runs on the open document.
- **`Check(Node)` is the incremental seam.** It answers that subtree's findings alone, so a caller that
  keeps the rest merges them itself - only the caller knows what it kept. The dependency-set machinery
  below is not implemented yet: today a caller rechecks the subtree that changed, which is correct and
  cheaper than the whole document, but it does not yet know that a *referencing* node's rule may have
  changed too. What the seam is worth is measured rather than assumed - see
  [What a full recheck costs](#what-a-full-recheck-costs): one node of a 7 501-node feature model costs
  0.001 ms against 3.3 ms for the whole document.
- **The consumers are two.** `SampleConstraints` in `Projectional.Samples.Checks` is a language's own
  sentence (a rule needs a condition). `ScopeConstraints` in `Projectional.Semantics/Scopes/` is the
  framework's: `AmbiguousReferenceConstraint` reports a reference whose name no longer identifies the
  element it points at, which is the finding that closes the scope model. It reads the target's
  **current** name rather than the `resolveInfo` it was written with, because a rename does not rewrite
  the stored text and resolving it would report names no user ever sees; and it stays quiet on an
  unresolved reference, which is already visibly wrong. The app composes both and merges the findings
  with the check catalogue's in `PublishFindings` - one list, because the panel and the markers must not
  be two answers to "what is wrong". It also passes `editor.Session.Scopes` to the engine, so a finding
  about a name is about the scope the editor actually uses rather than the default chain.
- **A rule can also be written down.** `Projectional.ConstraintRules` is the constraint language: a
  `constraints` folder of documents lowers into this same set, so a language defined in project files
  states its own rules ([language-projects.md](../language-projects.md)). The app checks each document with
  the rules of the language it is written in (`MainWindow.PublishFindings` reads `DocumentTab.Constraints`,
  which comes from `LanguageProject.Constraints`), and the sample language's rules stand in where a caller
  passes none - so a document of a language without a `constraints` folder behaves exactly as it did.

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

#### What a full recheck costs

Profiling has now spoken about the part that decides the order of the work, and the answer is
that incrementality is the third problem rather than the first.
`tools/Projectional.Benchmarks` builds a clean document of a given size, lays it out, runs the
constraints, runs the checks, and rechecks one node: best of five runs, Release build, on a
twenty-core machine with nothing else loading it. A clean document means no findings, so the times are
traversal and nothing else. Two runs of the same table differ by up to tens of percent, so read the growth
and not the last digit - the question these tables answer is "linearly or quadratically", and that answer
is stable.

The rules language - `n` rules, `2n + 1` nodes, no references:

| rules | nodes | layout | constraints | checks | both | one rule |
| --- | --- | --- | --- | --- | --- | --- |
| 100 | 201 | 0.9 | 0.2 | 1.0 | 1.1 | 0.002 |
| 1 000 | 2 001 | 7.2 | 1.6 | 5.8 | 9.5 | 0.002 |
| 5 000 | 10 001 | 43.7 | 1.5 | 7.3 | 8.7 | 0.000 |
| 20 000 | 40 001 | 123.1 | 6.6 | 48.2 | 52.1 | 0.000 |

The feature model - `n` features and `n / 2` constraints of two references each, `1.5n + 1`
nodes:

| features | nodes | layout | constraints | checks | both | one constraint |
| --- | --- | --- | --- | --- | --- | --- |
| 100 | 151 | 0.1 | 0.1 | 0.2 | 0.3 | 0.002 |
| 500 | 751 | 0.5 | 0.5 | 0.9 | 1.4 | 0.002 |
| 2 000 | 3 001 | 1.9 | 2.0 | 4.5 | 6.2 | 0.002 |
| 5 000 | 7 501 | 15.1 | 3.3 | 20.7 | 23.5 | 0.001 |

All times in milliseconds. Three consequences, and they decide the order of the work:

- **Layout is linear and is the largest cost the editor already pays.** `EditorSession` rebuilds
  tree and layout on every committed change, so 123 ms at 40 001 nodes is the floor a keystroke
  costs today - and no incrementality of *constraints* can go below it. Of that number roughly a
  third is the layout and two thirds the tree build, after the layout's per-cell bookkeeping was cut
  from 503 to 156 bytes and its time from 286 ms to 35 ms on the same 280 011 cells
  ([projection.md](../projection.md)). Incremental layout is still a separate problem from incremental
  validation, and the bigger one for a document without references.
- **Checking a document without references is linear** (5.7 ms at 40 001 nodes), so the
  design's premise holds there: a full recheck is affordable and the dependency sets would
  buy almost nothing.
- **Checking a document with references used to be quadratic, and is linear now.** The first
  run of this table reported 2 232.8 ms for 7 501 nodes: `AmbiguousReferenceConstraint` asks the
  scope for every reference, and `DefaultScopeProvider` collected the named nodes of each level to
  answer - where the nearest level is "my siblings", i.e. the whole document in a flat model. The
  per-project name index ([language.md](../language.md#what-a-scope-costs)) answers that question
  without walking, and the same document now costs 2.3 ms. The seam was never the problem: one
  node alone cost 0.9 ms before and 0.001 ms after.

So what is left is not the dependency sets. A full recheck now costs 6.6 ms (rules) and 3.3 ms
(features) against a rebuild of 123 ms and 15.1 ms on the same keystroke, so incremental
*validation* would optimise the smaller of two costs; what that rebuild is spent on is the tree and
the layout, not the rules (open question 111). The measurement is repeatable - `dotnet run -c Release
--project tools/Projectional.Benchmarks` - and a change to `ScopeConstraints`, to the index or to the
layouter that made the table worse would be visible the same day.

### Language of constraints

C# and **written rules**, since the constraint language landed ([language-projects.md](../language-projects.md)): a
`constraints` folder holds one document per rule, and `ConstraintLowering` turns each into the same `IConstraint` a
C# language writes, so nothing downstream changes. A rule names the concept it is about (or names none, which makes
it a rule about every node), says what is wrong, how much it matters, and asks one of a closed set of questions: is
this single-child slot empty, is this property still the default its concept declares, has this value whitespace
around it, does this reference point at nothing.

The set is closed on purpose. A general expression body would mean shipping an interpreter for a language whose only
job is to say what is wrong - the same line the type rules draw by having three bodies instead of a statement list
([§The declared form](types.md#the-declared-form)). A rule that cannot be lowered - no message, no body, a slot the concept
does not declare or declares as another kind - is a `ConceptDefinitionException` at load, where the author is
looking, because a rule that could never fire is the one failure nobody sees from the outside.

The parity rule of [ADR 0014](../../adr/0014-both-authoring-paths-are-permanent.md) applies here too:
`ConstraintLoweringTests` writes the rules the shipped catalogue implements as models and demands the same findings -
at the same places, with the same severities - as `Projectional.Checks` produces on one document.

Constraint order within a concept is
unspecified; constraints must not depend on each other's results.

