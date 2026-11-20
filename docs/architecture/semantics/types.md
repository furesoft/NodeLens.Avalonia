# Types

> Part of [semantics.md](../semantics.md), the design of record for the semantic aspects. The pass order,
> the diagnostics, the error recovery and the performance numbers stay in the parent.

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

`Projectional.Language/Types/` holds the model; the primitive, structural, named and `Unknown` kinds are
done.

**It lives in `Projectional.Language`, beside the notation and the typing rules, because a type is a
statement about what a language means.** The layer diagram puts `Semantics` above `Language`, so a
language package that declares a type rule - the expression language does - could not reference it from
there. The consumers sit above and read the registry the language hands them.

| Type | Job |
| --- | --- |
| `IType` | `Name` and `Accepts(actual)` - assignability, asked in that direction |
| `ISubtype` | `IsSubtype(actual, expected)` - the language's subtype edges, asked in the same direction and deliberately **stricter** than `Accepts` |
| `SubtypeRelation` | those edges: a primitive is its own subtype by name, a declared edge is transitive, and `unknown` stands in no edge at all |
| `TypeVariable` | a type a rule cannot name yet, written `?elementType`; distinct from `UnknownType`, which means nothing more will be known |
| `TypeEquation` | one relation a rule states - `Equal`, `Subtype` or `Supertype` - whose sides may be variables |
| `TypeSolver` | the fixpoint over those equations, with a step budget; a bounds solver, not a unification engine |
| `TypeSolution` | what each variable turned out to be, why the solve stopped, and what it cost |
| `JoinType` / `MeetType` | a type expression: "either of these" / "both of these", named `int|string` and `int&Amount` |
| `TypeLattice` | reduces a join or a meet to one type from the relation, or answers `unknown` when the edges do not determine one |
| `PrimitiveType` | wraps a `PropertyType`, so a language declares its primitives where it already declares properties |
| `RecordType` | a fixed set of named fields, compared **by name, not by order** |
| `ListType` | its element type; a length is a fact about a value, not about its type |
| `OptionalType` | the inner type or nothing; accepts the inner type, and nesting is not collapsed |
| `NamedType` | a name for a type: `Name` is what it is called, `Expansion` is what it means |
| `UnknownType` | the answer when nothing says; accepts and is accepted |
| `ITypeRule` | the aspect: `TypeFor(TypeContext)` |
| `TypeContext` | the node and `TypeOf(node)` / `TypeOfChild(slot)` - the whole of local propagation |
| `TypeRegistry` | the rules, keyed by concept, inherited by the concepts below it |

Four rules worth knowing:

- **`expected.Accepts(actual)` is the only question a slot asks.** It reads the way the rule does, and a caller that
  got the direction wrong would be comparing two types for equality - a different and much weaker
  question. It has a **default implementation** on the interface, which answers with
  `SubtypeRelation.Primitives`: a type a language adds needs only its `Name`, and a type whose answer is more
  lenient says so where the leniency belongs. Being a default member it is reached through `IType`, which is why
  every consumer here holds an `IType` rather than a concrete class.
- **Subtyping is the second question, and it is strict.** `ISubtype.IsSubtype(actual, expected)` answers what the
  language's declared edges say, and `SubtypeRelation` is where they live: a type is its own subtype, a primitive
  is its own subtype **by name**, a declared edge is transitive, and `unknown` stands in **no** edge - neither as
  the subtype nor as the supertype. That last one is the whole reason the two questions are separate: `Accepts` is
  lenient about `Unknown` so one mistake does not become fifty findings, and a type rule must never be told that a
  guess satisfies `int`. Collapsing them is how `Unknown` leaks out of the one place it belongs.
- **`Unknown` accepts and is accepted**, which is what suppresses follow-on errors: one mistake in a
  condition must not produce a finding on every expression that reads it. It is therefore **not** a
  wildcard to rely on - a caller that must not proceed on a guess (a generator) asks
  `type is UnknownType` explicitly. That is the "cannot leak into generation unnoticed" rule: the type
  does not stop the caller, it makes the caller say what it does about not knowing.
- **A concept with no rule is `Unknown`**, not an error. Nobody said, so nothing is known, and every
  consumer is told that rather than handed a guess.
- **A record is compared by name, not by order.** Two records that differ only in the order their fields
  were written are the same shape; the order is kept for display.
