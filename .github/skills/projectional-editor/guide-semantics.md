# Scopes, constraints, types, text generation and interpretation

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How scopes work

`Projectional.Semantics/Scopes/` answers "what may this reference point at", and the design is
`docs/architecture/language.md` §Scope model.

- **`IScope` is asked two questions and they are the same question**: `Entries` is "what may I
  reference here" (the completion list) and `Resolve(name)` is "what does this name mean" (a typed
  name). One implementation, two consumers - a second lookup that answered differently would let the
  list offer something the name cannot mean.
- **A level does not deduplicate.** Two entries with one name inside a level are exactly what makes a
  name ambiguous; collapsing them would turn a decision the user has to make into a silent pick.
- **`ScopeChain` does not fall through an ambiguity.** A nearer level that knows the name twice makes
  the name ambiguous; looking further for a unique answer would make a nearer declaration lose to a
  farther one, which is the opposite of what a chain is for.
- **`DefaultScopeProvider` is local, ancestors, model, project** - nearest first. Only named nodes
  contribute (no name slot or an empty name is not something a name could mean), and **the node being
  resolved is excluded everywhere**: a reference that could point at the thing holding it is a cycle
  the model already refuses. The "imported models" step of the design is not implemented - a project
  has no import list yet, so the project level stands in for it.
- **A level answers `Resolve` from `NameIndex`, never by enumerating itself.** The nearest level is "my
  own children and my siblings", which in a flat document is every node, so filtering the level made one
  reference cost the document (2 232.8 ms for 7 501 nodes; 2.3 ms with the index). `ChildrenScope` and
  `RootsScope` therefore test membership per index candidate, and `Entries` - the completion list - is
  materialised only when someone asks for it. **The index is per project, not per document**: the
  outermost level *is* the project, so a per-model index would fall back to a walk for exactly the
  cross-model case that was expensive.
- **The index is invalidated by `Model.Version`, never by a change handler.** Subscribing to
  `Model.Changed` sets `IsRecording` for every model of the project, so a consumer that wanted no undo
  history would start allocating rollback closures for it; comparing the versions of the project's models
  costs one read each and sees a rename, a move, a deletion and a new node alike. Do not "tidy" this into
  a cache inside `DefaultScopeProvider` - it is a stateless singleton shared by every session and project,
  so a cache there has nowhere to belong and no way to expire.
- **`ScopeChain` must not read a level's entries while it is being built.** It concatenates them lazily
  and drops only a level that answers eagerly and holds nothing (`Scope`), because that one is known to
  contribute nothing. `ScopeChainTests.AChainAsksALevelForItsEntriesOnlyWhenTheListIsWanted` is the test
  that keeps it that way; a "simplification" back to `entries.Count > 0` would silently restore the cost.
- **`EditorSession.Scopes` is the aspect**, defaulting to `DefaultScopeProvider.Instance`. It is a
  property rather than a constructor argument because a language package cannot reference the editing
  layer, so the host installs a replacement.
- **A language replaces a level with `ScopeProviderRegistry`**, which is itself an `IScopeProvider`: it
  maps a concept key to a provider, walks the super concepts in declaration order (the same order
  notation and triggers resolve in) and falls through to the default. A host installs one by assigning it
  to `EditorSession.Scopes` - one place decides which provider wins, not a second kind of provider.
- **A reference completion carries a *name*, not an id.** `AcceptCompletionAction` re-resolves it
  through the scope at the moment the key is pressed, so a target renamed or removed after the list
  opened is refused instead of linked, and a name that has become ambiguous is refused rather than
  picked. That is the same "re-resolve at apply time" rule a `DiagnosticFix` follows.
- **`EditContext.NodeOf` searches the document, not the model.** A reference target is often a
  *sibling root*, so resolving one through `NodeOf` returns null and the edit silently does nothing -
  use `node.Model.Project.FindNode(id)`. This cost a debugging round; do not "simplify" it back.
- **`SelectCompletionAction` takes a delta, not an index**, and `CompletionList.Move(0)` throws. A
  freshly opened list already selects index 0, so a test that wants the first entry accepts directly.
- **Typing into a reference cell resolves a name, it does not store text.** `TypeTextAction` asks the
  scope for the pending run plus this character: `Found` links the slot, `Missing` keeps the run (a name
  is spelt over several keystrokes), `Ambiguous` is `Rejected` with a reason. Nothing is written while
  the name is incomplete, so a half-typed name costs no undo step. **`Select` clears `PendingText`**, so
  a test that spells a name over several keystrokes must select once and then apply the keystrokes -
  selecting before each one wipes the run and the name never completes.

## How constraints work

