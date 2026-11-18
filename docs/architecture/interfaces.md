# Interfaces: the framework's structural vocabulary

Status: **planned** (M5, first slice in [roadmap.md](../roadmap.md)). Slices **I1 and I2 are delivered**: the
concept kind, the member merge, the validation table, `Framework.Named` in every registry, `Node.IsNamed`, and the
declared path (`implements`, the `interface` header word, the lowering, `implements:` in the notation and
`interfaces=` in the parity dump). What is left is adoption by the shipped languages (I3) and the records (I4).

## The problem

Every language with named elements writes the same declaration:

```csharp
new ConceptDeclaration(
    RulesConceptKeys.Rule,
    [new PropertySlot("name", PropertyType.String, "unnamed"), ...],
    nameSlot: "name")
```

The framework already treats the repetition as one fact. `ConceptRegistry.Build` lets a concept inherit a name
slot from its first super concept, and its comment says it outright: *"A concept that is a `Named` is also named
the same way"*. There is no `Named` to point at, so three things stay unsaid:

- **Which part is the role and which is local choice.** Three languages declare `name` with three defaults and
  three descriptions, and nothing separates "references display this" from "this language's placeholder word is
  `unnamed`".
- **A slot cannot be declared over the role.** `DefaultScopeProvider` finds named nodes by walking
  `Concept.NameSlot`; a language that wants "any named node may be referenced here" cannot say so, so it
  re-implements the rule in C# - a scope provider, a check, a filter - while the declaration language has no way
  to express it.
- **The declared path has nowhere to put it in the header.** A structure document writes
  `<<nameSlot : name>>` in the body and must remember to declare the property too, while the one place MPS says
  it - `implements INamedConcept` in the header - is the place this framework cannot write.

## What an interface is

An interface is a **concept kind**, not a second registry and not a second conformance relation. It is declared
with the same `ConceptDeclaration` (`isInterface: true`), it is abstract by definition, and no node is ever an
instance of one.

| | A concept | An interface |
| --- | --- | --- |
| Instances | yes, when concrete | never |
| Properties and references | its own | yes - and they are **provided** to implementers |
| Child slots | yes | no, in the first slice |
| Triggers | yes | no |
| `Applicability` | any | `Anywhere` only |
| Supers | concepts and interfaces | interfaces only |
| Notation | required when concrete | never - nothing is projected through it |
| Storage and conformance | one flat slot map, `IsKindOf` | unchanged |

Three rules keep this from becoming a second mechanism:

- **Conformance stays `IsKindOf`.** A slot declared over an interface accepts every implementer and every
  descendant of one, exactly as a slot declared over an abstract concept does. The model, projection, editing,
  persistence and checks learn nothing new.
- **Storage stays one flat slot map.** An interface's members are merged by the same walk that merges a super
  concept's, so `node.GetOrNull("name")` works and no layer asks where a slot came from.
- **Aspect lookup stays the super-concept walk.** An interface that designates a name slot hands it down, like
  any super; an interface with no aspect is simply skipped by the lookup that already walks supers in
  declaration order.

**An interface may not declare a child slot, and that is a decision.** A child slot is a statement about the
node's *shape*: merging one into an unrelated implementer makes every implementer's notation owe a cell it never
asked for, and a registered concrete concept without a notation draws `MissingNotationCell`, the one state a user
cannot work around. A reference slot says "this points at a thing of that role", costs nothing in the drawing,
and is what interfaces are for. A language that needs composition declares the child slot on a shared abstract
concept instead.

One consequence to know before designing around it: **a child slot declared over an interface can never be
filled by Enter or completion**, because `ConceptRegistry.DefaultInstantiableFor` only answers concrete
concepts. Interfaces are role types for references and for slots that accept what already exists - not
factories.

A language declares its own interfaces the same way, and should: `Framework.Named` is the framework's one
piece of vocabulary, not a closed set. A rules language that wants "anything a rule may point at" declares
`Rules.Target` and implements it on the three concepts that qualify - the mechanism is the same, and the
framework only owns the role it has to own, because every language has it.

## The first interface: `Framework.Named`