- **An optional accepts its inner type, and a plain type does not accept an optional.** "May be absent"
  is a promise about what a reader has to handle, not a different kind of value - so a slot that promised
  a value must not be handed one that might not be there. Nesting is not collapsed: `int??` says the
  absence may itself be absent, and a language that does not want that should not declare it.
- **Every structural type accepts `Unknown`**, for the same reason `PrimitiveType` does: one mistake must
  not become a finding on every expression that reads the value.
- **A name is a spelling, not a kind of value.** `NamedType` keeps `Name` (what the language calls it) and
  `Expansion` (what it means) apart, and it stays structural: `Accepts` looks through a name on both sides,
  so `Amount` that means `int` is accepted by `int` and two names for one type accept each other. The
  looking-through is not the alias's privilege - every `Accepts` starts with `NamedType.Unwrap(actual)`,
  because the *accepter* is who would otherwise refuse an alias. A consumer that has to know what kind of
  type it has (a generator) calls `NamedType.Unwrap` explicitly, for the same reason it asks
  `type is UnknownType` rather than relying on `Unknown` accepting everything.
- **`ITypeRule.TypeFor`, not `TypeOf`**: CA1716 forbids a reserved word as an interface member, the same
  reason `ICheck.Find` is not called `Of`. The class member `TypeContext.TypeOf(Node)` keeps the natural
  name, because the rule does not apply to it.

**The first consumer is a constraint**: `SampleConstraints.ConditionMustBeBoolean` asks
`context.TypeOf(condition)` and reports a condition that is not a truth value. `ConstraintContext`
therefore carries the `TypeRegistry` beside the `IScopeProvider`, and `ConstraintEngine` takes both - a
rule that needs a type and a rule that needs a scope are the same kind of rule.

`ExpressionsLanguage.Types` declares the expression language's types: a literal is its own type, a
parenthesised expression is as wide as what it wraps, a comparison is a `bool`, arithmetic is as wide as
its left operand, and a name is `Unknown` because what it means depends on a declaration this language
does not have. They ship **with** the language, not beside it: a host that declares a slot over
`Expression` asks the language it borrowed what may stand there, and composes nothing extra. That is the
one thing the type rules share with the notation and the typing rules rather than with intentions and
checks - those two produce edits, so they live in the language's own project beside its concepts (in
`Checks/` and `Intentions/`, [ADR 0021](../../adr/0021-one-project-per-language.md)); a type rule only answers a
question.

### The declared form

`Projectional.TypeRules` is the language a type rule is written in, and it is the first *declared
aspect*: a rule is a model, and `TypeRuleLowering` turns it into the same `ITypeRule` a C# author would
have written ([ADR 0014](../../adr/0014-both-authoring-paths-are-permanent.md)).

| Concept | Says |
| --- | --- |
| `TypeRule.Rules` | the document: one language's rules |
| `TypeRule.Rule` | `conceptKey` and a body |
| `TypeRule.Fixed` | a type written out, so it is the same whatever the node holds |
| `TypeRule.Child` | the type of one of the node's children |
| `TypeRule.Switch` | a choice between bodies, decided by a property's value, with an `otherwise` |
| `TypeRule.Case` | one value of a switch and the body it selects |
| `TypeRule.Complain` | `message`, `severity` and an optional `when`: reports instead of typing |
| `TypeRule.NotComparable` | `left` and `right`: the two child slots whose types are not interchangeable |
| `TypeRule.Replacement` | `claim`, `left`, `right` and `instead`: a claim between two patterns, and what to prove in its place |
| `TypeRule.Comparable` | `left` and `right`: two types either may be used for the other |
| `TypeRule.TypeExpr` | a type written as data, abstract: one of the four kinds below |
| `TypeRule.Primitive` | `name`: `string`, `int`, `bool`, `date` |
| `TypeRule.Record` | `fields`: a fixed set of named fields |
| `TypeRule.Field` | `name` and `type` |
| `TypeRule.List` | `element` |
| `TypeRule.Optional` | `inner` |
| `TypeRule.Ref` | `name`: a declared type, expanded where it is used |
| `TypeRule.Named` | `name` and `type`: a type the language declares, written as a root of the types model |