`Projectional.Semantics/Constraints/` is the language's own validation, and the design is
`docs/architecture/semantics.md` §Constraints.

- **A constraint reports; it never repairs and never mutates.** The repair is a `DiagnosticFix` built by
  whoever knows how to fix the finding, which is why a language author can write a rule without knowing
  what the editor can do about it. A finding with no repair reaches the squiggles and the problems panel
  and not the quick-fix list - the right answer for "a rule needs a condition", where inventing a
  condition would be worse than saying one is missing.
- **`ConstraintContext` is deliberately small**: the node, `ScopeOf(slot)` and the collector. It cannot
  mutate, cannot reach the editor and cannot see a cell, so a rule that runs on a model nobody is drawing
  (a load, a batch check) works the same as one on the open document.
- **A concept inherits its super concepts' constraints**, in declaration order - the same rule notation,
  triggers and scope providers follow.
- **A rule about *any* node is `ConstraintSet.Everywhere`, not a concept key**, and runs before the
  concept's own. A language's concepts need not share a root, so a rule declared over one root silently
  skips every concept that does not inherit from it - that is the mistake this seam exists to prevent.
  `With(set)` composes two sets in a predictable order (a host adds `ScopeConstraints.All` to its own).
- **A constraint is not a check.** `ICheck.Find` returns `DiagnosticFix` and its contract says every
  finding carries the repair that removes it, so a rule with no repair *cannot* be a check. If it only
  reports, it is a constraint - which is why ambiguity diagnostics are one.
- **`Check(Node)` is the incremental seam**: it answers that subtree's findings alone, so a caller that
  keeps the rest merges them itself. The dependency-set machinery of the design is **not** implemented -
  a caller rechecks the subtree that changed, which is correct and cheaper than the document, but it does
  not yet know that a *referencing* node's rule may have changed too. Do not claim incrementality beyond
  that. **The numbers are measured, not assumed** (`tools/Projectional.Benchmarks`, recorded in
  `docs/architecture/semantics.md` §What a full recheck costs): a document without references checks
  linearly (6.6 ms at 40 001 nodes) and is dominated by the rebuild the same keystroke pays (123 ms, of
  which the layout is a third); a document *made of* references used to be quadratic (2 232.8 ms at 7 501
  nodes) because every reference asked the scope and the scope walked the document, and costs 3.3 ms since
  the name index. So the dependency sets are still last - do not start with them.
- **The app merges constraint findings with the check catalogue's in one list** (`PublishFindings`),
  because the panel and the markers must not be two answers to "what is wrong". A constraint finding has
  no fix, so it is a marker and a panel row only. Which rules it runs is the document's: `DocumentTab.Constraints`
  carries the language's own set, and `SampleConstraints.All` stands in where a caller passed none.
- **A rule can be written down.** `Projectional.ConstraintRules` is the constraint language: a `constraints` folder
  holds one document per rule, `ConstraintLowering` turns each into the same `IConstraint` a C# language declares,
  and `LanguageProject.Constraints` is what a host runs. The body vocabulary is closed on purpose (`EmptySlot`,
  `DefaultValue`, `SurroundingWhitespace`, `EmptyReference`) - a general expression body would be an interpreter for a
  language whose only job is to say what is wrong, the same line the type rules draw with three bodies. An empty
  concept key means "every node", drawn as the property cell's `every node` placeholder; a rule naming a concept or
  slot the language does not have (or declares as another kind), or without a message or a body, is a load error.
  Parity with the catalogue is a test rather than a claim: `ConstraintLoweringTests` declares the catalogue's rules as
  models and demands the same findings, at the same places, with the same severities.

## How types work

`Projectional.Language/Types/` computes what a node evaluates to; the design is
`docs/architecture/semantics.md` §Types.

- **`expected.Accepts(actual)` is the only question a slot asks**, and the direction matters: a caller that got it
  wrong would be comparing two types for equality, which is a different and much weaker question. It has a
  **default implementation** on `IType` answering with `SubtypeRelation.Primitives`, so a type a language adds
  needs only a `Name`; the lenient types override it. A default member is reached through `IType`, so hold the
  interface, not the concrete class.
- **Subtyping is the second question and it is strict**: `ISubtype.IsSubtype(actual, expected)` asks the declared
  edges in `SubtypeRelation` (its own type, a primitive of the same name, transitive declared edges) and `unknown`
  stands in **no** edge - an edge naming it is refused where it is declared. That separation is the point:
  `Accepts` is lenient about `Unknown` so one mistake is not fifty findings, and a type rule must never be told a
  guess satisfies `int`. Collapsing the two is how `Unknown` leaks.
