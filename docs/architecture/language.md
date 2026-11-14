# Language definition

A language describes three things: which concepts exist (**structure**), how they
appear and behave (**notation/editor**), and what they mean (**aspects**: constraints,
types, scopes, generation, interpretation).

## Concept

```csharp
Concept.Define("Rules.RuleSet")
    .Property("name", PropertyType.String)
    .Children("rules", ChildCardinality.ZeroOrMore, ofConcept: "Rules.Rule")
    .Reference("defaultOutcome", ofConcept: "Rules.Action", cardinality: ReferenceCardinality.ZeroOrOne)
    .Build();
```

A concept declares its slots, its superconcept(s), whether it is abstract, and the
aspect implementations. Concept keys are strings of the form `Language.Name`, the same
form used in XML and in the model API. There is no separate namespace mechanism — the
prefix *is* the namespace, and it keeps XML readable.

Superconcepts affect:

- which slots a node may have (inherited union),
- which concepts are accepted where a superconcept is expected (subtyping),
- which aspect applies (most specific implementation wins).

## Notation

Notation is a tree of **editor cells**. A cell is one of:

| Cell | Renders | Edit behaviour |
| --- | --- | --- |
| Constant | a keyword/punctuation string | not editable, participates in substitution menus |
| Property | the property value | free text input, parsed to the property type |
| Single child | the one child's notation, drawn where the cell sits | newline/indent semantics, inserts siblings |
| Children | an indented list of child notations | |
| Reference | the target's name, with a chooser | typed text resolves through scopes |
| Collection | a child list with a separator cell between children | |
| Table/Grid | rows (children) × columns (fixed or per-row roles) | cell-level caret |
| Line, Sequence, Indent, Space | layout modifiers | |

The layout cells are the skeleton, and they come in two shapes that cannot be
mixed up. A **line** runs its inline cells horizontally and is the unit of vertical
layout; a **sequence** stacks blocks vertically unchanged; an **indent** stacks
blocks one level deeper; a **space** separates two inline cells. Only inline cells
(constants, properties, references, single children, spaces) may sit in a line, and
only blocks (lines, sequences, indents, children, collections) may sit in a sequence
or an indent. That split is expressed in the type hierarchy, so a notation that
misplaces a cell fails to compile rather than at startup.

Empty slots are never invisible: a slot-bearing cell carries the text it shows while
it is empty (`<empty>`, `<none>`). The caret always needs something visible to
attach to, and an empty list still has to be fillable.

```csharp
notation.Add("Sample.Ruleset", ed => ed.Sequence(
    ed.Line(
        ed.Keyword("ruleset"), ed.Space, ed.Property("name"),
        ed.Space, ed.Punct("{")),
    ed.Indent(ed.Children("rules")),
    ed.Line(ed.Punct("}"))));
```

Notation is **data, not code**, even in the built-in stage: it is a declarative
structure that the projection engine walks. That is what allows stage C to edit the
notation itself inside the editor.

Notation is validated once, when it is registered: every cell is checked against the
concept registry, so the projection engine can assume a cell only ever names a slot
that exists. A concept without its own notation inherits the nearest one in its
super-concept chain, which is how a language reuses a notation it only wants to
extend.

Language authors never write layout code. If a notation needs something the cell set
cannot express, the cell set is incomplete — adding a cell primitive is a framework
change, deliberately heavier than a per-language hack.

### Styles

Colours, fonts and borders come from a style sheet keyed by cell kind and concept
tag. Hard-coded visual properties in notation are rejected in review: they prevent
theming, dark mode and accessibility.

## Aspects

Aspects are the semantic hooks. All are optional per concept, all are delegates
registered without reflection ([ADR 0007](../adr/0007-reflection-free-language-definition.md)).