**A type is a separate family from a body, and that is the load-bearing split.** A body may read the node
(`type of left`, `switch on operator`); a type may not. Mixing them would allow a record field whose type
depends on the rule's node - a question with no answer, because a field is part of a type and not of a
node. `TypeRule.Fixed` therefore holds a `TypeExpr` rather than a name, which is also why `type int` reads
exactly as it did before.

**The drawing of a type expression is exactly what `IType.Name` prints.** `(amount: int, currency:
string)`, `[int]`, `int?` - so the rule an author reads and the sentence a finding quotes about it are the
same text. `ATypeExpressionDrawsExactlyWhatTheTypeIsCalled` in `TypeRuleNotationTests` is that invariant as
a test. It holds for a *complete* type expression: an empty record draws the shared `<< ... >>`
empty-list placeholder, because an empty slot has to stay visible and fillable.

**Bodies, not a statement list.** MPS' typesystem is imperative - `do { typeof(node) :== int; }` -
and copying that would mean shipping an interpreter for a language whose only job is to answer one
question per node. A fixed type, a child's type and a switch over a property cover what a DSL's rules
actually say, and each lowers to a delegate the C# path could have written by hand. A complaint is the
fourth: it answers no type at all but states a message, optionally gated by a condition over two child
slots, and it lowers into an `IConstraint` rather than a rule.

**A declared type is a root of the types model, not a second list inside the document.** So a types model
keeps one shape a reader has to learn: the rules, and the names they may use. `TypeRule.Ref` names one by
its `name` - a string the lowering resolves, the same way `TypeRule.Rule.conceptKey` names a concept -
because the alternatives are worse here: a reference slot would buy completion and `<unresolved>` drawing,
and it needs a scope provider over the document, which is a slice of its own.

Three things are load-time errors, because nothing later could decide them: a name the model does not
declare, a name declared twice, and a name written in terms of itself (`type A = [A]`, or `A` through
`B`). The last is not a recursive type but a definition with no base - it would expand forever - so the
lowering carries the chain of names being expanded and reports it with the chain in the message.

**A body that cannot be lowered is a load-time error**, not a rule that silently answers `Unknown`: a
rule with no body, a type name the framework does not have, a fixed body or a field with no type, a record
that names one field twice, a switch that declares one value twice, a switch with no `otherwise`, and the
three alias errors above all throw `ConceptDefinitionException` when the language is loaded, where the
author is looking.

**Only the primitives are nameable by a `Primitive` node.** An enumeration is a type a language declares,
and a rule that named one would have to be resolved against that language's registry - the declared
stage's job, not thisone's.

**The parity rule applies to types too.** `TypeRuleLoweringTests` writes the sample's type rules as a
model and asserts that the declared registry and `ExpressionsLanguage.Types` answer the same type for
every node of the sample document - the same "two authoring paths, one answer" check the language layer has.

### Planned: parity with MPS' typesystem

**Planned, not built** - except T1, T2, T3, T4, T5, T7 and T8's replacement, which are delivered. This section is the design of record for the next
stage of the type system, and what it records is a *compatibility target*: the rules a language author can write in
MPS' typesystem cookbook should be expressible here, in this repository's vocabulary, without falling back to C#. The
cookbook is <https://www.jetbrains.com/help/mps/cookbook-type-system.html>; it is the acceptance list for these
slices, not a specification to copy.

