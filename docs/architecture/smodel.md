# The smodel language: queries over the tree

Status: **planned** (M6). Nothing of this exists. The sources are MPS' four pages on the language -
[the language](https://www.jetbrains.com/help/mps/smodel-language.html),
[its types](https://www.jetbrains.com/help/mps/smodel-language-types.html),
[its queries](https://www.jetbrains.com/help/mps/smodel-language-queries.html) and
[its modification operations](https://www.jetbrains.com/help/mps/smodel-language-modification-operations.html).

## What "compatible with MPS" can mean here

Compatibility is claimed at exactly one level, and the other two are named as dropped so nobody
assumes more than is delivered.

| Level | Verdict |
| --- | --- |
| **File format** - MPS' `.mps` instance files for `org.jetbrains.mps.lang.smodel` models | **no**. Different metamodel, different serialization (ADR 0005), and MPS' nodes carry Java type information this framework has no concept for. Loading MPS models is a migration tool, not this |
| **Concept keys** - `jetbrains.mps.lang.smodel.*` | **no**. Keys here are the framework's own (`Smodel.Parent`), because a key is also the identity a projection, a check and a reference resolve by, and importing a foreign namespace would silently promise a Java API |
| **Query shapes, operation names and meanings** | **yes**. A query written against MPS' vocabulary reads the same here: `parent`, `ancestors<concept = X, +>`, `descendants<...>`, `children<link>`, `isInstanceOf`, `as`, `:eq:`, `containing root`, `concept/Name/`, `node-ptr/.../`. The point of the language is that a language author who knows MPS needs to learn no new words |

A program is *source*-portable in the direction MPS -> here, with the two documented differences
below (no modification operations in the language, and an absent node is typed as optional).

## Why, and what is missing today

Everything the language asks for exists in small pieces, and none of them is reachable from a
document:

- **Navigation is C#.** `Node.Parent`, `ParentSlot`, `IndexInParent`, `GetChildren`, `GetChild`,
  `GetReferenceTarget`, `IsAncestorOf`, `IsSelfOrDescendantOf`, `ChildrenInDocumentOrder` - all
  public, none of it expressible as data. A check that asks "does this state have an incoming
  transition" is a C# class, so it cannot ship with a language written in a folder.
- **Concepts are C#.** `ConceptRegistry.Get`, `Concepts`, `Concept.SuperConcepts`,
  `Concept.IsKindOf` exist; a document cannot name a concept or walk the hierarchy.
- **There is no node value and no node type.** `Value` has `IntValue`, `FloatValue`, `BoolValue`,
  `StringValue` and `DateValue`; `IType` has the primitives plus records, lists, optionals, names and
  `UnknownType`. A query's answer has no representation at either end.
- **There is no subtree walk a document can reach.** `Node.SelfAndDescendants()` exists but is
  `internal` to `Projectional.Core`, and the walk is written **five more times** in the tree:
  privately in `Model` (which shares the assembly and could call it), and again in
  `Checks/NodeWalk`, `Editing/EditorSession`, `Checks/DuplicateRuleNames` and
  `Workbench/Checks/DefaultNames` - which cannot see the internal one, so each wrote its own. That
  is what a missing public walk costs, and publishing it is the first thing Q1 does.
  `Node.ChildrenInDocumentOrder()` answers the **direct** children only; the name is a trap that
  already cost two failing tests.

That unpublished walk is the reason this is a feature and not a refactor: publishing it is the same
piece a generator rule's *condition* would need ([model-generation.md](model-generation.md), which has
no condition today), a test that names a node needs ([language-tests.md](language-tests.md)) and a
host's own "find in this document" needs - and until a document can say `descendants`, each of them
writes another copy.

## Where it lives

`Projectional.Smodel` is a **language package** and ships all three of its aspects itself, because
the evaluation rules are declaration, not behaviour: `IEvaluationRule`, `ITypeRule` and `Notation`
all live in `Projectional.Language`, so a query language needs nothing above it. It references
`Projectional.Expressions` and `Projectional.Language` - the same shape as `Projectional.Samples`,
which borrows `Expressions` for its conditions.

It is **not** a query language *inside* an aspect block. That distinction is what keeps the older
decision in [language-authoring.md](language-authoring.md#why-it-has-to-look-like-mps) intact: an
aspect block stays a closed rule shape, and anything that wants logic still uses C#. Here a
*document* is a query, asked by a host or by a test - which is what MPS does with smodel in
actions and refactorings, except that MPS embeds it in BaseLanguage code and this framework makes
it a language of its own.

## The types

| MPS | Ours | Note |
| --- | --- | --- |
| `node<C>` | `NodeType(C)` - a new `IType` | accepts a node whose concept `IsKindOf(C)`; `UnknownType` accepts it, as always |
| `nlist<C>` | `NodeListType(C)` | a `ListType` whose element type is a `NodeType`; an empty list is `nlist` of anything |
| `node-ptr<C>` | **dropped** | a reference slot already stores an id plus `resolveInfo` (invariant 6); a pointer value as well would be a second way to name a node |
| `model`, `model-ptr` | `ModelType` only | there is one project in memory and no repository to resolve a pointer against |
| `concept<C>` | `ConceptType` | |
| `conceptNode<C>` | **dropped** | deprecated in MPS |
| `search scope` | **dropped in Q1** | `ScopeOf(slot)` exists in the semantics layer and is worth a slice of its own once a consumer asks for it |
| `reference`, `association` | **dropped in Q1** | the values exist (`NodeReference`), the literals (`link/Concept : slot/`) need a link-literal concept and no consumer asks yet |
| `enummember<E>` | **n/a** | an enumeration property holds a string here; there is no member node to name |

Two values come with the types: `NodeValue(Node)` and `NodeListValue(IReadOnlyList<Node>)`, plus
`ConceptValue(Concept)` and `ModelValue(Model)`. Values are content-compared records; a node value
compares by **id**, not by structure, because that is what "the same node" means in this model.
Structural equality is MPS' separate `matches` operation and needs its own deep compare.

### The one semantic difference: an absent node is optional

MPS is deliberately null-liberal: a property read on a null node answers the type's default, a
children read answers an empty list, and a navigation that finds nothing answers null. This plan
keeps the *meanings* and types the *absence*:

- `parent`, `next-sibling`, `ancestor`, `first` answer `NodeType(C)?` - an `OptionalType`, which
  already exists. `children`, `ancestors`, `descendants` answer `nlist<C>`, never null.
- **The dot navigates through an absent receiver**, so `state.transition.target.initial` is legal
  and answers absent when any step of it is - MPS' null-safe dot, expressed with the optional the
  type system already has.
- `isNull`/`isNotNull` test an optional; `as C` on an absent node answers absent; `: C` on a
  mismatch **fails the evaluation** with a message naming the node, which is MPS'
  `NullPointerException` in the only form this framework reports problems.
- The one place this is stricter than MPS is a query that *consumes* a possibly-absent node where a
  node is required (`size`, `first`): the framework's rule that a plain type does not accept an
  optional applies, so the author writes the `isNotNull` MPS would have let them skip. It is a
  finding, not a different answer.

An **unresolved reference** resolves to absent, never to a failure: a query has to survive the
degraded reference that ADR 0004 keeps instead of dropping.

## The operations

Concept keys are identifiers, so MPS' multi-word operation names become `Smodel.ContainingRoot`
drawn as `containing root`, and `super-concepts/all` becomes `Smodel.SuperConceptsAll` drawn as
`super-concepts/all`. The **drawing** is what a user reads and it is the compatibility surface; the
key is this project's business.

| Group | MPS operations | Ours |
| --- | --- | --- |
| navigation | `parent`, `next-sibling`, `prev-sibling`, `next-siblings<+>`, `prev-siblings<+>`, `siblings<+>`, `ancestor`, `ancestors<...>`, `descendants<...>`, `children<linkQualifier>`, `containing root`, `model`, `index`, `containingRole` | all, on the members of `Node` that already exist (`ParentSlot` is `containingRole`, `IndexInParent` is `index`, `IsRoot` finds the root) - except `descendants`, which needs the new walk |
| concept | `concept` operation, `concept/Name/`, concept switch, `isInstanceOf`, `isExactly`, `: C`, `as C`, `ofConcept<C>` | all; `concept/Name/` lowers through `ConceptRegistry.Get` at load, so a concept that does not exist is a definition error rather than a query that answers nothing |
| equality | `:eq:`, `:ne:`, `matches` | `:eq:`/`:ne:` compare ids; `matches` is a deep compare and arrives in Q3 |
| collections | (MPS keeps these in its collection language: `size`, `isEmpty`, `first`, `last`, `contains`) | a small set in the same package, because there is no BaseLanguage collection library to borrow. **Named as a deviation** so a port of an MPS query does not look for `jetbrains.mps.lang.collections` |
| model | `roots(C)`, `rootsIncludingImported()`, `nodes(C)`, `nodesIncludingImported()`, the `#expr` and `<all>` forms | `roots`, `nodes` with a concept-or-expression argument. The `IncludingImported` forms are dropped: a model has no import list yet, and `DefaultScopeProvider`'s project level stands in for it |
| concept hierarchy | `super-concepts/all<+>`, `super-concepts/direct<+>`, `sub-concepts(model, scope)` | all three, over `Concept.SuperConcepts` and a walk of `ConceptRegistry.Concepts`. The scope argument is dropped |
| roles | `hasRole(Concept : child)` | kept - it is how a check asks "is this child in the role I expect" |

**Parameters are declared slots, not a key-value list.** MPS writes `ancestors<concept = X, +>`;
here `Smodel.Ancestors` declares a `concepts` slot (a `Many` slot of `Smodel.ConceptRef`) and a
boolean `includeSelf`, drawn with `Conditional` so the `+` appears only when it is true - the same
rule that makes the word `abstract` in the structure language and `final` in the state machine.
A generic `<name = value>` list would be a stringly lookup the notation registry could not check;
a slot is checked at load, and `concept` naming a concept the language does not have becomes a
definition error.

## What this deliberately does not take: modification

MPS' smodel modifies the model (`=`, `set`, `add next-sibling`, `new node<>`, `copy`,
`replace with`, `detach`). **None of that is in this language**, and the reason is not laziness:

- every one of them already exists here as an `EditAction` - `InsertChildAction`,
  `InsertNodeAction`, `MoveNodeAction`, `ReplaceAction`, `SetPropertyAction`, `WrapInAction`,
  `PasteFragmentAction` - which is what carries the transaction, the undo step and the preview
  ([ADR 0017](../adr/0017-preview-is-a-discarded-edit.md)). A modification operation in a query would
  be a second, un-previewable way to change a model, and the model's own invariants are only
  enforced on the paths `Model` owns.
- **Purity is a property of the language, not a promise.** Because there is no concept for a
  modification, no document can express one: the illegal program is unrepresentable, the same way
  `Applicability` makes an illegal child unrepresentable. A hand-written C# rule could still call
  `Model.DeleteSubtree`, and that seam is not policed for any aspect - but a *declared* query
  cannot mutate by construction.
- A host that wants "run this query, then act" gets a list of nodes and calls the actions it
  already has.

The same reasoning drops `downcast /`: there is no Java API below the model, and reaching one is
exactly what "the model is the only source of truth" forbids.

## The notation

MPS' shape, drawn with cells that already exist:

```
states
  .descendants<concept = Sm.State, +>
  .where(isFinal == true)
```

- a receiver, then `.`, then the operation word (`ed.Keyword`), then an optional parameter block
  `ed.Punct("<")`, the declared slots separated by `", "`, `ed.Punct(">")`.
- `+` is a `Conditional` on the boolean slot, never always-drawn: `ancestors<>` may not print an
  empty parameter list, because an empty box is a caret stop the user has to skip.
- casts draw as `: Sm.State` and `as Sm.State`; `concept/Name/` and `node-ptr/.../` draw with
  their slashes as literal punctuation, since that is what makes them recognisable to a reader
  coming from MPS.

## Composition with `Expressions`

A query is an expression, and the framework already has one: `Projectional.Expressions`. Two ways
to compose, and the second is what the repository does today:

1. **Subclassing** `Expressions.Expression` from each `Smodel` concept. It costs the least for the
   author (`size(xs) > 3` just works) and it is what a `ConceptDeclaration` allows, since
   `Expressions.Expression` is abstract. Its own type rules then refuse a node where a number
   belongs, which is the wanted answer.
2. **Borrowing** the declarations in a `[.. ExpressionsLanguage.ConceptDeclarations, .. own]`
   list, which is how `Projectional.Samples` gives its rules a condition. No `Smodel` concept is an
   `Expression`, so a query cannot be nested under `&&`.

Recommend (1) for the boolean and collection parts of the vocabulary, because a query that cannot
say `isFinal && !hasIncoming` is a query that sends the user back to C#. Q1 delivers the navigation
alone (2 is enough there) and Q2 adds the subclassing once a `where`-style filter needs it.

## Who consumes it

| Consumer | What it asks | When |
| --- | --- | --- |
| a **host**, through the toolkit | "which nodes in this document match this query" - a query panel, a rule editor, a refactoring | after Q1 |
| **model generation** ([model-generation.md](model-generation.md)) | a rule today applies to **every** node of its source concept - `ReductionRegistry` answers one rule per concept and has no condition - so a mapping that is only sometimes right has no way to say so. A query is what such a condition would be written in | with M6's generator |
| **language tests** ([language-tests.md](language-tests.md)) | naming the node an assertion is about, which is that plan's open decision 1 | with T4 |
| **declared intentions and actions** (M6) | the guard of an action, which is the same query shape MPS uses inside them | after Q3 |

## Slices

| Slice | Content | Effort |
| --- | --- | --- |
| Q1 | `Projectional.Smodel` with the five types, `NodeValue`/`NodeListValue`/`ConceptValue`/`ModelValue`, the navigation and concept groups, the public subtree walk in Core (and the four copies in higher layers replaced by it), notation, and an xunit corpus of queries answered against the sample languages | 1.5 weeks |
| Q2 | `Expressions` composition (subclassing), the collection set, `isNull`/`isNotNull` with the optional typing, and the failure messages that name the node | 1 week |
| Q3 | model queries (`roots`, `nodes`) with the `#expr` and `<all>` forms, `matches`, and the query panel in the workbench | 1 week |
| Q4 | `hasRole`, `containingRole`, link literals, `search scope` - each only with the consumer that asks | open |

## Non-goals

- **No modification operations.** See above; actions own mutation.
- **No repository, no model pointers, no concurrent access** (`jetbrains.mps.lang.access`). One
  project in memory and one thread that matters; a lock vocabulary would be a vocabulary with no
  meaning.
- **No `downcast`.** No API below the model.
- **No `.mps` import.** Reading MPS' smodel models is a migration tool (M6), not this language.
- **No query caching.** A query is answered when asked; the model changes underneath it, and a
  cached answer would need a second invalidation path.
- **No unbounded scans in a query.** A query runs under the interpreter's step budget (10 000) and
  its cancellation token, so `doc.nodes(...)` over a large model stops with a *budget* failure
  rather than freezing a host. A host that wants a full scan uses the `Model` API.

## Open questions

1. **How far to follow the null-liberal typing.** Recommend: optional types plus a null-safe dot,
   as above. The alternative - a `nullableNode` that behaves exactly like MPS and is only checked
   when the author asks - is faithful and weaker; it is the fallback if source portability turns
   out to matter more than the finding.
2. **Is a query one expression or a small program?** Recommend one expression in Q1-Q3: it keeps
   the language free of variables, and the interpreter already supplies `Variable(name)` for a host
   that wants to pass values in. `let` becomes worth it when a query wants a name for a sub-query
   it uses twice.
3. **Does `concept/Name/` name a concept of *this* language or of the registry?** Recommend the
   registry the document is opened with, because a check may legitimately ask about another
   language's concept in a composed project - and the load error then only fires for a key nobody
   declared.
4. **Does the notation keep MPS' `.` or become postfix words?** Recommend the `.`: it is what
   makes a ported query readable, and the framework has no other postfix operator to be confused
   with.
