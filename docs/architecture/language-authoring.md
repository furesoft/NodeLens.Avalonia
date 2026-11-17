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
  language.mps           the language root: name, version, dependencies, extended languages
  structure.mps          concepts, slots, inheritance, enumerations
  editor.mps             notation, one editor per concept
  constraints.mps        validation rules                    (M6)
  typesystem.mps         type and subtype rules              (M6)
  generator.mps          generation rules                     (M6)
  intentions.mps         actions offered at the caret        (M6)
  textGen.mps            text output rules                    (M6)
  accessories.mps        completion, keymap, node factories   (M6)
```

One model per aspect, because the aspects are edited at different times by different concerns
and a language definition that mixes "what a rule is" with "what colour its keyword is" cannot
be reviewed. The `language` root is the only model every language has; the rest appear when the
language has something to say in them ([persistence.md](persistence.md) for the on-disk layout).

The file names are MPS' (`structure.mps`, `editor.mps`, ...), and so is the split, because the
alternative - one file with every aspect as a section - loses exactly the property that makes MPS
languages readable: you can open the structure model and see the shape of the domain without any
presentation.

## How a concept reads

The structure model is written in `Projectional.Structures`, and it reads like MPS' structure
language because it is the same three ideas: a header that says what the concept *is*, angle
bracket blocks that say what it *holds*, and nothing else.

```text
concept RuleSet
  <<property name : string>>
  <<child rules : Rule[0..n]>>
  <<shortDescription : a named set of rules>>

abstract concept Condition
  <<property negated : boolean>>

concept Comparison extends Condition implements Named
  <<reference left : Value>>
  <<reference right : Value>>
  <<property operator : Operator>>

enum Operator
  Less
  Equal
  Greater
```

The vocabulary, and what each piece lowers to:

| Written | Lowers to | MPS equivalent |
| --- | --- | --- |
| `concept X`, `abstract concept X` | `ConceptDeclaration(key, isAbstract)` | `concept` / `abstract concept` |
| `interface concept X` | an abstract concept with no slots of its own - a spelling, not a second kind | `interface concept` |
| `concept X extends Y` | the first entry of `superConcepts` | `extends` |
| `concept X implements Y`, `Z` | the remaining entries of `superConcepts`, in written order | `implements` |
| `<<property name : string>>` | a property slot plus its `PropertyType` | `<<property>>` |
| `<<child steps : Step[0..n]>>` | a child slot plus its cardinality | `<<child>>` |
| `<<reference target : Rule[0..1]>>` | a reference slot plus its cardinality | `<<reference>>` |
| `<<shortDescription : ...>>` | the concept's documentation ([concept-descriptions.md](concept-descriptions.md); lowered to `ConceptDeclaration(description:)`) | `<<shortDescription>>` |
| `<<nameSlot : name>>`, `<<collapseSlot : visible>>` | a designated slot ([language.md](language.md#the-two-designated-slots)) | none - MPS has no analogue |
| `<<offeredInCompletion : false>>` | `isOfferedInCompletion` | `<<hideInEditor>>`-style flags |

Three details are deliberate:

- **Slot blocks carry the type, not the notation.** A slot is storage; what it looks like is the
  editor model's business. Mixing the two is how MPS beginners produce concepts whose notation
  cannot be reused.
- **The cardinality is written in brackets, always.** `[1]` for required single, `[0..1]` for
  optional, `[0..n]` and `[1..n]` for lists. A default of "you did not say" would be a fourth
  meaning the reader has to learn.
- **Inheritance is in the header; designated slots are in the body.** The header answers "what is
  this", the body answers "what does it hold and how does the framework treat it".

One honesty note about the brackets: `Cardinality` today has two values, `One` and `Many`, so
`[0..1]` and `[1]` currently lower to the same thing - the minimum is written, checked against
the syntax, and then dropped. That is fine for structure (`Many` is the only distinction the
model makes) but it cannot stay: a required child that a model may omit is exactly the kind of
finding a constraint should not have to rediscover. Whether the minimum belongs on
`Cardinality` or on a separate slot property is open question 64, and it is decided before P4
writes the structure notation.

## How an editor reads

The editor model is written in `Projectional.Editor`. A concept editor is a nested cell tree
drawn with brackets and markers, which is what makes MPS notation editable as text-like code
rather than through a wizard:

```text
concept editor RuleSet
  [> -ruleset- %name% -{-
       { rules }
     -}- ]

concept editor Comparison
  [> %left% -(%operator%) -%- %right% <]
