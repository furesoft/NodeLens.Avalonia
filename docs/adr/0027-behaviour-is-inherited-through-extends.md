# ADR 0027 — Behaviour is inherited through `extends`, and only through it

- Status: Accepted
- Date: 2026-11-21
- Related: [ADR 0026](0026-a-dependency-resolves-to-the-vocabulary.md),
  [language-projects.md](../architecture/language-projects.md#resolving-a-dependency),
  [language.md](../architecture/language.md)

## Context

[ADR 0026](0026-a-dependency-resolves-to-the-vocabulary.md) resolved both of a root's lists into the dependent
language's concept registry and deliberately stopped there: a base's constraints, scopes, types, typing rules and
intentions did not run in a dependent's document. It named the follow-up and the question that blocks it - *whose
invariants does a shared concept carry?*

Meanwhile the two lists say different things and nothing used the difference. A language that **depends on** another
says "I use its vocabulary". A language that **extends** another says "I am that language, plus more" - the reading
the language root's own documentation already gives it. A dependency that lends a concept without lending the rules
about that concept leaves the concept unguarded in the dependent's documents, which is the state nobody wants for an
extension.

## Decision

**An extended language's behaviour is inherited; a depended-on language's is not.**

1. **`extends` merges the behavioural aspects** of the resolved chain: the type rules and their relations, the
   constraints, the scope providers, the typing rules and the intentions. **`depends on` still resolves the
   vocabulary only** - structure, notation and accessory models - exactly as
   [ADR 0026](0026-a-dependency-resolves-to-the-vocabulary.md) decided.

2. **Precedence is own first, then the nearest extended language.** The extending language is the one whose author is
   looking at the document, so its statement is the one that holds. Behind it come its bases, nearest first, then
   theirs. For an additive set (constraints) that is an order the findings follow; for a keyed set (a type rule, a
   scope provider) it is an override - the nearer statement for a concept replaces the farther one, because one
   concept has one type rule and one scope provider.

3. **A chain is followed through `extends` only.** `A extends B extends C` gives `A` the behaviour of `B` and `C`.
   `A depends on D` where `D extends E` gives `A` `D`'s and `E`'s vocabulary but neither one's behaviour: the
   dependency is a use, not an adoption.

4. **What a language *produces* still does not cross.** Text generators and importers stay their own; behaviour is
   about reading, drawing and checking a node, which is what an extension inherits.

5. **What a language *declares* stays its own where a reader would expect the declaration.** `Declarations`,
   `Notations` and `DeclaredTypingRules` are not changed by this decision. The consequence is stated rather than
   hidden: the C# export writes what the folder declares, so an exported package does not carry the behaviour it
   inherited - it never carried the behaviour it declared either, which the README it generates says.

## Consequences

- **A shared concept keeps its rules.** A document of an extending language reports the base's findings and answers
  its base's typing claims, which is what makes "extends" mean something.
- **The shell needs no new wiring.** A document of an opened language is already given `module.Types`,
  `module.Constraints`, `TypingFor(module)` and `module.Intentions`; that these are now merged sets is the project's
  business, not the window's.
- **Counts become inherited counts.** Module settings' "Type rules", "Constraints", "Scope rules" and "Intentions"
  say how much is in force, which is what an author wants to know. What the folder itself declares stays visible
  through the explorer's own documents.
- **A keyed aspect has no "both".** Two type rules for one concept cannot coexist, so an extension replaces its
  base's rather than adding a second. A language that wants to add to a base's validation uses constraints, which are
  additive by design.

## Rejected alternatives

- **Inheriting through both lists.** It would make the two lists synonyms and let a mere use adopt another
  language's opinions about concepts the using language did not define.
- **Refusing an override of a keyed aspect.** It would forbid the ordinary act of a language refining its base's
  rule, and it would leave the base's statement in force where the author just said otherwise.
- **Merging only constraints, which are additive.** It would be the easy half: typing and scope are the aspects a
  dependent document actually consults while the user edits.