- **Equations are how a rule ties two nodes' types**, and `TypeSolver` is the fixpoint over them: a bounds solver
  where an equality pins, a subtyping narrows, and a claim about a compound type is rewritten by a
  `TypeReplacement` before it is judged (a list against a list becomes a claim about their elements, an optional
  about their inners, a record never). It refuses three temptations - it does not invent the missing side of
  `?a == ?b`, it does not give a variable a type nothing demanded, and it stops on a budget instead of hanging.
  `ITypeRule.EquationsFor` states equations and defaults to none, so `TypeFor` keeps working for every rule that
  needs no solve. `TypeRegistry.EquationsFor(node)` is the consumer: it hands a node's declared relations to a
  solver, so a rule table that needs solving is solved without every consumer learning a second question.
- **A rule states relations in a model, not in C#**: a `TypeRule.Equations` body holds `TypeRule.Equation`
  children, each a `relation` (`==`, `<:`, `:>`) between two type expressions, and `TypeRule.TypeVariable` is a
  type expression the solver gives a type to. The symbols are the ones `TypeEquation.ToString()` prints, so a rule
  and the sentence a failed solve quotes read the same text. A variable is an **identity**, not a name: two nodes
  writing `?element` in one rule lower to one `TypeVariable`, because relations between unrelated unknowns say
  nothing. Refusals sit where the value is written - the relation's enumeration makes the model refuse a spelling
  the lowering never sees, and the lowering refuses an equations body that states nothing, an equation whose two
  sides are the same variable, and a variable outside a body that states equations. `TypeRule.Join`/`TypeRule.Meet`
  are type expressions too, so "either" and "both" are spellable in a model.
- **A subtyping declaration is a document, not a slot on a type**: `TypeRule.Supertypes` states one edge
  (`subtype <: supertype`) and lowers into a `SubtypeRelation` the `TypeRegistry` carries beside its rules, so the
  solver and the lattice a consumer builds are given the language's own edges rather than only the framework's
  primitives. The relation compares a named type by **identity** - "are two declarations of one name one type?" is
  the language's question - so the language answers it by handing out one instance per name: a rule writing
  `Ref Amount` and an edge naming `Amount` receive the same type, which is what makes the edge join anything.
- **A replacement states what a claim about two types really means.** `TypeRule.Replacement` is a document of its
  own: a claim (`<:` or `==`) between two type **patterns**, and the relations to prove instead. A pattern is a type
  whose leaves may be `TypeVariable`s, so there is no second syntax for "a type with a hole in it", and one variable
  table is shared by both patterns and every relation - which is what makes a relation a statement about what the
  patterns bound. It lowers into a `TypeReplacement` the registry carries beside its rules and its edges. Three
  things to keep true: the framework's own covariance for a list and an optional is **stated as replacements too**
  (`TypeReplacements.Covariance`), so there is one mechanism and not two - never re-add a `case (ListType, ListType)`
  to the solver; a replacement that states **no** relations *discharges* its claim rather than being unfinished, drawn
  as `nothing` and not as the fill-me-in placeholder; and a pattern may not be a join or a meet, because "either of
  these" is not one shape a claim can be about. Refused at load: a join or meet pattern, a relation naming a variable
  no pattern binds, and (through the enumeration) a claim that is not `<:` or `==` - `:>` is `<:` with the sides
  swapped, so it is not a second claim.
- **A comparison is a relation apart from the edges, on purpose.** `TypeRule.Comparable` states that two types may
  each be used for the other, and lowers into a `ComparisonRelation` the registry carries beside `SubtypeRelation`.
  It is not written as two `<:` edges: those say the same about interchange while also putting each type in the
  other's supertype set, so `TypeLattice` would reduce a join of the two to one of them and a walk up from either
  would cycle. So a language can state an interchange without ordering the two types. The relation is symmetric
  (one declaration, one fact) and transitive (a chain relates its ends), because that is exactly the answer the
  both-ways subtype reading gives - two spellings of one fact must not answer differently. It refuses `unknown`
  where a pair is declared, for the reason the edges do. Two consumers ask it: the solver's fact check, and the
  `TypeRule.NotComparable` condition of a complaint.
- **Join and meet are questions about the relation, not new kinds of type**: `JoinType`/`MeetType` are what a slot
  or a rule *writes* ("either", "both") - `TypeRule.Join`/`TypeRule.Meet` in a model - and `TypeLattice` reduces
  them from the declared edges - one side below the
  other, or the unique least/greatest common type. When the edges do not determine one the answer is `UnknownType`,
  never a pick between two incomparable candidates, which is why the relation had to learn to enumerate
  (`SupertypesOf`/`SubtypesOf`) before the reduce could be honest.
