# Declared structure: a language written as a model

The first half of "a language is data": concepts, slots, inheritance and enumerations written as *nodes*, and one
lowering step that turns them into the `ConceptDeclaration`s every other layer already understands.

Status: **delivered** as slice P1 of [language-authoring.md](language-authoring.md). It is the provisional shape -
what a definition says, not yet how MPS writes it (P4) and not yet notation as data (P2).

## Why this and not a config file

A language definition is a model, so it is edited by the same editor, saved by the same writer, undone by the same
history and validated by the same registries. Nothing about a definition is special: `structure.mps` is a document
whose concepts happen to be `Structure.Concept` and `Structure.Slot`.

That is the property [ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) needs. The declared path is
lowered to *exactly* the objects the C# builders produce - one `ConceptDeclaration` per declared concept, with the
slots it declared and nothing else - so no layer below knows where a concept came from. There is no second
registry and no declared-only node kind.

## The language

Nine concepts, and the small number is the design: a definition says what a concept **is**, never how it looks.
Presentation is the editor language (P2), which is a separate model for the same reason MPS splits
`structure.mps` from `editor.mps` - a definition that mixes the two cannot be reviewed.

```text
enum Rules.Outcome
  accept
  review
concept Rules.Rule abstract=false offered=true
  named by name
  described as One rule of a ruleset.
  extends Rules.Element
  property name : string
    default
    described as What the rule is called.
  child steps : Rules.Step
    cardinality many
  reference condition : Rules.Condition
    own subtree false
```

| Concept | Holds |
| --- | --- |
| `Structure.Language` | the definition: `enumerations`, `concepts` |
| `Structure.Concept` | `key`, `isAbstract`, `isOfferedInCompletion`, `description`, `nameSlot`, `supertypes`, `slots` |
| `Structure.SuperType` | `conceptKey` |
| `Structure.Slot` (abstract) | `name` |
| `Structure.PropertyDecl` | `typeName`, `defaultValue`, `description` |
| `Structure.ChildDecl` | `childConceptKey`, `cardinality` (`one`/`many`) |
| `Structure.ReferenceDecl` | `targetConceptKey`, `allowOwnSubtree` |
| `Structure.Enumeration` | `key`, `symbols` |
| `Structure.EnumSymbol` | `symbol` |

Two spellings are data rather than types on purpose: a `cardinality` is the text `one` or `many`, and a
`typeName` is a name a language author types. Both are checked by the lowering, not by the model - a model that
refused them would have to know the concept registry, which is exactly the layering this project does not allow.

## The lowering, and what it refuses to check

`StructureLowering.Lower(document)` answers the declarations; `CreateRegistry(document)` answers the registry those
declarations build.

- **It checks what the compiler would have checked** for a hand-written language: that every property names a type,
  that the type is one of the four built-ins or a declared enumeration, that an enumeration has symbols and a unique
  key, and that every slot is one of the three kinds. Each failure is a `ConceptDefinitionException` naming the
  concept and the slot, because a definition is user data and its errors are user-facing.
- **It does not check the graph** - whether a super concept exists, whether a child slot names a declared concept,
  whether a name slot is declared. `ConceptRegistry.Create` is the single validator for that, and a second one here
  would eventually disagree with it. `CreateRegistry` is therefore the call that answers "is this a language".
- **Duplicates are the registry's**, too: `ConceptDeclaration` owns the key's shape, so two concepts with one key
  fail when the registry is built, not while lowering.

## The namespace is `Projectional.Structures`

The plan says `Projectional.Structure`, and the .NET name is plural because CA1716 is an error here: `Structure` is
a reserved word in VB, so a namespace called `Projectional.Structure` would be flagged on every file that declares
it. The project is `Projectional.Structures`, the language inside it is still the *structure language*, and the
classes keep their names (`StructureLanguage`, `StructureLowering`, `StructureConceptKeys`).

## What is deliberately not here yet

| Missing | Arrives with |
| --- | --- |
| notation as data (`editor.mps`) | P2 |
| the MPS-shaped surface: `language.mps`, one model per aspect, dependencies | P3, P4 |
| triggers, applicability and collapse slots as declared data | after P2, when an aspect model exists to hold them |
| constraints, types, generators as declared data | M6 |
| loading a definition from a project folder | P3, with the aspect models it needs |

A declaration that the shape above cannot express is not silently dropped: it cannot be written at all, which is
the property to keep until the shape grows on purpose.

## Testing

`StructureLoweringTests` builds a definition the way a user would - one model, nodes created through it - and then
uses what it lowered the way a hand-written language is used: create nodes, watch a slot refuse the wrong concept,
watch an enumeration refuse an undeclared symbol, and render the definition itself with `LayoutSnapshot`. The last
one is the plan's rule made executable: if the definition language needed a rendering exception, it would not be a
language.

Covered failures: a property with no type or an unknown one, an enumeration with no symbols, two concepts with one
key, a super concept that does not exist, and a document that is not a definition at all.
