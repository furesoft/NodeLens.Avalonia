# Authoring a language

How a language comes into existence. [language.md](language.md) describes what a language
*is* - concepts, notation, aspects; this document describes how one is *written down*, in two
paths that both stay in the product for good, and what the authoring language itself has to
look like so that defining a language feels the way it does in MPS.

Status: **planned.** The code path exists from M1 (`Projectional.Expressions`,
`Projectional.Samples`, `TestLanguage` in the tests, all fluent C#). The project path is
provisional in M2 (a generic form over declared data) and becomes the MPS-shaped definition
languages in M5. The permanence of both is decided in
[ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md).

## Two paths, one registry

| Path | Written in | Can express | Ships as | Fixture today |
| --- | --- | --- | --- | --- |
| **Code** | C# fluent declarations from `Projectional.Toolkit` | every declaration *and* every behaviour | a NuGet package | yes - every shipped language |
| **Project** | structure, editor and aspect models inside the project | declarations only, no code | project files | no |

The declared path is not a replacement and not a lower tier. Both produce the same runtime
objects and register into the same `ConceptRegistry` / `NotationRegistry`; a model cannot tell
which path its language came from, and does not ask. ADR 0008's staging (code first, then
declared, then self-hosting) is the *order* in which the machinery appears, not a lifecycle in
which one path dies.

### The parity rule

The invariant, in one sentence: **the declared form can express nothing the code form cannot,
and every capability is reachable from both.**

It is spelled out as four rules, because "both are supported" is too vague to review against:

1. **Lowering, not reimplementation.** A declared concept, slot, notation or annotation is
   lowered to the same `ConceptDeclaration`, `Notation` and aspect delegates that a C# author
   writes by hand. There is no second notation engine and no declared-only cell kind; if a
   declared construct cannot be lowered, the construct does not exist yet.
2. **No declared-only feature.** A capability added to the workbench ships in the same commit
   with its C# equivalent. Otherwise a language exists that no package can carry, and a host
   that references the framework cannot load it.
