# Declared structure: a language written as a model

The first half of "a language is data": concepts, slots, inheritance and enumerations written as *nodes*, and one
lowering step that turns them into the `ConceptDeclaration`s every other layer already understands.

Status: **delivered** as slices P1 and P4 of [language-authoring.md](language-authoring.md): the surface reads the
way MPS writes a structure document - a header, a block of options, one section per kind of slot, and every
declaration marked with its own kind and drawn on a line of its own. The file layout is delivered as well:
`language.mps` at the project root is the language itself - a `Structure.Language` node naming it, carrying its
version and listing the languages it stands on ([language-projects.md](language-projects.md)). Those lists are
resolved through the host, as a vocabulary: the language's registry is built over its bases' declarations, while a
base's notation and behaviour stay its own ([ADR 0026](../adr/0026-a-dependency-resolves-to-the-vocabulary.md)).

## Why this and not a config file

A language definition is a model, so it is edited by the same editor, saved by the same writer, undone by the same
history and validated by the same registries. Nothing about a definition is special: a file in the language's `structure` folder is a document
whose concepts happen to be `Structure.Concept` and `Structure.Slot`.

That is the property [ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) needs. The declared path is
lowered to *exactly* the objects the C# builders produce - one `ConceptDeclaration` per declared concept, with the
slots it declared and nothing else - so no layer below knows where a concept came from. There is no second
registry and no declared-only node kind.

## The language

Ten concepts, and the small number is the design: a definition says what a concept **is**, never how it looks.
Presentation is the editor language (P2), which is a separate model for the same reason MPS splits
a structure document from an editor document - a definition that mixes the two cannot be reviewed.

```text
  enum Rules.Outcome
    accept
    review
```

```text
      concept Rules.Element

    instance can be root: true
    alias: <no alias>
    short description: <no short description>
    offered in completion: true
    named by:
    collapsed by:
    named by rule:

    properties:
      << ... >>

    children:
      << ... >>

    references:
      << ... >>
```

```text
      abstract concept Rules.Rule extends Rules.Element
      implements Framework.Named

    instance can be root: true
    alias: Rule
    short description: One rule of a ruleset.
    offered in completion: true
    named by: name
    collapsed by:
    named by rule:

    properties:
      name : string
        default:
        described as: What the rule is called.
        described when filled: false

    children:
      steps : Rules.Step[0..n]

    references:
      condition : Rules.Condition
        own subtree: false
```

`abstract` is a word of the header and not an option, which is MPS' spelling and the better reading of the same
fact: abstractness is part of what a concept *is*. A concrete concept therefore draws nothing where an abstract one
draws the word, which is a [conditional cell](language.md#notation) - and the reason toggling the flag is an
[intention](context-actions.md) rather than typing into a value.

| Concept | Holds |
| --- | --- |
| `Structure.Language` | the definition: `enumerations`, `concepts` |
| `Structure.Concept` | `key`, `isAbstract`, `isRoot`, `alias`, `isOfferedInCompletion`, `description`, `nameSlot`, `collapseSlot`, `nameRule`, `supertypes`, `properties`, `children`, `references` |
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
extends, then the `implements` list on a row of its own, then a block of options and one section per kind of slot,
the groups separated by blank lines. Copying the shape is worth it for the reason MPS has it: a reviewer reads the
inheritance and the kind of the concept in the header, what it allows in the options, and what it holds in three
named sections. There are no braces: a concept is a document, and the grouping says where each part ends. The body
sits two indents in from the page edge and the header one step further than the options - the same shape MPS
draws, so the page does not start on the border and "what the concept is" is visibly apart from "how the framework
treats it".

Three decisions make that readable, and all three are P4:

- **The header carries the inheritance, and only what is true** - `concept Rules.Rule extends Rules.Element` and the
  `implements` row under it, or just `concept Rules.Rule` when there is neither. `extends` is an
  [inline while-filled cell](language.md#notation) and `implements` the block form of the same idea, so an empty
  list draws nothing at all rather than a placeholder nobody wants to read. The price is stated in the same place as
  the feature: an empty list has no caret stop, so `Inherit from…` and `Implement interface…`
  (`Projectional.Structures.Intentions`) are the way to write the first entry - and to append a second, because
  Enter does not extend a list inside a line.
- **Every section reads the same way, and nothing carries a marker.** A row is `name : type` for a property,
  `name : Concept[range]` for a child and `name : Concept` for a reference, because the heading above it already
  says which kind it is - one marker per row would repeat the heading once per line, and MPS draws them without one
  for exactly that reason. What a row needs instead of a closing marker is the indentation of the lines that hang
  under it: a property's `default:` and `described as:`, a reference's `own subtree:`, are one step in from the row
  they belong to.
- **Every collection stacks, and an empty section says `<< ... >>`.** Layout draws a collection inline unless the
  notation asks it to ([projection.md](projection.md) §Layout), so two properties of one concept used to be drawn
  side by side and an enumeration's symbols shared a line. A definition is read one declaration per line. The empty
  state of a section is the one shared placeholder because the heading above it already names the kind, and saying
  it twice is what MPS does not do. The two header lists are the exception: their words are `extends` and
  `implements`, so they need no placeholder and are simply not drawn.

What that look needs is a cell that can disappear, and the notation layer has two: a
[conditional cell](language.md#notation) draws its inline cells only while a property holds a value, and the
while-filled cell does the same for a child slot - inline for a word, as a block when a whole row has to go, which
is what puts `implements` on its own line without leaving the blank row an emptied line would keep. `abstract` is
the case the conditional was added for - a concrete concept draws `concept ...`, an abstract one
`abstract concept ...`.

The remaining gap is not the notation's:

- **Neither `instance can be root` nor `alias` is a gap any more.** Both are `ConceptDeclaration` arguments
  (`isRoot`, `alias`) and both have a consumer: the model refuses to make a non-root concept a document root, and
  the proposal list shows an alias instead of the concept's own name. That is the form ADR 0014 asks for - a
  declared fact is only worth adding once something below reads it.
- **A declared block conditional.** The C# cell exists (`WhenFilledBlockNotationCell`) because the concept notation
  needed it; the editor language has no `Editor.WhileFilledBlock` yet, so a language written as a model cannot use
  the block form. ADR 0014's rule is not broken by that - it forbids a capability only the *workbench* can express -
  but the two paths are unequal here until the declared twin arrives.

## The lowering, and what it refuses to check

`StructureLowering.Lower(document)` answers the declarations; `CreateRegistry(document)` answers the registry those
declarations build.

- **It checks what the compiler would have checked** for a hand-written language: that every property names a type,
  that the type is one of the five built-ins or a declared enumeration, that an enumeration has symbols and a unique
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
| a declared block conditional (`Editor.WhileFilledBlock`) | when a declared language needs to hide a whole row |
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
