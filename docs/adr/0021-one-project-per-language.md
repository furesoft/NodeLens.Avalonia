# ADR 0021 — One project per language

- Status: Accepted
- Date: 2026-11-19
- Supersedes: the "a language that needs a check or an intention ships a *second* package beside it"
  guidance in [ADR 0019](0019-toolkit-is-the-language-surface.md), and the per-package split recorded in
  [import.md](../architecture/import.md) and [worked-examples.md](../architecture/worked-examples.md)
- Related: [ADR 0014](0014-both-authoring-paths-are-permanent.md),
  [ADR 0019](0019-toolkit-is-the-language-surface.md), [overview.md](../architecture/overview.md)

## Context

Every shipped language had grown into a *set* of projects: `Projectional.Json`, `Projectional.Json.Checks`
and `Projectional.Json.Intentions` beside each other, and the same three-way split for XML, the samples and
the three worked examples. The reason was sound when it was written - a check produces edits, so it needs
`Projectional.Editing` and `Projectional.Semantics`, and keeping those out of the language assembly made
"a language is headless" a property of the build rather than a rule somebody had to remember
([ADR 0019](0019-toolkit-is-the-language-surface.md) rule 1).

The cost was paid in a currency the framework cares about as much:

- **Nineteen projects for eight languages**, each with its own `.csproj`, its own entry in the solution
  and its own reference list. Adding a check to JSON meant editing three projects and two consumers.
- **The split was invisible at the point of use.** Nothing about `Projectional.Json.Checks.JsonChecks`
  tells a reader that it belongs to JSON; the name does, the folder does not, because the folder was a
  sibling rather than a child.
- **The boundary leaked anyway.** A host that wanted a reference language took the language *and* its
  checks *and* its intentions, which is `Core`, `Language`, `Projection`, `Editing`, `Semantics` and
  `Checks` - the same closure a merged project would have. The split only helped a consumer that wanted
  the language **without** its behaviour, and no such consumer exists: `Projectional.Workbench`, the
  screenshots tool, the host sample and the tests all take the whole set.

## Decision

**A language is one project.** Its concepts, notation, importer, generator, types, constraints, scopes,
checks and intentions are one assembly, grouped by folder:

```
src/Languages/Projectional.Json/
  Checks/        namespace Projectional.Json.Checks
  Intentions/    namespace Projectional.Json.Intentions
  ...            namespace Projectional.Json
```

Every language lives under `src/Languages/`, and the sample languages are nested one level deeper in
`src/Languages/Samples/` - a folder rather than a second `Samples/` beside the repository's own `samples/`,
which holds the host application. `src/` itself keeps the framework and the host, so the folder list answers
"is this a product, a language, or an example" at a glance.

Three rules follow:

1. **The namespaces do not move.** `Projectional.Json.Checks` stays the namespace of the JSON checks and
   `Projectional.Xml.Intentions` the namespace of the XML intentions, so a host that names one of those
   types is unaffected by where the code lives. The folder says which language the code belongs to; the
   namespace says which part of it - the two are allowed to differ here, because a namespace is an API and
   a folder is a place.
2. **The language project references what its behaviour needs**, which for a language with checks means
   `Projectional.Checks`, `Projectional.Editing` and `Projectional.Semantics`. This is a downward arrow in
   the layer diagram: a language package sits above the behaviour band it uses, exactly as `Samples`
   already sat above `Expressions`. It is not a cycle - `Projectional.Checks` references
   `Projectional.Expressions`, and no behaviour package ever references a language.
3. **A language that must be consumable without the editing layer may still split.** Nothing stops a
   language from publishing its concepts and notation on their own; what changes is that this is now the
   exception, taken because a consumer needs it, rather than the shape every language is born in.

`Projectional.Toolkit` is unchanged: a language *author* still declares against the toolkit, which still
aggregates `Core`, `Language`, `Projection` and `Persistence` and still excludes `Editing` and `Semantics`.
The shipped languages in this repository are packages rather than authoring projects, and the two roles
have different defaults.

## Consequences

- Eleven projects disappear: `Projectional.Json.Checks`, `Projectional.Json.Intentions`,
  `Projectional.Xml.Checks`, `Projectional.Xml.Intentions`, `Projectional.Expressions.Intentions`,
  `Projectional.Samples.Checks`, `Projectional.Samples.Intentions`,
  `Projectional.Samples.FeatureModel.Checks`, `Projectional.Samples.StateMachine.Checks`,
  `Projectional.Samples.SqlSchema.Checks` and `Projectional.Structures.Intentions`.
- **"A language is headless" stops being true of the assembly and stays true of the code**: `Language`,
  `Projection` and `Persistence` still cannot see the editor, and a language's concepts and notation still
  compile without it. What a consumer of `Projectional.Json` now loads transitively is the behaviour band
  it chose to include by referencing the language. That is the price, and it is recorded here rather than
  discovered later.
- `LayerDependencyTests` no longer checks the two intention packages by name (they are gone). It checks the
  merged language assemblies instead: each references `Projectional.Editing`, and none references
  `Projectional.Avalonia` or `Projectional.Workbench`.
- The published API of the merged assemblies grows by the types that used to live beside them. Since both
  namespaces keep their names, no consumer's source changes - only its `ProjectReference` list shrinks.

## Alternatives considered

- **Keep the split and only group the files** (one folder tree, several projects). It fixes the navigation
  complaint and none of the others: the solution still lists three entries per language, and the reference
  lists still have to be kept in step by hand.
- **Merge and flatten the namespaces into `Projectional.Json`.** Tidier as a rule ("one namespace per
  assembly"), but every consumer and every test would have to change a `using` for a purely cosmetic gain,
  and the namespaces are the part of this decision users can actually see.
- **Deliver the checks as a declarative aspect instead of code** (`Projectional.ConstraintRules` can state
  a rule; `CheckSet` cannot). That would remove the dependency rather than hide it, and it is the right
  long-term answer for a rule a language *states*. It does not cover a check that needs the scope, the
  reference index or an operator table, which is why it is not this ADR's subject.