Today the type layer answers **one** question per node - `ITypeRule.TypeFor(node)` returns an `IType` - and a type
answers one: `expected.Accepts(actual)`. That is enough for a DSL whose types are written down, and it is *not*
enough for the cookbook's central case: tying the type of one node to the type of another (`infer typeof(a) :<=:
typeof(b)`), which needs variables over types and a solver. `TypeVariable`, `TypeEquation` and `TypeSolver` now
exist for that (slice T2), and `ITypeRule.EquationsFor` is the door a rule uses to state relations instead of
answering a type. `TypeRegistry.EquationsFor` is the caller that door was missing (slice T4): it hands a node's
declared relations to a `TypeSolver`, so a rule table that needs solving is solved without teaching every consumer
to ask a second question - `TypeFor` stays what everything asks. What is left is T8's other half, with T6 answered
rather than built.

The plan keeps the property that makes the current layer cheap to reason about: **no statement list**. MPS'
`do { … }` block is imperative and its power comes from mutation plus anti-quotation, which is exactly the
interpreter this repository has refused four times (constraints, templates, types, intentions). The declarative
equivalent is a **type equation**: a rule states relations over types, and a small solver finds an assignment. So
"compatible with MPS" here means *every cookbook rule has a declarative spelling*, not that a `do` block can be
typed in.

#### What each kind of MPS rule becomes

| MPS (cookbook section) | Its meaning there | Here: today | Here: planned |
| --- | --- | --- | --- |
| inference rule, equality (`typeof(x) :==: <T>`) | the node's type *is* this type | `TypeRule.Fixed`, `TypeRule.Child` | `TypeRule.Equation` with an `equals` relation |
| inference rule, inequality (`infer typeof(a) :<=: <T>`) | the type must be a *subtype* | only `IType.Accepts` (conformance, not subtyping) | a subtype relation as data, `TypeRule.Equation` with `subtype`/`supertype` |
| `var elementType` + anti-quotation | a variable shared between two statements | nothing - a rule cannot name a second node's type | `TypeRule.TypeVariable`, shared by equations; anti-quotation is unnecessary because a variable is declared once and referenced by name |
| `join(A, B)` (least upper bound) | the common supertype of two types | `JoinType`/`MeetType` as types, and **`TypeRule.Join`/`TypeRule.Meet`** as their declared spellings; `TypeLattice.Join` reduces either to one type | - |
| `when concrete (typeof(x) as t)` | proceed only once the child's type is settled | every rule is already a pure function of the node, so there is nothing to defer, and a type read descends a tree so it cannot cycle | - closed: the solver's fixpoint is the ordering, and a self-referential name is refused at load |
| `error`/`warning`/`info "…" -> node` | a diagnostic from the typechecker | **`TypeRule.Complain`** as a body - a `message` and a `severity` lowered into an `IConstraint` the language's own set carries, so the existing findings path reports it; with an optional `when` body it reports only where that condition holds, and **`TypeRule.NotComparable`** is the condition ("the types of two child slots are not interchangeable") | the further condition kinds a language might want beside comparison (an arity, a name shape), each of which is a new body rather than a new path |
| replacement rule | rewrite one equation before solving (covariance, `NullType`) | **`TypeRule.Replacement`** - a document stating a claim (`<:` or `==`) between two type *patterns*, and the relations to prove instead with those patterns' variables filled in, or none at all when the claim holds outright. Lowered into a `TypeReplacement` the registry carries, so a solver built from the registry rewrites a claim the language has a rule for; the framework's own covariance for a list and an optional is stated in the same vocabulary (`TypeReplacements.Covariance`) rather than hard-coded in the solver | - |
| subtyping rule | the direct supertypes of a type | **`TypeRule.Supertypes`** - a document stating one edge (`subtype <: supertype`), lowered into the `SubtypeRelation` the registry carries; a name is one instance per document, so a rule and an edge naming it are given the same type | - |
| comparison rule | two types are interchangeable (weak/strong) | **`IType.IsComparableTo`** - the third question, asked both ways through the relation, so "interchangeable" is neither conformance nor a one-way subtype edge; `TypeSolver` asks it instead of comparing two subtype checks by hand - and **`TypeRule.Comparable`**, a document stating the pair, lowered into a `ComparisonRelation` the registry carries. The pair is not written as two edges because two edges would also collapse the types in the lattice; it is symmetric and transitive, so it means what the both-ways reading means | - |
| substitute type rule | swap the node representing a type (configuration-dependent `int`/`long`) | nothing | `TypeRule.Substitute`; last, because it needs a per-model configuration input the framework does not have |
| checking rule + quick fix | report a problem, offer a repair | `ICheck` (with a repair) and `IConstraint` (without) | **done** - the cookbook's `checking rule` is this repository's check catalogue, not part of the type system |

#### The type kinds the cookbook assumes

| MPS type | Here: today | Planned |
| --- | --- | --- |
| `PrimitiveType` (`<string>`, `<boolean>`) | `PrimitiveType` | - |
| `ClassifierType` / `RuntimeTypeVariable` | `NamedType` (a name plus an expansion) | keep; a declared name is compared by name, as MPS compares by classifier |
| `SequenceType` / `ListType` / `ArrayType` | `ListType` | `Sequence`/`Array` as declared *aliases* of one list kind, so a rule written for MPS' vocabulary has a spelling that does not lose the element type |
| `NullableType` | `OptionalType` | - |
| `TupleType` | `RecordType` | - |
| `JoinType` / `MeetType` | nothing | `Join`/`Meet` as type expressions, resolvable by the subtype relation |
| `WildcardType` / `DynamicType` | `UnknownType` covers the "suppresses follow-on errors" job | a wildcard only when a rule needs to *say* "anything", which `Unknown` deliberately is not |
| `NullType` | `Optional` accepts its inner type; there is no null literal in the shipped languages | not planned until a language has a null value to type |
| `VoidType` | nothing | a `Void` primitive, when a statement language lands |

#### Slices

Each slice is independent and each is only worth starting when a language needs it - the same rule the rest of
`docs/` follows, because a type system nobody writes rules in is a liability.

- **T1 - the subtype relation as data.** `ISubtype`/`SubtypeRelation` in `Projectional.Language/Types/`, primitive
  edges plus a declared one, answering `IsSubtype(actual, expected)`. `Accepts` keeps its meaning (conformance of a
  value to a slot's type) and gains a default implementation on top of the relation; the two questions stay
  distinguishable, because collapsing them is what makes `Unknown` leak. **Delivered**: the relation is immutable
  and transitive, `unknown` stands in no edge (an edge that names it is refused at declaration), `Accepts`'s
  default answers with `SubtypeRelation.Primitives`, and `SubtypeRelationTests` pins the distinction by asserting
  that an unknown type conforms to a primitive while being no subtype of it.
- **T2 - type variables and equations.** `TypeVariable`, `TypeEquation` (`Equals`, `Subtype`, `Supertype`),
  `TypeSolution` and a fixpoint `TypeSolver` with a step budget. A rule's answer becomes "these equations hold", and
  `ITypeRule` gains an overload that collects them while `TypeFor` keeps working for the rules that need no solving.
  **Delivered**: `TypeVariable` (`?name`), `TypeEquation` with the three kinds (the third a spelling of the second,
  read the other way), `TypeSolver` - a *bounds* solver, not a unification engine: an equality pins a variable, a
  subtyping narrows one, a claim about a compound type is rewritten by a `TypeReplacement` before it is judged (a
  list against a list decomposes into its elements, an optional into its inner, a record deliberately not), a second
  equality that disagrees is reported by the branch that compares the two concrete
  sides, and two variables stated equal stay a relation rather than being given each other's name. `TypeSolution`
  hands an undetermined variable back as itself, so a consumer sees `?elementType` instead of a type nobody proved.
  `ITypeRule.EquationsFor` is the overload, defaulting to nothing, so every rule that already answers a type keeps
  doing so. `TypeSolverTests` proves each of those, including the budget stopping a table that needs more passes
  than it is given.
- **T3 - `join` and `meet`.** Computed from the subtype relation, with `Unknown` if the two types share no supertype -
  reported, never guessed. **Delivered**: `JoinType` and `MeetType` are the type *expressions* (a slot may say
  "either of these" or "both of these" before a rule knows which), and `TypeLattice.Join`/`.Meet` reduce them to one
  type from the relation. The relation gained the enumeration the reduce needed - `SupertypesOf`/`SubtypesOf`, walked
  transitively with a worklist, because a language may declare two names for one type and recursion would not
  terminate on that. What the reduce refuses is the point: when nothing is above both, or several things are and
  none is least, the answer is `UnknownType` - the two cases reported the same way, because the answer a rule needs
  is "I cannot say" and which of the two it was belongs to a diagnostic about the rule. Two facts the tests found
  worth stating: a meet of comparable sides is simply the narrower one (the fast path answers first), and a meet is
  as lenient about `unknown` as its sides are - the strictness a meet adds is over its *sides*.
- **T4 - the declared forms of T1-T3.** `TypeRule.Equation`, `TypeRule.TypeVariable`, `TypeRule.Join`, and the
  subtyping declaration, with the load-time validations the aspect already has (unique variable names, a relation
  whose sides have the right kinds, a variable used but never constrained). **Delivered for the equations**: a
  `TypeRule.Equations` body states `TypeRule.Equation` children, each a `relation` (`==`, `<:`, `:>`) between two
  type expressions, and `TypeRule.TypeVariable` is a type expression the solver gives a type to. Three facts the
  shape forced. The relation's symbols are the ones `TypeEquation.ToString()` prints, so a rule and the sentence a
  failed solve quotes read the same text - the same invariant the type expressions already keep. A variable is an
  *identity*, not a name: two nodes writing `?element` in one rule lower to one `TypeVariable`, because relations
  between unrelated unknowns say nothing; the table lives for one rule's lowering. And the load-time refusals are
  where the value is written: the relation is an enumeration, so the model refuses a spelling the lowering never
  sees, while the lowering refuses an equations body that states nothing, an equation whose two sides are the same
  variable (true of every type and deciding none), and a variable written outside a body that states equations
  (nothing would constrain it). The type expressions were closed the same way: `TypeRule.Join` and
  `TypeRule.Meet` are the two types the layer already had but a model could not write, so a slot that accepts
  "either" or "both" is now spellable and lowers to `JoinType`/`MeetType` - the drawing being the operator the
  type's `Name` prints (`int|string`, `int&Amount`), the invariant the rest of the family keeps. **Delivered for
  the subtyping declaration**: `TypeRule.Supertypes` is a document of its own - one edge, `subtype <: supertype` -
  lowered into the `SubtypeRelation` the registry now carries, so a language's edges reach the solver and the
  lattice instead of only the framework's primitives. What the shape forced: the relation compares a named type by
  *identity*, because whether two declarations of one name are one type is the language's question, not the
  relation's - so the language answers it by handing out one instance per name. A rule writing `Ref Amount` and an
  edge naming `Amount` therefore receive the same type, which is what makes the edge join anything at all.
- **T5 - comparison rules.** `TypeRule.Comparable`, and `IType.IsComparableTo` beside `Accepts` - a third question,
  because "interchangeable" is what MPS' weak comparison means and neither accept nor subtype says it. **Delivered
  for the question**: `IType.IsComparableTo` asks the relation both ways, so "interchangeable" is a conjunction of
  two subtype edges rather than a one-way edge, and each type whose acceptance is structural overrides it so its
  comparison is structural too (a list compares its elements, a record its fields, a name its expansion, a join
  either side, a meet both). `TypeSolver` now asks it instead of comparing two subtype checks by hand - the
  consumer the machinery needed. **Delivered for the declaration too**: `TypeRule.Comparable` states that two types
  may each be used for the other, lowered into a `ComparisonRelation` the registry carries beside its edges. It is
  kept apart from the edges on purpose - two `<:` edges, one each way, would say the same about interchange while
  also putting each type in the other's supertype set, so `TypeLattice` would reduce a join of the two to one of them
  and a walk up from either would cycle; a comparison states the interchange and leaves the lattice alone. It is
  symmetric and transitive, which is exactly the answer the both-ways subtype reading gives, because two spellings of
  one fact must not answer differently. The deferral this carried is what unblocked it: the note said "nothing in the
  tree consumes one yet", and the conditional complaint's `TypeRule.NotComparable` now does, beside the solver's own
  fact check.
- **T6 - the settle pass.** An explicit order so a rule may read a child's settled type (`when concrete`), with a
  cycle reported as a finding instead of a stack overflow. Deliberately after T2: an equation solver makes most
  `when concrete` blocks unnecessary, and the cookbook uses them to work around statement ordering. **Answered,
  not built**: there is nothing to order. A rule is a pure function of the node and reads its children, so a type
  read descends a tree and terminates - the solver is where the ordering lives, and it already runs to a fixpoint.
  A cycle can only arise from a name declared in terms of itself, and the lowering refuses that at load with the
  chain printed; a type read cannot follow a reference, because a child body may only name a child slot, and the
  lowering now asks the concept registry and refuses a child body naming anything else. So the two failures T6
  was to prevent - a deferred read and a cyclic read - are precluded by construction rather than reported, and
  `TypeRule.Settle` is not planned. The registry check is what closed the last hole: without it a child body
  naming a reference slot crashed the type read instead of failing at load.
- **T7 - a type rule that reports.** `TypeRule.Complain`, lowered into `Diagnostic` with a severity, so a
  "operation is not applicable to these operands" message reaches the squiggles and the problems panel through the
  path that already exists. A complaint names its node, so no new diagnostic kind. **Delivered**: `TypeRule.Complain`
  is a body carrying a `message` and a `severity`, and `TypeRuleLowering.CreateComplaints` lowers it into an
  `IConstraint` keyed by the concept the rule names, which `LanguageProject` composes into the language's own
  constraints - so the constraint engine, the findings and the squiggles all carry it without a new path. A complaint
  states no type, so it is kept out of the registry: a language with only a complaint has `HasTypes` false and
  `HasConstraints` true, and a concept that also has a type rule keeps that type unchanged. **Delivered for the
  conditional form too**: `TypeRule.Complain` carries an optional `when` body, and `TypeRule.NotComparable` is the
  condition it states - the types of two child slots are not interchangeable. Without a condition the complaint is a
  sentence about the concept and reports unconditionally; with one it reports only where the condition holds, which is
  how "operation is not applicable to these operands" is written without inventing a second diagnostic path. The
  condition reads its types at check time from the context's own type layer, so it costs nothing until a check runs
  and follows whatever the language's rules and declared edges say; comparability is asked of the type first and of
  `TypeRegistry.Subtypes` second, because `IType.IsComparableTo` only ever consults the primitive relation. An operand
  slot that is empty makes the condition *hold* rather than fail: there is nothing to compare, and blaming the author
  for a node that is merely incomplete is the wrong finding. The condition's two slots are checked at load like a
  child body's, so a reference slot is refused where the author can see it instead of throwing during a check.
- **T8 - replacement and substitution.** `TypeRule.Replacement` and `TypeRule.Substitute`. **Delivered for
  replacement**: a replacement is a document of its own that states a claim - `<:` or `==` - between two type
  *patterns*, and the relations to prove in its place, written in exactly the vocabulary a relation is because a
  pattern is a type whose leaves may be variables. It is lowered into a `TypeReplacement`, which the registry carries
  beside its rules and its edges, and a `TypeSolver` built from the registry rewrites a claim it has a rule for
  before judging it - which is what a covariance rule needs, since "a sequence of one element type is a subtype of a
  sequence of another" is not an edge between two types but an edge between *any* two sequences. Three decisions
  shaped it. The framework's own covariance for a list and an optional is now stated as replacements too
  (`TypeReplacements.Covariance`), so what the solver knows about a list is written in the same vocabulary a language
  uses and there is one mechanism rather than two. A replacement that states no relations *discharges* its claim
  rather than being an unfinished rule - MPS' null type is the example, and the drawing says `nothing` instead of the
  placeholder that means "waiting to be filled". And a pattern may not be a join or a meet ("either of these", "both
  of these at once"): neither is one shape a claim can be about, so both are refused at load where the author is
  looking rather than owned as a rule that never fires. Two further refusals live at load: a relation naming a
  variable no pattern binds, and (through the enumeration) a claim that is not one of the two. `:>` is deliberately
  not a claim, because it is `<:` with the sides swapped and would be one claim with two spellings.
  **Not delivered: substitution** (`TypeRule.Substitute`), because configuring a type per model needs a per-model
  configuration slot the framework does not have - open question 113.

#### What stays out, and why

- **A statement list, mutation of a type node, and anti-quotation.** `%()%` exists in MPS to move AST fragments in
  and out of a statement; a rule here is an expression over slots, so there is nothing to move. Reintroducing it
  would mean shipping an interpreter - the line `docs/architecture/semantics.md` already draws for constraints,
  templates, types and intentions.
- **`overrides`, and rule-order resolution beyond concept declaration order.** Inheritance here is "own rule first,
  then the super concepts in declaration order", stated once and used by notation, triggers, scopes, constraints,
  types and intentions. A second resolution order is a second answer.
- **A checking rule as part of the type system.** It is `ICheck`, with a repair, or `IConstraint`, without - and both
  already run in the app.

#### How it is proven

The parity rule, exactly as `TypeRuleLoweringTests` proves it for the current bodies: a **corpus of cookbook
rules** written twice - once as a C# `ITypeRule` and once as a `Projectional.TypeRules` model - answered by the same
`TypeRegistry` over the same documents, compared as `IType.Name` per node. A cookbook rule with no declarative
spelling is a gap in the vocabulary, which is the same defect ADR 0014 calls it.