```

The glyph set, and the cell each one lowers to:

| Written | Lowers to | Reading |
| --- | --- | --- |
| `-keyword-` | `ConstantCell` | a fixed token, not editable |
| `%name%` | `PropertyCell` | the value of one property |
| `{ slot }` | `CollectionCell` | a many-valued slot, separator from the notation |
| `( slot )` | `SingleChildCell` | the one child's own notation, inline |
| `< slot >` | `ReferenceCell` | the target's name |
| `;` | `SpaceCell` | a gap in the gap's own address |
| `[> ... <]` | `IndentCell` | a block, one level deeper |
| `[ ... ]` | `LineCell` | one line, inline cells only |

The bracket letters are the notation, so a misplaced cell fails when the editor model is
validated, exactly as `ed.Line(ed.Indent(...))` fails to compile in C#
([language.md](language.md#notation)). Variants are a suffix on the block - `concept editor
RuleSet : table` - because a variant is notation for the same concept, not a second concept.

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
  framework change.
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
| scopes | chain description plus per-slot overrides | M6 | MPS needs a language for this too; the default chain covers most DSLs |
| generation | template rules with traceability | M6 | the largest declarative win, and the largest piece of work |
| intentions, textGen, accessories | block per concept | M6 | declarative by nature; these are where DSL authors spend their time |
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
languages for features this project does not have (data flow, find usages, checking rules).

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
| `Projectional.Toolkit` package | one reference instead of five internals; aggregates `Core`, `Language`, `Projection` and `Persistence` and exposes `LanguageSnapshot` ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)). `Editing` and `Semantics` are deliberately **not** aggregated: they are what keeps a language headless, and a language that needs behaviour ships a second package beside it |
| `dotnet new projectional-language` | a template with a concept, its notation, a fixture model and a golden corpus, so a language starts testable |
| Composition, not inheritance | `[.. ExpressionsLanguage.ConceptDeclarations, .. own]` - a language borrows, it does not descend |
| Golden corpus | one `.mps` document per language as the byte-stability test ([persistence.md](persistence.md)) |
| Packaging | one NuGet per language, versioned ([integration.md](integration.md#packaging)) |

## Export: declared to C#

The gap between the two paths is closed from the workbench side by generating the C#
declaration from a declared language. Without it, "prototype in the editor, ship as a package"
means retyping, and retyped languages drift.

What export guarantees, and what it refuses to hide:

- Every structure, notation and designated-slot decision is emitted as the equivalent builder
  call. The output compiles against `Projectional.Toolkit` and passes the same registry dump as
  the source language.
- **A behavioural aspect is emitted as an explicit stub** - the aspect is set and throws with a
  message naming the concept and the aspect - because a language that silently lost its
  constraint would look exported and be wrong. The parity test of an exported language therefore
  compares declarations only, and the stubs are listed in the exported project's README.
- Export is one-way by design: the declared model stays the source of truth
  ([ADR 0002](../adr/0002-model-as-source-of-truth.md)) and re-export overwrites the generated
  files. Editing generated code and re-exporting loses the edit, which is stated in the header of
  every generated file.

## Slices

Each slice is its own branch and its own merge. The order is chosen so that something is
shippable at the end of each, and so that the parity test exists before the second path does.

| Slice | Branch | M | Delivers |
| --- | --- | --- | --- |
| C1a ✓ | `feat/toolkit-builder` | M2 | the `Projectional.Toolkit` surface a language package needs: one reference that brings `Core`, `Language`, `Projection` and `Persistence`, plus `LanguageSnapshot` - the renderer a notation test asserts on, exposed through a friend grant rather than by opening the projection layer ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)). Proven by `tests/Projectional.Toolkit.Tests`, which references the toolkit and nothing else and declares, draws and round-trips a language. **Deliberately narrower than the slice planned**: no shipped language was re-expressed through it, because the shipped languages are packages *in this repository* that may keep their explicit references, and the template is still owed |
| C1b | `feat/language-template` | M2 | `dotnet new projectional-language`: a concept, its notation, a fixture model and a golden corpus, so a language starts testable. What is left of C1 |
| C2 ✓ | `test/language-parity` | M2 | the three parity tests over a declared twin of `TestLanguage` - delivered, see [language-parity.md](language-parity.md) |
| P1 ✓ | `feat/declared-structure` | M2 | `Projectional.Structures` (stage B): concepts, slots, inheritance, enumerations as nodes; lowering into `ConceptDeclaration` - delivered, see [declared-structure.md](declared-structure.md) |
| P2 ✓ | `feat/declared-editor` | M2 | `Projectional.Editors` (stage B): notation as nodes, lowering into `Notation` - delivered, see [declared-editor.md](declared-editor.md) |
| P2b ✓ | `feat/mps-shaped-notation` | M2 | both definition languages drawn in MPS' shape - a header, a block of options, one section per kind of slot, `<< ... >>` for an empty section, `[/ ... /]` and `[>` for the cell model. **Ahead of P4/P5 in the one part that is only notation**; what P4/P5 still add are the cell *glyphs* and the file layout |
| P3 | `feat/definition-languages` | M5 | the MPS shape: `language.mps` root, one model per aspect, the project folder, dependencies and extended languages - the **loader** for such a folder is delivered (`feat/language-project`, [language-projects.md](language-projects.md)) |
| P4 | `feat/structure-notation` | M5 | the rest of `Projectional.Structures`' own notation: `<<property>>`-style slot markers where a section heading is not enough, conditional cells, and the file layout P3 needs |
| P5 | `feat/editor-notation` | M5 | `Projectional.Editor` with its own notation - the cell glyphs - and the table variant |
| P6 | `feat/language-export` | M5 | declared to C# export, with the stub policy and the exported-project corpus |
| P7 | `feat/bootstrap-selfhost` | M5 | the new languages define themselves: the bootstrap corpus is the definition of `Projectional.Structures` and `Projectional.Editors`, with the C# declarations as the fail-safe |

C1-C2 come first and are not optional: without the builder there is no surface to lower *into*,
and without the parity tests the second path has nothing checking it. P1-P2 are the M2 meaning
of "a language without C#", and they are where the generic renderer stops being the only
renderer. P3-P5 are M5, and P4-P5 are the point at which the definition language looks like MPS
rather than like data. P6 can happen any time after P2; it is scheduled after P5 so that it
exports the finished shape rather than a provisional one. P7 is the self-hosting proof, and the
last place a bootstrap-shaped shortcut can hide.

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
- **Export.** The generated project compiles and its registry dump matches the source language;
  stubs are counted, so a new behavioural aspect cannot silently disappear from the export.

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