- **A type asks three questions, not two**: `Accepts` is conformance ("may this value stand here", lenient about
  `unknown`), `ISubtype.IsSubtype` is subtyping ("is this below that", strict, one direction), and
  `IType.IsComparableTo` is comparison ("may each be used for the other", asked through the relation **both ways**).
  A one-way edge is a subtype without being a comparison - that is the difference the third question exists for, and
  it is why `TypeSolver` asks `IsComparableTo` instead of writing two subtype checks. Each type whose acceptance is
  structural overrides it so its comparison is structural too (list elements, record fields, a name's expansion, a
  join's either side, a meet's both). `SubtypeRelation.IsComparableTo` is the authority for a language's edges and
  `ComparisonRelation` for its declared pairs; `IType.IsComparableTo`'s default asks
  `SubtypeRelation.Primitives`, so a bare type compares only with itself.
- **A type read cannot cycle, so there is no settle pass.** A rule is a pure function of the node and reads its
  children, so a type read descends a tree and terminates; the solver's fixpoint is where ordering lives. A cycle
  can only come from a name declared in terms of itself, refused at load with the chain printed. A child body may
  name only a child slot: `TypeRuleLowering.CreateRegistry` asks the concept registry when it is handed one and
  refuses a child body naming a reference slot or an undeclared slot - without that check the type read throws
  where the node has no such child slot.
  A comparison a language states as data is no longer waiting for a consumer: the solver's fact check and the
  `TypeRule.NotComparable` condition both ask one.
- **The same question has two authoring paths, and they must answer alike.** "Are these two operands usable one
  for the other" is `TypeRule.NotComparable` in a types document and
  `SampleConstraints.OperandsNotComparable` in C# - the expression language's own rule, which is what makes
  `1 + "x"` a finding instead of a silent `int`. Both ask the type first and then `Subtypes` and `Comparisons`, in
  that order: **if you change one, change the other**, or the two paths start answering differently. The C# one
  lives in `Projectional.Samples` because `Projectional.Expressions` deliberately does not reference the semantics
  layer, and keying a constraint by concept is what lets one set hold two languages' rules.
- **A type rule may report instead of typing.** `TypeRule.Complain` is a body carrying a `message`
  and a `severity`; `TypeRuleLowering.CreateComplaints` lowers it into an `IConstraint` keyed by the
  concept the rule names, and `LanguageProject` composes it into the language's own constraints - so
  the existing engine, findings and squiggles carry it with no new path. A complaint states no type,
  so it stays out of the registry: a language with only a complaint has `HasTypes` false and
  `HasConstraints` true, and a concept that also has a type rule keeps that type. **An optional `when`
  body gates the report.** Without one the message is a sentence about the concept and reports wherever
  the concept appears; with one it reports only where the condition holds. `TypeRule.NotComparable` is
  the one condition kind there is - the types of two child slots are not interchangeable - and it is how
  "operation is not applicable to these operands" is written without a second diagnostic path. Three
  rules make it behave: the types are read at check time from `ConstraintContext`, so a condition costs
  nothing until a check runs; comparability is asked of `IType.IsComparableTo` **and** of
  `TypeRegistry.Subtypes`, because the interface's default only consults the primitive relation and a
  language that declares two names interchangeable has answered a question the types alone cannot; and an
  empty operand slot makes the condition *hold*, because there is nothing to compare and blaming a merely
  incomplete node is the wrong finding. The condition's slots are validated at load like a child body's,
  so a reference slot is refused where the author is looking rather than throwing during a check.
- **`UnknownType` accepts and is accepted**, which is what suppresses follow-on errors - one mistake in
  a condition must not produce a finding on every expression that reads it. It is **not** a wildcard to
  rely on: a caller that must not proceed on a guess (a generator) asks `type is UnknownType` explicitly.
  That is the "cannot leak into generation unnoticed" rule - the type does not stop the caller, it makes
  the caller say what it does about not knowing.
- **A concept with no rule is `Unknown`**, not an error. Nobody said, so nothing is known.
- **A record is compared by name, not by order**; the order is kept for display only. **An optional
  accepts its inner type, and a plain type does not accept an optional** - "may be absent" is a promise
  about what a reader has to handle, so a slot that promised a value must not be handed one that might
  not be there. Nesting is not collapsed: `int??` says the absence may itself be absent. **Every
  structural type accepts `Unknown`**, for the same reason `PrimitiveType` does.
- **`ITypeRule.TypeFor`, not `TypeOf`**: CA1716 forbids a reserved word as an interface member, the same
  reason `ICheck.Find` is not called `Of`. The class member `TypeContext.TypeOf(Node)` keeps the natural
  name because the rule does not apply to it.
