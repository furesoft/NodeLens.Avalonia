# Declared structure: a language written as a model

The first half of "a language is data": concepts, slots, inheritance and enumerations written as *nodes*, and one
lowering step that turns them into the `ConceptDeclaration`s every other layer already understands.

Status: **delivered** as slices P1 and P4 of [language-authoring.md](language-authoring.md): the surface reads the
way MPS writes a structure document - a header, a block of options, one section per kind of slot, and every
declaration marked with its own kind and drawn on a line of its own. What is still provisional is the *file* layout
(`language.mps` as one model per aspect, dependencies): the loader reads the folder
([language-projects.md](language-projects.md)), and a descriptor document has no consumer until a language can
extend another one.

## Why this and not a config file

A language definition is a model, so it is edited by the same editor, saved by the same writer, undone by the same
history and validated by the same registries. Nothing about a definition is special: a file in the language's `structure` folder is a document
whose concepts happen to be `Structure.Concept` and `Structure.Slot`.

That is the property [ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) needs. The declared path is
lowered to *exactly* the objects the C# builders produce - one `ConceptDeclaration` per declared concept, with the
slots it declared and nothing else - so no layer below knows where a concept came from. There is no second
registry and no declared-only node kind.

## The language

Nine concepts, and the small number is the design: a definition says what a concept **is**, never how it looks.
Presentation is the editor language (P2), which is a separate model for the same reason MPS splits
a structure document from an editor document - a definition that mixes the two cannot be reviewed.

```text
enum Rules.Outcome
  accept
  review
```

```text
concept Rules.Element {
  offered in completion: true
  named by:
  collapsed by:
  named by rule:
  described as:
  properties:
    <<property name : string
      default:
      described as:
      described when filled: false>>
  children:
    <<child>>
  references:
    <<reference>>
}
```

```text
abstract concept Rules.Rule extends Rules.Element, implements Framework.Named {
  offered in completion: true
  named by: name
  collapsed by:
  named by rule:
  described as: One rule of a ruleset.
  properties:
    <<property name : string
      default:
      described as: What the rule is called.
      described when filled: false>>
  children:
    <<child steps : Rules.Step[0..n]>>
  references:
    <<reference condition : Rules.Condition
      own subtree: false>>
}
```

`abstract` is a word of the header and not an option, which is MPS' spelling and the better reading of the same
fact: abstractness is part of what a concept *is*. A concrete concept therefore draws nothing where an abstract one
draws the word, which is a [conditional cell](language.md#notation) - and the reason toggling the flag is an
[intention](context-actions.md) rather than typing into a value.

| Concept | Holds |
| --- | --- |
| `Structure.Language` | the definition: `enumerations`, `concepts` |
| `Structure.Concept` | `key`, `isAbstract`, `isOfferedInCompletion`, `description`, `nameSlot`, `collapseSlot`, `nameRule`, `supertypes`, `properties`, `children`, `references` |
| `Structure.SuperType` | `conceptKey` |
| `Structure.Slot` (abstract) | `name` |
| `Structure.PropertyDecl` | `typeName`, `defaultValue`, `description` |
| `Structure.ChildDecl` | `childConceptKey`, `cardinality` (`0..1`, `0..n`, `1`, `1..n`) |
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

**The cardinality and the type name are data rather than types on purpose**: a `cardinality` names one of the four
ranges the notation offers in one cell (`0..1`, `0..n`, `1`, `1..n`), and a `typeName` is a name a language author
types. Both are checked by the lowering, not by the model - a model that refused them would have to know the concept
registry, which is exactly the layering this project does not allow. The range reaches the runtime as
`ChildSlot.Cardinality` (the storage shape) plus `ChildSlot.Minimum` (the lower end), so a definition writes a
statement a check can read instead of a flag the model has to interpret.

## What MPS's look the engine can and cannot do

`StructureNotations` draws the definition in MPS's shape: a header that says what the concept *is* and what it
extends and implements, then a body in braces with a block of options and one section per kind of slot. Copying the
shape is worth it for the reason MPS has it: a reviewer reads the inheritance and the kind of the concept in the
header, what it allows in the options, and what it holds in three named sections.

Three decisions make that readable, and all three are P4:

- **The header carries the inheritance, and only what is true** - `concept Rules.Rule extends Rules.Element,
  implements Framework.Named {`, and `concept Rules.Rule {` when there is neither. Both lists are inline
  collections wrapped in a [while-filled cell](language.md#notation), so an empty one draws nothing at all rather
  than a placeholder nobody wants to read. The price is stated in the same place as the feature: an empty list has
  no caret stop, so `Inherit from…` and `Implement interface…` (`Projectional.Structures.Intentions`) are the way
  to write the first entry - and to append a second, because Enter does not extend a list inside a line.
- **A declaration says its own kind and closes where it ends** - `<<property name : string ... >>`, `<<child steps :
  Rules.Step[1..n]>>`, `<<reference condition : Rules.Condition ... >>`. A property carries three option lines under
  it, so without a visible end the reader cannot tell where one declaration stops and the next begins. The word
  inside the marker is MPS' word for the same three things.
- **Every collection stacks, and an empty one names its kind.** Layout draws a collection inline unless the
  notation asks it to ([projection.md](projection.md) §Layout), so two properties of one concept used to be drawn
  side by side and an enumeration's symbols shared a line. A definition is read one declaration per line. The empty
  state of a *slot* section is `<<property>>`, `<<child>>`, `<<reference>>` and `<<symbol>>` rather than one shared
  placeholder, because the section heading groups declarations and the placeholder has to say which kind stands in
  the slot it will create - a placeholder that said nothing would offer Enter a slot it could not name. The two
  header lists are the exception that proves it: their words are `extends` and `implements`, so they need no
  placeholder and are simply not drawn.

What that look needs is a cell that can disappear, and the notation layer has one: a
[conditional cell](language.md#notation) draws its inline cells only while a property holds a value. `abstract` is
the case it was added for - a concrete concept draws `concept ...`, an abstract one `abstract concept ...`.

The remaining gap is not the notation's:

- **`instance can be root` and `alias`.** MPS offers both; neither has anywhere to land here. Applicability is a
  `ConceptDeclaration` argument with `Anywhere`/`AsChild`/`AsRoot` places, and an alias would be a second name for a
  concept that no layer below reads. ADR 0014's rule - nothing declared-only - keeps them out until something
  consumes them.
- **A block conditional** - hiding a whole option line rather than a word - would be the same mechanism one level up,
  and it is not here because nothing needs it yet.

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
| notation as data (one editor document per notation) | P2 |
| `language.mps` as one model per aspect, with dependencies | when a language can extend another, which is what a dependency list is for |
| a block conditional, which would let a whole option line disappear | when a notation needs it |
| toggling the flag from the header rather than from a menu | it is an [intention](context-actions.md), by design |
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