| Aspect | Signature (shape) | Purpose |
| --- | --- | --- |
| `Validate` | `node, scope → diagnostics` | constraint checks, error markers |
| `TypeOf` | `node, typeContext → IType` | compile-time type |
| `ScopeProvider` | `reference role, node → IScope` | what a reference may point to |
| `Completion` | `cell, prefix, node → candidates` | suggestions in a menu |
| `Substitute` | `caret, node → actions` | transformation/wrapping menus |
| `Evaluate` | `node, evalContext → value` | interpreter |
| `GenerateText` | `node, textGenContext → text` | text generation |
| `Icon`, `Label` | `node → visual` | lists, completion, navigation |
| `Migration` | `oldVersion, node → node` | language upgrade |

Representative shapes:

```csharp
concept.Validate((node, ctx) =>
{
    if (node.Children("rules").Count == 0)
        return ctx.Error(node, "A ruleset needs at least one rule.");
    return ctx.Ok;
});

concept.TypeOf((node, ctx) => BuiltinTypes.Boolean);
```

The constraint language starts as C#. Making it a *declared* language later (M6) is
additive: a declared constraint is lowered to the same `Validate` delegate, so no
aspect signature changes.

## Scope model

Reference resolution walks a chain of scopes, first match wins:

```text
local (siblings, own children)
  → enclosing ancestors' scopes      (lexical, like a block)
  → model scope                      (all roots of the current model)
  → imported models                  (explicit imports, ordered, no cycles)
  → global/root scope                (computed, e.g. all models in the project)
```

Every step is provided by an aspect, with a default implementation. A language can
replace any step. Ambigous results are diagnostics, not silent picks.

The scope chain is also what completion lists are built from — one implementation,
two consumers.

## Languages come in two flavours

Both implement the same `ILanguage`; nothing else in the system distinguishes them.

| Flavour | Source | Loaded by | Used for |
| --- | --- | --- | --- |
| **Built-in** | C# fluent declarations, compiled into an assembly | assembly load | bootstrap languages, performance-critical languages |
| **Declared** | concept/notation nodes inside the project | reading project XML | DSLs authored in the workbench |

## Bootstrap stages

Self-hosting is a cycle: to define languages in the editor you need an editor for the
language-definition language. Resolved the same way MPS resolves it — by bootstrapping
a subset in code first, then describing the rest with itself.

| Stage | What exists | What the user can do | Milestone |
| --- | --- | --- | --- |
| **A** | Built-in languages hardcoded in C#, generic renderer | edit models of the built-in languages | M1 |
| **B** | Concepts + notation as declared data | add a new language without writing C# | M2 |
| **C** | The structure/editor language is itself a declared language, bootstrap XML shipped | edit language definitions in the editor, see live result | M5 |

The bootstrap XML is versioned with the code and migrated like any user data. It is
the most sensitive artefact in the repo: if it breaks, the workbench cannot load its
own languages.

## Language versions and compatibility

Each language has a version number. A model records the version of every language it
was written against.

- Adding an optional slot or a concept: no version bump needed, old models stay valid.
- Renaming a slot, changing cardinality, removing a concept, or changing meaning:
  version bump plus a migration.
- Migrations are per-step (`3 → 4`), chained on load, run before constraints.
- Loading a model written by a *newer* language version fails loudly rather than
  silently dropping slots.

## Built-in languages to ship

| Language | Purpose |
| --- | --- |
| `Projectional.Structure` | concepts, slots, superconcepts (stage C) |
| `Projectional.Editor` | notation cells, styles (stage C) |
| `Projectional.Constraints` | declarative validation rules (M6) |
| `Projectional.Types` | type rules when they outgrow C# (M6) |
| `Projectional.Migrations` | migration rules when they outgrow C# (M6) |
| `Projectional.Expressions` | shared expression/name sub-language used by most DSLs |

`Projectional.Expressions` is not a convenience: nearly every DSL needs an identifier
and a boolean expression, and duplicating them per DSL is the most common way these
frameworks become unusable.