- **`TypeContext.TypeOfChild(slot)` answers `Unknown` for an empty slot**, not an error: a half-written
  expression is the ordinary state of a document being edited.
- **A rule is inherited by the concepts below it**, the same lookup notation, triggers, scope providers
  and constraints use.
- **`ConstraintContext` carries the `TypeRegistry` beside the `IScopeProvider`**, and `ConstraintEngine`
  takes both - a rule that needs a type and a rule that needs a scope are the same kind of rule.
- **Type rules live *in* the language package, beside the notation and the typing rules** -
  `ExpressionsLanguage.Types` is part of the language. That is why the type model sits in
  `Projectional.Language` and not in `Projectional.Semantics`: the layer diagram puts `Semantics`
  above `Language`, so a language package could not declare a rule from there. The line is what the
  aspect *does*: a type rule only answers a question, so it belongs to the language like notation
  does; intentions and checks produce edits, so they must live beside it (their packages reference
  `Editing`). Do not move the types back into `Semantics` - it looked tidier and it is wrong.
- **`Projectional.TypeRules` is the declared form** - the first declared aspect: a rule is a model
  (`TypeRule.Rule` with a `Fixed`/`Child`/`Switch` body) and `TypeRuleLowering` turns it into the
  same `ITypeRule` a C# author writes. **Five bodies, not a statement list**: MPS' typesystem is
  imperative, and copying that would mean shipping an interpreter for a language whose only job is to
  answer one question per node. A body that cannot be lowered (no body, an unknown type name, a value
  declared twice, a switch with no `otherwise`) is a `ConceptDefinitionException` at load, not a rule
  that silently answers `Unknown`. `TypeRule.Primitive` names only the four framework primitives - an
  enumeration would have to be resolved against the language's own registry, which is the declared
  stage's job; a structural type is written with `Record`/`List`/`Optional` instead of being named. The
  parity rule applies: `TypeRuleLoweringTests` asserts the declared registry and
  `ExpressionsLanguage.Types` answer the same type for every node of the sample document.
- **A type expression is a family of its own, not a body.** `TypeExpr` (abstract) + `Primitive` /
  `Record` / `Field` / `List` / `Optional` / `Ref` / `Join` / `Meet` / `TypeVariable`, and `Fixed` holds one
  instead of a type name. The split is
  load-bearing: a body may read the node (`type of left`), a type may not - a record field's type cannot
  depend on the node that holds the rule, because a field is part of a *type* and there is no node there
  to ask. Nested arbitrarily; **the drawing of a complete type expression is exactly `IType.Name`**, so
  the rule an author reads and the sentence a finding quotes are the same text
  (`ATypeExpressionDrawsExactlyWhatTheTypeIsCalled`). The one exception is an empty record, which draws
  the shared `<< ... >>` placeholder because an empty slot must stay fillable.
- **A record needs an inline list, which is why `InlineCollectionNotationCell` exists.** `Collection`
  and `Children` are *block* cells: they start their own line, so `(amount: int, currency: string)`
  written inside a `Line` used to be inexpressible - the declared language was weaker than C# for a
  purely notational reason. Both list cells now build the same runtime `CollectionCell` (which arranges
  inline either way); the difference is only whether the list may stand inside a line.
- **A declared name is transparent, and the *accepter* unwraps it.** `NamedType(name, expansion)` keeps the
  name for the message and the expansion for the meaning; every `IType.Accepts` starts with
  `NamedType.Unwrap(actual)`, because a primitive that did not look through would refuse an `Amount` that
  *is* an `int`. `NamedType.Unwrap` is also what a generator calls before deciding what kind of type it has
  (the same rule as `type is UnknownType`). A named type is a **root of the types model** (not a second
  list inside the rules document), `TypeRule.Ref` names it by a string the lowering resolves like
  `conceptKey`, and undeclared/duplicate/self-referential names are load-time errors - the last one needs
  the chain of names being expanded threaded through `LowerType`, which is exactly the bug a fresh set per
  call would reintroduce (infinite recursion, a StackOverflow crash rather than a failing test).

### The framework ships one: ambiguity

`ScopeConstraints.All` is `AmbiguousReferenceConstraint` declared for every node. It closes the scope
model: a typed name and a completion pick both refuse an ambiguous name, so a document cannot be *made*
ambiguous by typing - but renaming one element can make an existing reference share its name with
another, and that reference keeps drawing as resolved because it points at a node id.

Two decisions inside it, both load-bearing:

- **It resolves the target's *current* name, not the stored `resolveInfo`.** A rename does not rewrite
  the stored text (`SetReference` is its only writer), so resolving `resolveInfo` reports names no user
  ever sees - a false positive on every renamed target. `resolveInfo` is the fallback for *re-linking*,
  not a description of now.
