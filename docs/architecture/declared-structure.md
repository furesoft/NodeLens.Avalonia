# Declared structure: a language written as a model

The first half of "a language is data": concepts, slots, inheritance and enumerations written as *nodes*, and one
lowering step that turns them into the `ConceptDeclaration`s every other layer already understands.

Status: **delivered** as slice P1 of [language-authoring.md](language-authoring.md), and the surface now reads the
way MPS writes `structure.mps` - one section per kind of slot. What is still provisional is the *file* layout
(`language.mps` as one model per aspect, dependencies), which is P3/P4.

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
enumerations:
  enum Rules.Outcome
    accept
    review
concepts:
  concept Rules.Rule
    is abstract: false
    offered in completion: true
    named by: name
    collapsed by:
    described as: One rule of a ruleset.
    extends:
      Rules.Element
    properties:
      name : string
        default:
        described as: What the rule is called.
    children:
      steps : Rules.Step cardinality: many
    references:
      condition : Rules.Condition own subtree: false
```

| Concept | Holds |
| --- | --- |
| `Structure.Language` | the definition: `enumerations`, `concepts` |
| `Structure.Concept` | `key`, `isAbstract`, `isOfferedInCompletion`, `description`, `nameSlot`, `collapseSlot`, `supertypes`, `properties`, `children`, `references` |
| `Structure.SuperType` | `conceptKey` |
| `Structure.Slot` (abstract) | `name` |
| `Structure.PropertyDecl` | `typeName`, `defaultValue`, `description` |
| `Structure.ChildDecl` | `childConceptKey`, `cardinality` (`one`/`many`) |
| `Structure.ReferenceDecl` | `targetConceptKey`, `allowOwnSubtree` |
| `Structure.Enumeration` | `key`, `symbols` |
| `Structure.EnumSymbol` | `symbol` |

**Three collections, not one.** A concept holds `properties`, `children` and `references` as three separate
`Many` slots rather than one `slots` list of three kinds. The reason is the reason MPS splits them: the kind of a
slot decides which sections of the notation can name it, and a single list would make "is this name a property or a
reference" a question answered at render time by looking at the child's concept. With three collections the model
answers it, and the notation says exactly one thing per section.

The consequence is a **fixed rank**: a concept's slots are lowered properties first, then children, then
references. That rank is the concept's declaration order, which decides the default fill of a slot and the ranking
in the completion list, so it is part of the contract rather than an artefact - a definition that creates a
reference before a property still declares the property first.

Two spellings are data rather than types on purpose: a `cardinality` is the text `one` or `many`, and a
`typeName` is a name a language author types. Both are checked by the lowering, not by the model - a model that
refused them would have to know the concept registry, which is exactly the layering this project does not allow.

## What MPS's look the engine can and cannot do

`StructureNotations` draws the definition in MPS's shape - a header, a block of options, and one section per kind of
slot, with `<< ... >>` for a section nobody has filled. Copying the shape is worth it for the reason MPS has it: a
reviewer reads what a concept *is* in the header, what it allows in the options, and what it holds in three named
sections.

Two parts of that look are out of reach, and the honest half is to say so rather than to fake them:

- **Conditional cells.** MPS shows `abstract` only when the concept is abstract; a notation here is a fixed tree of
  cells, so `is abstract:` is drawn always, with its value. Making a cell disappear needs a mechanism the notation
  layer does not have, and inventing one for this document would be a second mechanism beside the notation.
- **`instance can be root` and `alias`.** MPS offers both; neither has anywhere to land here. Applicability is a
  `ConceptDeclaration` argument with `Anywhere`/`AsChild`/`AsRoot` places, and an alias would be a second name for a
  concept that no layer below reads. ADR 0014's rule - nothing declared-only - keeps them out until something
  consumes them.

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
| `language.mps` as one model per aspect, with dependencies | P3, P4 |
| conditional cells, which is what would let `abstract` be shown only when true | not planned; a notation is a fixed tree |
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
