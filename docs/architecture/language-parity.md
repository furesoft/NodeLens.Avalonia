# Language parity: the rule as three tests

[ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) says the two authoring paths stay at the same
standing. A rule nobody executes is a wish, so this is its executable form: one language, written twice, compared
three ways.

Status: **delivered** as slice C2 of [language-authoring.md](language-authoring.md), over a declared twin of
`TestLanguage`.

## What is compared

| Test | Compares | Fails when |
| --- | --- | --- |
| Registry dump | `ParityHarness.Dump(ConceptRegistry)` for both | a concept key, slot name, type, cardinality and its minimum, default, description, super concept, abstractness, completion flag, name slot or collapse slot differs - **or the order differs** |
| Snapshot | `LayoutSnapshot.Render` of one document through both notation registries | a declared notation draws anything differently |
| Findings | `ParityHarness.Findings(CheckSet, Model)` over two documents built the same way | a check reports a different code, place, role, index or range |

**Order is part of the contract**, which is why the dump is a sequence and not a set: declaration order decides
what an empty slot is filled with and which completion entry comes first, so a set comparison would pass while
the two languages behaved differently.

**Findings are compared by place, not by identity.** The two documents have different node ids, and the promise a
finding makes is about *where* it is - so the harness records the target's index in document order. The message is
not compared at all: two languages may word the same defect differently, and what must match is what was found.

**The constraint language executes the same rule against the catalogue.** `ConstraintLoweringTests` writes the rules
`Projectional.Checks` implements - `STD001`, `STD003` and `STD010` - as models, lowers them with the shipping code
and demands the same findings, compared by place for the same reason the harness does it: a rule the catalogue can
state and a declaration cannot is a parity defect, not a missing row in the table above
([semantics.md](semantics.md#language-of-constraints)).

## What the harness needed

`ParityHarness` (test-side) is three text-producing methods. Producing *text* rather than booleans is deliberate:
a failure then reads as a diff naming the concept and slot that drifted, instead of "expected true, got false".

## The two gaps it found immediately

The twin could not be written at first, and both reasons were real parity defects rather than test noise:

1. **`collapseSlot` was not expressible as data.** `TestLanguage.Rule` collapses on `enabled`, and the structure
   language had no way to say so - a declared language would have been unable to describe a language C# can. It is
   a property of `Structure.Concept` now.
2. **A collection's separator was not expressible as data.** `TestNotations` draws `notes` with `Punct(";")` between
   the entries, and the editor language had `Children` but no `Collection`. It has one now, with the separator as a
   single inline child.

That is the harness earning its keep on day one: it is a test whose *failure to compile* found two missing
features, which is exactly what a parity rule is for.

## What is still asymmetric, on purpose

| Not expressible as data yet | Why it is acceptable now |
| --- | --- |
| concept triggers | they are behaviour-adjacent and land with the aspect models of P3 |
| `Applicability` (conditional concepts) | same: an aspect, and M6 owns aspects |
| `isOfferedInCompletion`, `isAbstract` | expressible - the twin uses neither, and the dump covers both flags |
| notation variants beyond `variant` | the property exists; nothing needs a second drawing yet |

Each row is a capability C# has and data does not, so each is a violation of ADR 0014 rule 2 the day a declared
language needs it. They are listed here rather than discovered later.

## Testing this

The three tests *are* the tests. `DeclaredTestLanguage` builds its structure and editor models with the builders a
language author would use and lowers them with the shipping code, so what is compared is what a user would load.