```csharp
public static class FrameworkInterfaces
{
    /// <summary>The key every language names when it wants the framework's name property.</summary>
    public const string Named = "Framework.Named";

    public static ConceptDeclaration NamedDeclaration { get; } = new(
        Named,
        [new PropertySlot("name", PropertyType.String, description: "What references to this node display.")],
        nameSlot: "name",
        isAbstract: true,
        isInterface: true,
        description: "Something a reader can refer to by name.");
}
```

A concept that says `interfaces: [FrameworkInterfaces.Named]` gets, without declaring any of it:

| What | Why it is the interface's and not the concept's |
| --- | --- |
| a `name : string` property | it is what the role *is*; a concept that does not want to be called "name" keeps a designation instead |
| the name-slot designation | `Node.Name`, a reference's displayed text (`ReferenceCell`, `CollapsedSummary`), `resolveInfo` on `SetReference`, the scope (`DefaultScopeProvider`), the ambiguity constraint, `STD001`/`STD003`, `ChildOrder` (sort by name), `Outline` and `ProblemList` all read it |
| a stable answer to "is this node named" | `Node.IsNamed` is `Concept.NameSlot is not null`, which an interface makes a fact of the type rather than a habit |

The name slot's behaviour does not change - the interface supplies the same designation a language used to
write by hand. What changes is that "named" becomes a thing a slot can be declared over:

```csharp
new ReferenceSlot("refersTo", FrameworkInterfaces.Named)   // any named node, no language-specific rule
```

### Why the key is `Framework.Named` and not `Named`

`language.md` says concept keys are `Language.Name` and the prefix *is* the namespace. A bare `Named` would be
the one key without a namespace, and it would collide with the first language that declares a concept called
`Named` - the test fixture already does. `Framework` says what it is: the framework's vocabulary, not a language
a host can leave out. Prose may shorten it to "Named"; a declaration never does.

### The vocabulary is in every registry

`ConceptRegistry.Create` adds the framework's interface declarations to the set it resolves, so every registry -
a language package's, a project folder's, the workbench's, a test's - contains `Framework.Named`. Declaring the
key yourself is refused with a message that says it is reserved.

Injected rather than composed, because a host composes languages by concatenation
(`[.. ExpressionsLanguage.ConceptDeclarations, .. ConceptDeclarations]`) and the one declaration every language
must share would be reported as a duplicate by exactly that composition. An interface every language has is a
property of the framework, like `PropertyType.String`; the registry is where declarations become a language.

The rejected alternative is the smaller-looking one: let each language declare its own `Named`. Then
`implements Named` would mean "named the way *this* language says", a host could not ask one question of a
document whichever language it is written in, and two languages that both borrow `Expressions` would still
collide on the declaration they both need. A role every language has is vocabulary, and vocabulary belongs to
the layer the languages are written against.

The cost is honest and small: a registry is no longer literally the list it was given, so tests that count
concepts add one. The gain is that a language cannot ship *without* the vocabulary, so a host can rely on
`Framework.Named` meaning the same thing in every document it opens.

Two things stay as they are: `LanguageProject.Declarations` keeps the list the documents wrote - "what the
author said" and "what the registry resolves" were already two answers - and no layer walks `Concepts` demanding
a notation or a slot for every entry, so an interface with neither is invisible to everything that is not asking
about it.

## How a member merges with the implementer's own declaration

Today `ConceptRegistry.Build` deduplicates a slot reached twice only when the two declarations are equal
(`PropertySlot` is a record, so that includes the default value and the description) and otherwise reports a
conflict. An interface needs one step more:

- **Shape is the contract.** Slot kind; a property's `PropertyType`; a child slot's `ChildConceptKey` and
  `Cardinality`; a reference slot's `TargetConceptKey` and `AllowOwnSubtree`.
- **Metadata may be overridden.** A property's `DefaultValue`, `Description` and `ShowDescriptionWhenFilled`.
- **The more derived declaration wins, in place.** The implementer's own declaration beats every super's; between
  two supers the first in declaration order wins, which is the tie-break the name slot already uses. The slot
  keeps its position in `Concept.Slots`, so an override does not reorder the flat slot set.
- **A different shape is a `ConceptDefinitionException`**, naming the concept and both declarations - an override
  is not a licence to change a type.