- **It stays quiet on an unresolved reference.** That one is already visibly wrong (`<unresolved>`) and
  the text it fell back to may be stale, so reporting ambiguity there would blame the wrong problem.

## How text generation works

A language turns a model into text through a template per concept. The template is **data** in
`Projectional.Language/Generation/` (a language package declares one without referencing the layer that walks it);
the walk is `Generator` in `Projectional.Semantics/Generation/`. `docs/architecture/semantics.md` §Generation is the
design of record.

- **Twelve parts, and the set is closed.** `TextPart`, `PropertyPart`, `ParentPart`, `ReferencePart`, `ChildPart`,
  `ChildrenPart`,
  `LoopPart`, `IndentPart`, `LineBreakPart`, `ConditionalPart`, `WhenFilledPart`, `SelfPart`. There is deliberately
  no "compute", no
  variable and no counter: a part emits, reads a slot, expands a child or chooses, and anything else is a generator
  this framework does not have. `SelfPart` exists for one reason - inside a `LoopPart` body "this node" is the child
  being visited.
- **`ParentPart` is the part that reads the node above.** `PropertyPart` reads this node's property; this one reads a
  property of the node that holds it, which is how a fragment names what it belongs to (a key its table). The slot is
  named like every other part - `ParentPart("name")` is the name - the outermost node emits nothing rather than
  failing, and the emitted region belongs to the *holding* node, so "go to source" lands where the characters came
  from. It is the one part a load cannot validate: the holding concept is not declared anywhere, so the engine checks
  it and names both concepts. Declared twin: `TextGen.Parent`, drawn `^slot`.
- **`WhenFilledPart` is the part that asks a slot, not a value.** `ConditionalPart` compares a property with a text;
  this one asks whether the slot holds anything, and it accepts all four kinds - so brackets can close around a list
  only when it has an entry, a missing value can become a stand-in, and a tree can expand a node's children only once
  they exist. It is a separate part because the two are validated differently (a property, versus a slot of any kind),
  and a defaulted property counts as filled, exactly as it draws and as `PropertyPart` emits it. The declared twin is
  `TextGen.WhenFilled` (`conceptKey` = the slot, `body`, `otherwise`).
- **`ReferencePart` reads the model's three reference states, not a cell's.** A target's name, the `unsetText` the
  language names, the `unresolvedText` for a dangling target - so a generated file and the editor cannot disagree
  about what a reference says. It arrived with the SQL schema example, whose DDL is unreadable without the name of the
  column a key points at; before it, no language could generate a reference at all. A target that declares no name is
  emitted as `<Concept.Key>`, exactly as a reference cell draws it.
- **`TextTemplate.Of(...)` takes `params ReadOnlySpan<TemplatePart>`**, so a C# generator passes its parts without
  allocating an array.
- **A concept with no template is a failure; an empty template means "emits nothing".** Never collapse the two: the
  first is "nobody said", the second is a decision, and a silent hole in the output is the worst of both.
- **An entry that emits nothing takes no separator with it.** `ChildrenPart` and `LoopPart` write each entry into its
  own buffer (`Generator.EmitEntries`) and skip it when the buffer stays empty, because a separator already written
  cannot be taken back. Without this, "ignored" would mean "nothing" at a single-child slot and "a stray comma" in a
  list - a syntax error dressed up as a feature. Regions written into a buffer are shifted when the buffer is appended,
  so "go to source" still lands on the right character.