3. **Behaviour stays in code.** Aspects that decide something (`Validate`, `TypeOf`,
   `ScopeProvider`, `Evaluate`, `GenerateText`) are delegates. A declared aspect is a
   *declarative form* that lowers into such a delegate; the delegate itself is never data. This
   is a security property ([integration.md](integration.md#language-plugins)) and the reason the
   code path can never be retired.
4. **The core languages keep their C# twin after stage C.** It is the parity oracle, the
   fail-safe when a bootstrap cannot load, and the example a language author reads. It lives in
   the test corpus, not in the product.

Three tests keep the rule honest, and they are the acceptance gate of every slice below:

| Test | Asserts |
| --- | --- |
| **Registry dump** | the two definitions of one language produce the same concept keys, slot names, cardinalities, super-concept chains, abstractness, completion flags and designated slots, in the same order |
| **Snapshot** | one fixture document rendered through both produces byte-identical `LayoutSnapshot` output |
| **Diagnostics** | the same document produces the same findings, with the same codes, targets and ranges |

The registry dump is the interesting one, because order is part of the contract: declaration
order decides what an empty slot is filled with ([language.md](language.md#concept)), so a
parity test that compared sets instead of sequences would miss a real difference.

All three are implemented (`feat/language-parity`, over the declared twin of `TestLanguage`); how each one is
made comparable, and the two parity gaps it found on its first run, are in
[language-parity.md](language-parity.md).

## What a language project is

A language is a **project folder**, not a file, and that shape is taken from MPS because it is
the shape the problem has:

```text
MyRules/
  language.mps           the language root: name, what it is for, version, dependencies, extended languages
  structure/             concepts, slots, inheritance, enumerations - one definition per document
    Pipeline.Pipeline.mps
    Pipeline.Stage.mps
    Pipeline.Outcome.mps
  editor/                notation, one editor per concept - one document per notation
    Pipeline.Pipeline.mps
    Pipeline.Stage.mps
  constraints/           validation rules                    (M6)
  types/                 type and subtype rules, one rule per document
    Pipeline.Stage.mps
    Amount.mps
  generator/             text output rules, one generator per document
    Json.mps
  intentions/            actions offered at the caret        (M6)
  accessories/           completion, keymap, node factories   (M6)
```

One model per aspect, because the aspects are edited at different times by different concerns
and a language definition that mixes "what a rule is" with "what colour its keyword is" cannot
be reviewed. The `language` root is the only model every language has; the rest appear when the
language has something to say in them ([persistence.md](persistence.md) for the on-disk layout).

**Each aspect is a folder of documents, and each document holds one definition** - one concept,
one enumeration, one notation, one type rule or named type, one generator. That is how MPS stores
a model: one root per file, named after what it holds. The split is not cosmetic; it is what makes
the aspects readable and reviewable, because a diff of one concept is a diff of one file. Inside a
document the root *is* the definition: there is no `language`-style container concept whose only
job would be to list its children. The single exception is `language.mps` at the project root,
which is not a document *of* an aspect but the language itself - it names the language, may say what
it is for, carries its version and lists the languages it stands on, and `Projectional.Languages`
reads it beside the folders rather than through one.

The folder names are the aspects' (`structure`, `editor`, `types`, `generator`), and so is the
split, because the alternative - one file with every aspect as a section - loses exactly the
property that makes MPS languages readable: you can open one concept and see the shape of the
domain without any presentation.

## How a concept reads

The structure model is written in `Projectional.Structures`, and it reads like MPS' structure
language because it is the same three ideas: a header that says what the concept *is* and what it
extends, angle bracket blocks that say what it *holds*, and nothing else.

```text
      concept RuleSet
      implements Framework.Named

    instance can be root: true
    alias: <no alias>
    short description: <no short description>
    offered in completion: true
    named by: name
    collapsed by:
    named by rule:

    properties:
      name : string
        default:
        described as:
        described when filled: false

    children:
      rules : Rules.Rule[0..n]

    references:
      << ... >>
```

```text
      abstract concept Condition

    instance can be root: true
    alias: <no alias>
    short description: <no short description>
    offered in completion: true
    named by:
    collapsed by:
    named by rule:

    properties:
      negated : boolean
        default:
        described as:
        described when filled: false

    children:
      << ... >>

    references:
      << ... >>
```

```text
    enum Operator
      Less
      Equal
      Greater
```

The header says what the concept *is* - including what it extends and implements, so a reader does
not have to scan the body for the shape of a hierarchy. The three sections below it say what it
holds, one row per slot in the same spelling for all three kinds - `name : type`, `name : Concept[range]`,
`name : Concept` - because the heading above each section already says which kind it is. The lines that
hang under a row are one step further in, which is what marks where the row ends: that is what makes a
section with four slots readable.

The vocabulary, and what each piece lowers to:

| Written | Lowers to | MPS equivalent |
| --- | --- | --- |
| `concept X`, `abstract concept X` | `ConceptDeclaration(key, isAbstract)` | `concept` / `abstract concept` |
| `interface concept X` | `ConceptDeclaration(key, isAbstract: true, isInterface: true)` - a concept kind whose members implementers inherit, not a second conformance relation ([interfaces.md](interfaces.md)) | `interface concept` |
| `... extends A, B, ...` | `superConcepts`, in written order | `extends` |
| `... implements I, J` | `interfaces`, in written order; the same `IsKindOf` conformance, resolved after the extensions | `implements` |
| `name : string` | a property slot plus its `PropertyType` | a property row |
| `steps : Step[0..n]` | a child slot plus its cardinality | `<<child>>` |
| `target : Rule` | a reference slot plus its target | a reference row |
| `described as: ...` | the concept's documentation ([concept-descriptions.md](concept-descriptions.md); lowered to `ConceptDeclaration(description:)`) | `<<shortDescription>>` |
| `instance can be root: false` | `ConceptDeclaration(isRoot: false)`, and the model then refuses to make one a document root; absent means true, because that is what every concept meant before the flag existed | `instance can be root` |
| `alias: RS` | `ConceptDeclaration(alias:)`: the word a proposal list shows and filters by, while the entry still inserts the declared key | `aliases` |
| `named by: name`, `collapsed by: visible` | a designated slot ([language.md](language.md#the-two-designated-slots)) | none - MPS has no analogue |
| `offered in completion: false` | `isOfferedInCompletion` | `<<hideInEditor>>`-style flags |

Four details are deliberate:

- **Slot blocks carry the type, not the notation.** A slot is storage; what it looks like is the
  editor model's business. Mixing the two is how MPS beginners produce concepts whose notation
  cannot be reused.
- **The cardinality is written in brackets, always.** `[1]` for required single, `[0..1]` for
  optional, `[0..n]` and `[1..n]` for lists. A default of "you did not say" would be a fourth
  meaning the reader has to learn.
- **Inheritance is in the header; designated slots are below it.** The header answers "what is
  this" - and a list that holds nothing is not drawn, so the header says only what is true of the
  concept. Because that leaves the empty list without a cell, the two ways in are intentions:
  `Inherit from…` and `Implement interface…` create the first entry and put the caret on its key. The
  body answers "what does it hold and how does the framework treat it".
- **`implements` promises members, not a drawing.** An interface contributes the properties and references
  an implementing concept inherits - the framework's `Framework.Named` is the name property and the name-slot
  designation in one declaration - and it may declare no child slot, so implementing one can never make a
  concept's notation owe a cell it never asked for ([interfaces.md](interfaces.md)).

One honesty note about the brackets: `Cardinality` has two values, `One` and `Many`, and the
minimum is a slot property beside it - open question 64's answer, delivered with the four ranges
this notation offers. `[0..1]` and `[1]` therefore differ in `ChildSlot.Minimum` rather than in the
storage shape, and the difference is *reported* by `STD010`/`STD011` instead of enforced at the
keystroke: deleting the last element of a `1..n` list stays an ordinary edit. A minimum above one
("at least two rules") is representable in the model and has no spelling in this notation yet.

## How an editor reads

The editor model is written in `Projectional.Editor`. A concept editor is a nested cell tree
drawn with brackets and a glyph per cell, which is what makes MPS notation editable as text-like
code rather than through a wizard:

```text
editor for concept Pipeline.Pipeline variant: default
node cell layout:
  [/
    [-pipeline- ; %name% empty:  ]
    [>
      [/ table stages empty: + stage
        column name as -stage- empty: <empty>
        column command as -run- empty: <empty>
        column retries as -retries- empty: <empty>
      /]
    <]
  /]
```

```text
editor for concept Pipeline.Stage variant: default
node cell layout:
  [%name% empty:   ; %command% empty:   ; %retries% empty:  ]
```

Both are the `samples/declared-pipeline` documents as the shipping notation draws them. The first stacks a header
line and an indented block; the second is one row of three property cells with the gaps between them.

The glyph set, and the cell each one lowers to:

| Written | Lowers to | Reading |
| --- | --- | --- |
| `-keyword-` | `ConstantCell` | a fixed token, not editable |
| `%name%` | `PropertyCell` | the value of one property |
| `{ slot }` | `CollectionCell` | a many-valued slot, separator from the notation |
| `( slot )` | `SingleChildCell` | the one child's own notation, inline |
| `< slot >` | `ReferenceCell` | the target's name |
| `;` | `SpaceCell` | a gap in the gap's own address |
| `[/ ... /]` | `SequenceCell` | blocks stacked unchanged |
| `[> ... <]` | `IndentCell` | a block, one level deeper |
| `[ ... ]` | `LineCell` | one line, inline cells only |
| `[\| ... \|]` | `HorizontalCell` | blocks side by side, which is how a line keeps its brackets |

The bracket letters are the notation, so a misplaced cell fails when the editor model is
validated, exactly as `ed.Line(ed.Indent(...))` fails to compile in C#
([language.md](language.md#notation)). A variant is the `variant` property of the notation - `variant: table` -
because a variant is notation for the same concept, not a second concept.

The point of the glyphs is that the *definition* is projected by the same engine it defines:
one editor, one cell set, one caret. There is no separate authoring form widget, so the
definition language gets selection, undo, variants, folding, markers and completion for free,
and every improvement to the editor improves the thing that makes languages.

## Aspects are annotations

An aspect model attaches behaviour to a concept with an **annotation** on the concept block, the
way MPS attaches `@constraints`, `@editor` or `@typeSystem`:

```text
@constraints

concept RuleSet
  <<validate : a ruleset needs at least one rule>>
  | rules.isEmpty() -> error "A ruleset needs at least one rule."
  | otherwise       -> ok

@intentions

concept Comparison
  <<intention : swap operands>>
  | left, right := right, left
```

What that buys, and why it is not just syntax:

- **Each aspect is its own language.** `Projectional.Constraints`, `Projectional.Types`,
  `Projectional.Generators`, `Projectional.Intentions`, `Projectional.TextGen`,
  `Projectional.Accessories`, `Projectional.Migrations`. A new kind of aspect is therefore a new
  language plus a new annotation concept, not a new field in a sealed enum of aspects - which is
  exactly how aspects grow in MPS, and the reason a language author can add one without a
  framework change. Two of the list are delivered: `Projectional.TextGen` is the generator language,
  a language project's `generator` folder beside the structure and the notation, and
  `Projectional.ConstraintRules` is the constraint language, its `constraints` folder beside them.
- **The block is keyed by concept, and the aspect is collected per concept.** Lowering walks the
  aspect model once, gathers the block for each concept key and produces the delegate the C#
  author would have written. Concepts with no block get no delegate, which is the same state as
  a C# concept that sets no aspect.
- **The annotation is a normal concept.** `@constraints` is an annotation node with a notation,
  so it can be completed, validated and marked like anything else. Nothing about it is special
  to the framework except that the lowering knows its key.

Not every aspect is worth a declarative form, and the plan says which:

| Aspect | Declared form | Lands | Why |
| --- | --- | --- | --- |
| structure, notation | full | M2 (provisional), M5 (MPS shape) | pure declaration, the reason the path exists |
| constraints | expression-shaped rules | M6 | small closed vocabulary, covers the common check |
| types | rule table over concept keys and slots | M6 | MPS' type system is the same idea; expressions stay for the odd case |
| scopes | chain description plus per-slot overrides | M6 (delivered) | MPS needs a language for this too; the default chain covers most DSLs |
| generation | template rules with traceability | M4 (delivered) | the largest declarative win, and the largest piece of work - it arrived with the generator itself rather than a milestone later |
| intentions | block per concept | M6 (intentions delivered: wrap with presets, set a property; textGen and accessories remain) | declarative by nature; these are where DSL authors spend their time |
| interpretation | never | - | evaluation is a host decision with security consequences |
| migrations | stepping rules per version | M6 | declared form once one language has two versions |

The table is the answer to "why is `Evaluate` in the code path only": a declared language is
data, and data that runs is a sandbox we have deliberately not built
([integration.md](integration.md#language-plugins)).

## Why it has to look like MPS

Not because imitation is a goal, but because the MPS answer is the one that survives contact
with real DSLs, and each of its shapes has a reason this project would otherwise have to
rediscover:

1. **The definition language is itself projectional.** A wizard or a property grid would be a
   second editing surface with its own bugs, its own caret and none of the framework's tests.
   Using the editor to define languages means the self-hosting proof (stage C) is not a rewrite
   but a project folder.
2. **Aspects are annotations on a concept.** The alternative - a registry of aspects per concept
   in C# - would make the set of aspects sealed by the framework, which is the one thing a
   language workbench must not do.
3. **Structure and editor are separate models.** The domain shape and its presentation age at
   different rates; MPS keeps them apart and so does this.
4. **The author sees the language, not the data.** A definition is projected with the notation it
   defines, so a mistake looks like a mistake in the DSL rather than in a form field. Live
   preview (M5) then closes the loop: edit the definition, watch the model change.
5. **It is a known-good mapping.** Every deviation from MPS costs a design argument; the burden
   of proof sits on the deviation, not on the analogy.

What is *not* taken from MPS: `smodel`-style query languages inside aspects (our aspect blocks
are declarative rules, and anything beyond them is C#), the Java type system, and aspect
languages for features this project does not have (data flow, checking rules).

## The code path a host ships

The code path is the only path that can be packaged, so it is documented as a product feature
rather than as an internal builder. Today it is the shape `ExpressionsLanguage` uses - plain
constructor calls, no fluent layer, with the notation and the typing rules added through
separate registries:

```csharp
public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
[
    .. ExpressionsLanguage.ConceptDeclarations,

    new ConceptDeclaration(
        "Rules.RuleSet",
        [
            new PropertySlot("name", PropertyType.String),
            new ChildSlot("rules", "Rules.Rule", Cardinality.Many),
        ],
        nameSlot: "name"),
];
```

That is correct and testable, but it is more verbose than what [language.md](language.md) shows:
the `Concept.Define("...").Property(...).Build()` sketches in the design documents are
*illustrative shape*, not API that exists. A language author reads those snippets first, so the
gap is a documentation defect today and a real deliverable in C1 - the fluent builder and the
`ed.Keyword` notation builder are what `Projectional.Toolkit` has to provide before a host is
expected to write a language against it.

What M2 delivers so a language author needs nothing else:

| Piece | Content |
| --- | --- |
| `Projectional.Toolkit` package | one reference instead of five internals; aggregates `Core`, `Language`, `Projection` and `Persistence` and exposes `LanguageSnapshot` ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)). `Editing` and `Semantics` are deliberately **not** aggregated: they are what keeps a language *authored against the toolkit* headless. A shipped language package may carry its own behaviour in `Checks/` and `Intentions/` folders and reference them ([ADR 0021](../adr/0021-one-project-per-language.md)) - the toolkit is the authoring surface, not the packaging rule |
| `dotnet new projectional-language` | `templates/projectional-language`: concepts, their notation, a sample model and two tests (the drawing, and the round trip), so a language starts testable. The round trip replaces the committed golden corpus the plan asked for: a generated project cannot carry a corpus file that was never written, and the test that asserts byte equality is the contract either way |
| Composition, not inheritance | `[.. ExpressionsLanguage.ConceptDeclarations, .. own]` - a language borrows, it does not descend |
| Golden corpus | one `.mps` document per language as the byte-stability test ([persistence.md](persistence.md)) |
| Packaging | one NuGet per language, versioned ([integration.md](integration.md#packaging)) |

## Export: declared to C#

The gap between the two paths is closed from the declared side by generating the C# declaration
from a declared language. Without it, "prototype in the editor, ship as a package" means retyping,
and retyped languages drift.

`Projectional.LanguageExport` is that. `LanguageExporter.Export(language, namespaceName, output)`
answers an `ExportResult`: `Files` is the generated `<Name>Language.cs` and its `README.md` as text,
and `Assembly` is the compiled package - present only when the caller asks for
`ExportOutput.Assembly`, because a language author reviewing a diff wants the source and a build
wants the binary, and neither should have to do the other's work. **File ▸ Export language to C#…**
is the shell's caller: it exports an opened language with the namespace `LanguageExporter.NamespaceFor`
derives from the folder name - a folder picker cannot ask for one - and writes the files and the
assembly into the folder the user chooses.

What export guarantees, and what it refuses to hide:

- Every structure, notation and designated-slot decision is emitted as the equivalent builder
  call, and so are the two behavioural aspects whose vocabulary lives in `Projectional.Language`:
  every **typing** rule (`ITypingRules` and `TypingRuleRegistry` are exactly the predicates a
  hand-written language writes, so the export writes them as the same lambdas - `text =>
  text.All(char.IsAsciiDigit)`, `text is "true" or "false"`, ...) and every **name** a `behaviour`
  folder declares, written as the record it lowers to - `nameOf: new NameReference("to").ToFunction()`
  - because the vocabulary is data, not a delegate. The output compiles against
  `Projectional.Toolkit` - the emitted assembly references `Projectional.Core` and
  `Projectional.Language` and nothing else - and passes the same registry dump, the same drawing, the
  same answers to a burst of typing and the same name for every node as the source language.
- **A behavioural aspect is stubbed, not dropped.** `types` and `generator` become a member that
  throws `NotSupportedException`, with a message naming the aspect and the folder to look in: a
  language that silently answered an empty `Types` would look exported and be wrong. `constraints`,
  `scopes`, `intentions` and `import` are named in the README alone, because what they need lives in
  an assembly the toolkit deliberately does not aggregate - `Projectional.Semantics`,
  `Projectional.Editing` and `Projectional.ImportRules` - so a stub for them would break the compile
  promise above. Both halves are listed in the exported project's README.
- **The assembly is compiled in-process** (Roslyn, against the framework's trusted assemblies and
  the two projectional assemblies), so a caller needs no SDK, no temporary project and no shell,
  and emission is deterministic: the same language twice is the same bytes.
- **What the root declares travels with the binary.** `language.mps`'s name, version and the
  languages it stands on are written as assembly attributes - `Projectional.Language`,
  `Projectional.LanguageVersion`, `Projectional.DependsOn` and `Projectional.Extends` - so a host
  reads a package's identity without opening the language, and a folder name that differs from the
  language's name cannot make the package anonymous. The `description` the root may write travels
  the same way, as `Projectional.LanguageDescription`: the name says *which* language a package
  holds, and only the author can say *what it is for*. Blank reads as nothing, so an untouched cell
  costs no attribute. The declared version becomes the assembly's
  *informational* version, the one free-form version field there is; its assembly version is left
  alone, because that field is four numbers and nothing in the framework parses a version an author
  typed.
- **The languages it stands on are exported with it.** A language ships as one package per language, so exporting a
  folder writes one source - and, on request, one assembly - for each language it was resolved against, base first:
  `UnitsLanguage.cs` and `UnitsLanguage.dll` before `InvoiceLanguage.cs` and `InvoiceLanguage.dll`, which is the order
  a consumer installs in. Each keeps its own namespace and its own metadata, and the exported language's README names
  the files beside it. A language that ships with the framework is skipped: it already is a package, and there is no
  folder of a user's to write. **File ▸ Package…** turns that answer into one zip - the sources, or the assemblies -
  so a language is handed on as a single file. The archive is reproducible: entries in export order and a fixed
  timestamp, the same rule the generated files follow.
- **Two assembly layouts, chosen at export.** `AssemblyLayout.PerLanguage` compiles one binary per language of the
  chain - the shape a package family ships in, where a consumer takes only the base it needs - and
  `AssemblyLayout.Combined` compiles the same sources into one assembly named after the exported language, the shape a
  host that embeds the whole family references. The sources are one file per language either way; the compiler takes
  syntax trees rather than concatenated text, because each file carries its own using directives and namespace. In the
  combined layout the assembly's informational version is the exported language's and each dependency keeps its own
  name and version as `AssemblyMetadata`, because one assembly can hold only one informational version. **File ▸
  Package…** offers both (`Assemblies, one per language` and `Assemblies, one for all`); two languages whose classes
  answer the same name are a compile error, which is the honest answer - one assembly cannot hold both.
- Export is one-way by design: the declared model stays the source of truth
  ([ADR 0002](../adr/0002-model-as-source-of-truth.md)) and re-export overwrites the generated
  files. Editing generated code and re-exporting loses the edit, which is stated in the header of
  every generated file.
- **The declared version is part of the file name.** A language is written as `<ClassName>-<version>.cs` and compiled
  to `<ClassName>-<version>.dll`, so two releases of one language can sit in a folder without a manifest; the class
  keeps its unversioned name, because a package's C# identity is its type name and a type renamed on every release is
  a package nobody can upgrade. A version is free text, so whatever a file name cannot hold becomes a dash, and a
  language whose root declares none keeps its bare name.

### What export still leaves to you

`types/` is written as code, and it is the last aspect that was not. The five shapes a declared rule lowers to are
public in `Projectional.Language` - `FixedTypeRule`, `ChildTypeRule`, `WidestTypeRule`, `SwitchTypeRule`,
`EquationsTypeRule` - so a language written by hand and one exported from a folder declare the same objects, and the
generated file needs no lambda. `TypeRegistry.Rules` and `SubtypeRelation.Edges` are the other direction the export
needs, exactly as `TemplateRegistry.Templates` was for generators. A named type is hoisted into a field of its own,
because the lowering interns one instance per name and two rules naming `place` mean one type.

The two statements about types that are not rules are written too: **comparable pairs** (`Comparable`) as
`ComparisonRelation.Empty.Add(...)` and **replacements** (`Replacement`) as `new TypeReplacement(claim, left, right,
[equations])`, which is why `ComparisonRelation.Pairs` is readable beside `SubtypeRelation.Edges`. A variable in a
replacement's pattern is written inline as `new TypeVariable("a")`, because a variable's identity is the instance and
each rule holds its own - hoisting it would let two rules share one, which is what the patterns must not do.

`generator/` is no longer on that list. Every part a template is made of is a record in `Projectional.Language`, so a
generator is written as the data it is: one `new TextGenerator(name, TemplateRegistry.Empty.Add(concept, ...))` per
target, with text, property, child, children (separator, gaps and tight concepts included), parent, reference, self,
line break, loop, indent, conditional and when-filled parts. The one thing that was missing was
`TemplateRegistry.Templates` - the registry answered `TemplateFor(concept)`, and an export needs the other direction -
and a test proves the result by running the emitted generator and the folder's over one document and comparing the text.
## Slices

Each slice is its own branch and its own merge. The order is chosen so that something is
shippable at the end of each, and so that the parity test exists before the second path does.

| Slice | Branch | M | Delivers |
| --- | --- | --- | --- |
| C1a ✓ | `feat/toolkit-builder` | M2 | the `Projectional.Toolkit` surface a language package needs: one reference that brings `Core`, `Language`, `Projection` and `Persistence`, plus `LanguageSnapshot` - the renderer a notation test asserts on, exposed through a friend grant rather than by opening the projection layer ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)). Proven by `tests/Projectional.Toolkit.Tests`, which references the toolkit and nothing else and declares, draws and round-trips a language. **Deliberately narrower than the slice planned**: no shipped language was re-expressed through it, because the shipped languages are packages *in this repository* that may keep their explicit references, and the template is still owed |
| C1b ✓ | `feat/language-template` | `dotnet new projectional-language`: concepts, notation, a sample model and two tests, referencing `Projectional.Toolkit` alone. Verified by installing the template, generating a project, pointing it at the toolkit (the package is not published) and running its tests. Two defects only a real run finds are recorded in the template's commit: the SDK's compile glob reaching into the sibling test folder, and `dotnet test` falling back to VSTest unless the new runner is opted in - which the template does with a `global.json` that pins no SDK |
| C2 ✓ | `test/language-parity` | M2 | the three parity tests over a declared twin of `TestLanguage` - delivered, see [language-parity.md](language-parity.md) |
| P1 ✓ | `feat/declared-structure` | M2 | `Projectional.Structures` (stage B): concepts, slots, inheritance, enumerations as nodes; lowering into `ConceptDeclaration` - delivered, see [declared-structure.md](declared-structure.md) |
| P2 ✓ | `feat/declared-editor` | M2 | `Projectional.Editors` (stage B): notation as nodes, lowering into `Notation` - delivered, see [declared-editor.md](declared-editor.md). `Editor.Table`/`Editor.Column` were added with the M2 gate, so a declared language can draw a table |
| C3 ✓ | `feat/declared-dsl-gate` | M2 | **the M2 gate**: `samples/declared-pipeline` is a DSL that exists only as data - a `structure` folder, an `editor` folder and a document - and `DeclaredSampleLanguageTests` loads the folder, draws the document as a table, edits it structurally, saves it and reloads it byte-identically, and hands the folder to the shell's own `WorkbenchLanguage`. There is no C# twin to delete, which is the strongest form of the gate's "deleting the language's C# code changes nothing" |
| P2b ✓ | `feat/mps-shaped-notation` | M2 | both definition languages drawn in MPS' shape - a header, a block of options, one section per kind of slot, `<< ... >>` for an empty section, `[/ ... /]` and `[>` for the cell model. **Ahead of P4/P5 in the one part that is only notation**; what P5 still adds are the cell *glyphs* |
| P3 ◐ | `feat/language-project-root` | M5 | the MPS shape: the project folder and its `language.mps` root - **the loader, the root and its resolution are delivered** ([language-projects.md](language-projects.md)). The root is one `Structure.Language` document at the project root: it names the language, carries the version the author spells and lists the dependencies and extended languages, drawn as a header and two sections like any other document. A host-supplied resolver answers those names and the language's own registry is built over the bases' declarations, transitively and base before dependent ([ADR 0026](../adr/0026-a-dependency-resolves-to-the-vocabulary.md)). **Behaviour is inherited through `extends`** ([ADR 0027](../adr/0027-behaviour-is-inherited-through-extends.md)): an extended language's constraints, scopes, types, typing rules and intentions run in a dependent's document, its own statement first and its nearest base next - the answer to whose invariants a shared concept carries |
| P4 ✓ | `feat/structure-notation` | M5 | the rest of `Projectional.Structures`' own notation, delivered: the header carries the inheritance and draws a list only while it is filled (`concept X extends A`, `concept X` when it has neither), one section per kind of slot with blank lines between the groups, one row syntax for all three sections (`name : type`, `name : Concept[range]`, `name : Concept`), every collection stacks so a definition is one declaration per line, an empty section says `<< ... >>` where its rows would stand, and the four cardinality ranges reach `ChildSlot.Minimum` (open question 64) - see [declared-structure.md](declared-structure.md). The hiding needed a cell the notation layer did not have (`WhenFilledNotationCell`, `Editor.WhileFilled`) and the way back in (`Inherit from…`, `Implement interface…`); the braces, the per-kind placeholders and the `<<property …>>` markers were dropped later, when the shape was compared against MPS' |
| P5 ✓ | `feat/editor-notation` | M5 | `Projectional.Editors` drawn in its own glyphs: `-word-`, `%slot%`, `( slot )`, `{ slot }`, `< slot >`, `;`, `[/ ... /]`, `[> ... <]`, `[ ... ]`, `[\| ... \|]` - delivered, see [declared-editor.md](declared-editor.md). A line keeps its brackets beside its cells, which needed the one cell the notation layer was missing (`HorizontalNotationCell`/`Editor.Horizontal`); every string a cell carries (an `emptyText`, a `spacing`, a `separator`) stays editable, and the sample definition is drawn in the test that pins it |
| P6 ✓ | `feat/language-export` | M5 | declared to C# export - delivered, see [Export](#export-declared-to-c): the concepts as declaration calls, the notation as `ed.*` calls, a README naming every aspect the export cannot write, and an option that compiles the source into the assembly a package ships (`ExportOutput.Assembly`). Proven by `LanguageExporterTests`, which loads the emitted assembly back and compares its registry dump and drawing with the declared folder's |
| P7 | `feat/bootstrap-selfhost` | M5 | the new languages define themselves: the bootstrap corpus is the definition of `Projectional.Structures` and `Projectional.Editors`, with the C# declarations as the fail-safe |

C1-C2 come first and are not optional: without the builder there is no surface to lower *into*,
and without the parity tests the second path has nothing checking it. P1-P2 are the M2 meaning
of "a language without C#", and they are where the generic renderer stops being the only
renderer. P3-P5 are M5, and P4-P5 are the point at which the definition language looks like MPS
rather than like data - both done, which is why P6 exports the finished shape rather than a
provisional one. P7 is the self-hosting proof, and the last place a bootstrap-shaped shortcut can
hide.

## Testing

- **Parity, headless.** The three comparisons above, over a corpus of one document per core
  language, run in `Projectional.Core.Tests` beside the existing notation tests.
- **Lowering, headless.** A declared language is loaded and its registry dump compared against a
  hand-written expectation, so a lowering bug names the concept that lowered wrongly.
- **Round trip.** A declared language saves, loads and saves byte-identically, so a definition is
  a project file like any other model ([persistence.md](persistence.md)).
- **The definition language is projected.** The structure and editor models are rendered with
  `LayoutSnapshot` and asserted as text, exactly like a user DSL - if the definition language
  needs a rendering exception, it is not a real language yet.
- **Bootstrap fail-safe.** Loading a deliberately broken bootstrap leaves the workbench running
  on the C# declarations and reports the problem ([ADR 0008](../adr/0008-bootstrap-order.md)).
- **Export.** The generated source is compiled in-process and loaded back, so the registry dump and
  the drawing of the exported language are compared against the declared folder's. An aspect the
  export cannot write is asserted to be either a throwing stub or a README row, so a new
  behavioural aspect cannot silently disappear from the export.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| A text syntax for language definitions | Reintroduces a parser and a grammar toolchain, contradicting [ADR 0002](../adr/0002-model-as-source-of-truth.md) |
| Declared interpretation | Evaluation runs host code; declaring it would require the sandbox we have not built |
| Editing generated C# and re-importing it | Two sources of truth for one language; export is one-way |
| A visual debugger for aspects | Valuable at M6, needs the trace infrastructure from the interpreter first |
| MPS' `smodel` queries inside aspect blocks | Our aspect blocks are closed rule shapes; anything else is C# |
| Sharing a sub-language across languages by inheritance | Composition is by declaration list, not by descent ([language.md](language.md#built-in-languages-to-ship)) |

## Open questions

Rows 61-64 in [open-questions.md](../open-questions.md#language-authoring) are the parts of this
document that are choices rather than mechanics.