Why the metadata rule is needed rather than convenient: `Framework.Named` cannot choose a default that suits
every language. The rules language wants `"unnamed"` because `PlaceholderNameCheck` (`STD001`) reports a name
still equal to the concept's declared default, while `Expressions.Name` wants no placeholder at all. Without the
override, `implements Framework.Named` would force every language to adopt one placeholder word - a decision that
belongs to the language, not to the role.

**Do not let an override move a slot.** ADR 0005 makes the saved bytes a contract, and slot order is part of
them; a merge that inserts the interface's slot ahead of the implementer's own would rewrite every document of
that language. Keeping the first-seen position is what lets a language adopt `Framework.Named` without a format
migration.

## Validation

None of this is trusted; `ConceptDeclaration` refuses what it can see alone, and `ConceptRegistry.Create`
refuses the graph.

| Rule | Thrown by | Message shape |
| --- | --- | --- |
| `isInterface` requires `isAbstract` | `ConceptDeclaration` | an interface is abstract by definition |
| An interface declares no child slots | `ConceptRegistry` | interface 'X' declares child slot 'y' |
| An interface declares no triggers | `ConceptRegistry` | no node of an interface can edge a gesture |
| An interface's `Applicability` is `Anywhere` | `ConceptRegistry` | an interface names no place, because it is never a node |
| An interface's supers, extended or implemented, are interfaces | `ConceptRegistry` | 'Y' is not an interface |
| A non-interface does not appear in `interfaces` | `ConceptRegistry` | 'Y' is not an interface - declare it as a super concept |
| A non-interface does not extend an interface | `ConceptRegistry` | 'Y' is an interface - declare it under `implements`, which is the same conformance with a name that says why |
| One key in both lists | `ConceptDeclaration`/`ConceptRegistry` | the same key twice |
| `Framework.Named` is reserved | `ConceptRegistry` | the key is the framework's own interface |
| Interfaces may not cycle | `ConceptRegistry` (existing walk) | inheritance cycle |

`Concept.IsInterface`, `Concept.Interfaces` and `Concept.Extensions` answer the questions a tool asks;
`Concept.SuperConcepts` stays the **ordered union** (extensions, then interfaces), because that is what
`IsKindOf`, the slot merge and every aspect lookup walk. A second list that those walks did not see would be a
second conformance relation by accident.

## The C# path

```csharp
new ConceptDeclaration(
    RulesConceptKeys.Rule,
    [new PropertySlot("name", PropertyType.String, "unnamed"), ...],
    interfaces: [FrameworkInterfaces.Named])
```

- `isInterface` and `interfaces` are appended to the parameter list so every existing call site keeps compiling.
- Extensions come first, then interfaces, in written order - the same order the declared path writes them.
- `NameSlot` keeps working for a concept whose role lives in a property of another name: `Structure.Concept` is
  named by `key`, and no interface should be invented for that.

## The declared path

A language definition has to say the same thing, or ADR 0014's two paths stop being equal.

- `Structure.Concept` gains an `implements` many-slot holding `Structure.SuperType` (its `conceptKey`), beside the
  existing `supertypes`. Two slots rather than one with a kind property, because the header writes two words and
  a kind stored on the reference would be a third spelling of the same fact.
- The header gains the word `interface`, conditional on a new `isInterface` boolean. A concept cannot be
  concrete and an interface at once, and the lowering refuses that combination; the registry refuses a child slot
  or a trigger on one.
- `StructureLowering` passes `supertypes` as `superConcepts` and `implements` as `interfaces`, in document order.
- `StructureNotations` draws the word where `abstract` is drawn and adds an `implements:` section next to
  `extends:`, with the same `<< ... >>` placeholder an empty section already uses.
- `ParityHarness.Dump` gains `interfaces=`, or two languages that differ only in what they implement would compare
  equal.
- `Make interface` / `Make concept` join `Projectional.Structures.Intentions`: an interface is also abstract, so
  the intention sets both flags, and `Make interface` is `Absent` where the concept declares a child slot or a
  trigger - it would produce a definition the registry refuses.

## Adoption, and why it is not a migration

Nothing has to change for existing languages: the `nameSlot` designation stays, and a concept that never
implements `Framework.Named` behaves exactly as it does today. Adoption is a language author's choice, made per
concept.