- **`TextGen.Ignore` is the declared way to say it** (a mapping body beside `TextGen.Template`, both conforming to the
  abstract `TextGen.Body` the mapping's `template` slot holds - the slot name is persisted, so it keeps its name). A
  concept rather than an empty template node because an empty box reads as unfinished work.
- **`GenerationCoverage.Of(model, generator)` is the editor's hint**: one diagnostic per concept the chosen generator
  cannot print, at its first node in document order, carrying no repair (writing a template is the author's decision).
  The run still stops with a `GenerationFailure` - a report is what lets a user find out before pressing Generate.
  **The shell shows it**: the `Of(model, IEnumerable<TextGenerator>)` overload is asked once per generator the document
  offers, and the findings go into `DocumentFindings.Of(..., reports:)` - so coverage reaches the squiggles and the
  problems panel like any other finding, as a marker and a row and never as a repair.
- **Indentation is the template's, never counted from the text.** `LineBreakPart` writes the newline *and* the current
  indentation; `IndentPart` raises the level for the lines inside it. A separator is a `TextTemplate` too, which is why
  a list on separate lines is `ChildrenPart(slot, Template(LineBreakPart))` inside an `IndentPart` - an "indent" flag
  on the list would have made the drawing depend on what the child happened to emit.
- **Every emitted stretch is a `GeneratedRegion(Node, Slot, Start, Length)`.** The map is the half that is impossible
  to retrofit, and it costs one list while the text is built. Edits get no region (they have no node); a literal gets
  one with `Slot` null, because "go to source" on a keyword must find the node that wrote it. `GeneratedText.RegionAt`
  is a first-match scan - do not build a per-character index for a lookup nobody asked for.
- **A template that expands itself is stopped by `MaxDepth`, not by the stack.** A `SelfPart` outside a loop body
  recurses for ever; a stack overflow cannot be reported to a user at all.
- **The engine recognises only the parts it knows and fails on any other**, exactly like `CellLayouter` with cells: the
  set is closed, so a new part is added on purpose rather than silently ignored.
- **Generation is pure.** No file system, no clock, no culture, no model place - two runs produce the same bytes, which
  is what a host's build and the golden tests both stand on. **The framework does not write files**: it returns text and
  the host decides where it goes (open question 12), so writing is the shell's job and a language component can never
  guess a path or an encoding.

**A language has as many generators as it declares.** `TextGenerator` is a *name* plus a `TemplateRegistry` - not an
aspect on the concept - so the host picks one, the way it picks a notation variant, and nothing about the model
changes. `RulesTextGenerators` in `Projectional.Samples` ships two (JSON and a C# fragment) precisely so that is the
proven case rather than a claim in a comment. The shell builds its Generate submenu from the active document's
generators; there is no fixed "the generator" command, and a document whose language declares none simply has no
entries.

- **The declared form is `Projectional.TextGen`**, and it is a `generator` folder in a language project beside
  the `structure`, `editor` and `types` folders, one generator per document. A generator holds one mapping
  per concept; a mapping holds a template; a template is a list of parts.
- **`TextGenLowering` is the only lowering, and it validates at load**: unique generator names, a concept the language
  actually has, and every part's slot existing on the mapped concept *as the kind it is* (a `Child` part on a list slot
  is a load error). That is the notation registry's rule applied to templates - the earliest moment the author is
  looking at the file.
- **ADR 0014's parity rule is executed by `TextGenLoweringTests`**: a generator written as a model must produce exactly
  the text of its hand-written twin, compared as text because that is what a consumer sees. A declared construct that
  could not be lowered would be a defect in the language, not in the test.
- **A binary expression is always parenthesised by the pilot's generator.** A template cannot compute precedence - no
  part asks how an operand binds - so omitting the parentheses would be right only for the tree it saw. The pretty
  version belongs to the C# base language's generator (`docs/architecture/csharp.md`).
- **Strings are emitted verbatim, and there is deliberately no "as JSON" part.** A template that put a value inside
  quotes does not escape it. Spelling a format belongs to the language that owns the format, in its own generator, so
  converting a model into JSON, XML, TOML, HOCON or YAML means producing a model of *that* language and letting its generator
  print it - never a per-value escaping part, which would be a second answer to "how is this value written" in every
  language that prints, and would escape before the target is even chosen. Design:
  `docs/architecture/import.md` §Converting a model into one of these languages.

**The output panel is shared with the interpreter**, and that is deliberate: a run report and a generated file are both
"what this document produced", so one panel and one row type serve both. A generation shows one row per line and each
row points at the node that wrote the beginning of that line, which turns the traceability map into a gesture - clicking
a line of the generated file moves the caret to what produced it. A run's rows are its trace. An edit drops both, because
neither can be derived again from the new model.

## How interpretation works

A language says what its nodes *mean* through one function per concept, and the framework runs it. The design is
`docs/architecture/semantics.md` §Interpretation; the invariants that are expensive to retrofit are these.

- **The aspect is in `Projectional.Language/Evaluation/`, the runtime in `Projectional.Semantics/Interpretation/`.**
  Same split as the type rules: a language package must declare meanings without referencing the layer that
  consumes them, so `IEvaluationRule`, `EvaluationRegistry`, `EvaluationContext`, `Value` and its subclasses live
  beside the notation; `Interpreter`, `InterpretationResult`, `TraceEntry` and `InterpretationFailure` live above
  it. `DelegateEvaluationRule` exists because most meanings are one expression long and C# has no lambda-to-
  interface conversion - a language writes `.Add(Key, new DelegateEvaluationRule(ctx => ...))`.
- **A concept with no rule has no value.** Unlike `IType`, there is no `UnknownValue`: `Interpreter` fails with
  "declares no evaluation rule". Do not invent a fallback value - a half-written document must stop the run
  where it stopped being meaningful, not hand out a guess that a generator would then trust.
- **`EvaluationContext` is read-only and fails through `Error(message)`.** It offers `Eval(node)`,
  `EvalChild(slot)` (exactly one child, so an empty slot is a failure naming the slot), `Variable(name)`,
  `PropertyValue(slot)` and `Error(message)`. `PropertyValue` maps through the property's declared
  `PropertyType.TryParse` - the same authority the model validates writes with - so a literal cannot mean one
  thing in the interpreter and another in the document. Never add a mutation method here.
- **The interpreter owns the budget, the stack and the trace; a rule owns only its node.** Every `Eval` is one
  step, checked against `DefaultBudget` (10 000) and the caller's `CancellationToken` *between* nodes; the trace
  is post-order (a child's entry precedes the line of the node that asked for it), which is the order a reader
  follows the answer back. **Nothing is cached**: asking for a child twice costs two steps and appears twice.
  A failure records the *innermost* node once - the first `catch` wins, and ancestors add no line of their own.
- **A failure is a result, not an exception escaping the host.** `EvaluationException` is what a rule throws;
  `Run` catches it and answers `InterpretationResult` with an `InterpretationFailure` that carries the `NodeId`
  (not the node: the result outlives the call). Budget and cancellation are failures with their own `Kind` and
  are raised through the internal `EvaluationStoppedException` sentinel, so a rule's own `catch` cannot mistake
  them for model problems.
- **A run resets the previous one.** `Run` clears trace, steps and variables, so one `Interpreter` serves a
  session that runs the same table against different inputs. Creating a new interpreter per run is also fine.
- **A ruleset is a decision list.** `RulesEvaluation` makes a rule the truth of its condition and a ruleset the
  outcome of the **first** rule that applies; no match is a failure, because inventing `Review` would hide a
  table whose conditions leave a gap. Its `Registry` composes `ExpressionsEvaluation.Registry`, so a condition
  runs without a host bolting two registries together.
- **Values are content-compared records** (`IntValue`, `FloatValue`, `BoolValue`, `StringValue`, `DateValue`), with
  `Display` for the trace. A trace is read by a user, so `Display` is a rendering and never an identity. A record or
  list value arrives with the first language that evaluates one - do not add kinds speculatively. A value kind exists
  because a property type does: adding one to `PropertyType` means adding the matching `Value` and the case in
  `EvaluationContext.PropertyValue`, or a generator over that slot reports it "has no value here".
- **Tests pass the test token.** Any call to `Interpreter.Run` in a test passes
  `cancellationToken: TestContext.Current.CancellationToken`, or xUnit1051 is an error.

**Running a document is a language capability, not a command.** A registry *is* the interpreter, so "can this
document be run" is answered by the language it is written in - never by which menu opened it, and never by
falling back to another language's rules.

- **`WorkbenchLanguage.InterpreterFor(model)` is the one answer**, derived from the document's root concept: the
  shell ships an interpreter for the sample rules language and for nothing else, so a structure definition, a
  notation, a type ruleset and a document of a language opened from a folder all answer `null`. Running a
  structure model through the expression rules would report a missing rule for every concept, which is a wrong
  answer to a question nobody should have been able to ask.
- **`DocumentTab.Evaluation` carries it and defaults to `null`** - a caller that forgets the aspect gets a
  document that cannot be run, never one that silently runs with the sample's semantics.
- **The shell refuses rather than runs**: the Run entry is disabled, the panel's tooltip and the status bar say
  why, and `RunActiveDocument` returns before asking the interpreter.
- **The app's part is small on purpose**: `DocumentRun.Of` (document + registry + the typed variables) answers a
  `RunReport`; `RunInputs` hands the line to `Projectional.Language`'s `VariableInputs`, which reads one line of
  `name=value` pairs (number, then flag, then `yyyy-MM-dd`, else text; a malformed or repeated name refuses the
  whole line instead of half-applying it) - it lives in the language layer because a declared test's
  `Testing.Evaluates` assertion reads the same pairs; a row is `RunRow(Text, Place)`, so clicking a trace line
  moves the caret to the node that produced it.
- **A run is a snapshot**: an edit drops the report, exactly as an edit replaces the findings, because a headline
  about a document that no longer exists is worse than none - and a run cannot be repeated on the user's behalf,
  since the variables came from them.
- **Trace rows are one line and must be styled that way.** Fluent's `ListBoxItem` is 32 px tall; five steps then
  overflow the panel and the answer's own last line is cut in half. The output list sets `MinHeight` 18 and
  `Padding` 4,1 for that reason - a picture of the panel, not a passing test, is what showed it.
- **F5 is the shell's, through the window's tunnelling handler**, and the gesture is written into the menu header
  rather than declared as a `HotKey`: one mechanism, not two that could both fire.