| Concept | Today | After |
| --- | --- | --- |
| `Expressions.Name` | declares `name` with its own description (`"The name to read, such as amount."`), `nameSlot: "name"` | implements `Framework.Named`; the designation goes, the `name` line stays only to override the interface's description |
| `Rules.RuleSet`, `Rules.Rule` | declare `name` with default `"unnamed"`, `nameSlot` | implement `Framework.Named`; the designation goes, the `name` line stays only to override the default |
| `Test.Rule` (fixture) | same shape | same adoption, so the fixture proves the override |
| `Test.Tag` | named by `label` | unchanged - a designation for a role whose property is not `name` |
| `Structure.Concept`, `TypeRule.*`, `TextGen.*` | named by `key` or `name` | adopt only where the property is `name`, one commit per language |

Two preconditions, checked rather than assumed:

1. **The property is `name`.** A language that names by `key` or `label` keeps the designation; inventing a second
   interface to avoid a word would be a vocabulary nobody asked for.
2. **The slot keeps its position.** Every concept above declares `name` first, so the merge inserts nothing ahead
   of it and the corpus round trip stays byte-identical. A concept whose `name` is declared later would move it
   and fail `CorpusTests` - the answer is to reorder the declaration and re-save that corpus deliberately, or not
   to adopt the interface. Never regenerate a corpus file to turn a red test green.

## Slices

Each slice is shippable on its own and has a commit of its own.

| Slice | Delivers | Proves |
| --- | --- | --- |
| **I1 - Core** | `isInterface`/`interfaces` on the declaration, the resolved split on `Concept`, the validation table, the shape/metadata merge, `FrameworkInterfaces.Named` injected by `ConceptRegistry.Create`, `Node.IsNamed` | `ConceptRegistryTests`, `NodeTests`: an implementer has `name` without declaring it, its own default wins, a shape mismatch is refused, every interface rule throws, a bare registry contains `Framework.Named` |
| **I2 - declared path** | `Structure.implements`, the `interface` header, the lowering and its refusal, the notation, the parity dump, the two intentions | `StructureLoweringTests` (implements lowers, interface + concrete refused), `LanguageParityTests` (dumps include interfaces), `EveryDefinitionDocumentSavesBackByteIdentically` over a definition file that implements `Framework.Named` |
| **I3 - adoption** | the shipped languages in the table above, one commit each | `CorpusTests` byte equality plus the parity dump before and after |
| **I4 - records** | a row per delivery in [implemented.md](../implemented.md), open questions 92-97 answered or carried, and ADR 0020 in [docs/adr](../adr/) recording that the framework's vocabulary is injected into every registry | the documents |

**Gate for the feature:** a concept declares `implements Framework.Named` and nothing else, gets a `name` the
reference cell displays, is found by the scope, and the same language written as structure models lowers to the
same registry dump - with the document still saving back byte-identically.

**The repository skill is part of the delivery.** `.github/skills/projectional-editor/SKILL.md` states that "a
concept is concrete or abstract, and only concrete concepts can be nodes". I1 changes that sentence with the
code, because the skill is what an agent reads before touching `Concept`, and a skill that quietly disagrees
with `ConceptRegistry` is how a second conformance relation gets invented.

## Not in this slice

| Missing | Why it waits |
| --- | --- |
| an interface declaring a child slot | shape, not role - and it would make every implementer's notation owe a cell |
| narrowing an inherited member (an enum where the interface said string, a subtype child) | covariance in a storage model that has none; a language can declare the narrower slot itself instead |
| marker interfaces with no members | legal, and mean exactly `IsKindOf`; a second meaning ("not persisted", "not searchable") is a behavioural switch, not a type |
| per-node interfaces | an interface is part of what a concept *is*; a node that could add one would make the slot set depend on data |
| `Collapsible` as an interface | open question 97: the designation stays, and a ready-made boolean would only be a default |
| aspects declared for an interface | the inherited lookup would apply them to every implementer; useful, but no language has asked, and an aspect is behaviour rather than structure |
| language export emitting a C# interface per interface concept | M5's export row; the declaration is the source, the emission is a generator's choice |

## Open questions

The shape above is chosen. What the questions below leave open is what a second consumer should decide:
[open-questions.md](../open-questions.md) 92-97.
