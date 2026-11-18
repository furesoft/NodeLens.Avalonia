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

A concept is also either **concrete** or **abstract**. An abstract concept exists to name
a set — it is what a slot is declared over when the language does not care which member
of the set stands there — and no node can be an instance of it; the model rejects one at
the single place where nodes are created, so no code path can produce one. Two
consequences follow, and the editor depends on both:

- An empty slot declared over an abstract concept is filled with the first concrete
  concept below it in declaration order. Declaration order is therefore the language
  author's answer to "what will the user most likely want here", and it is the only place
  that preference is expressed.
- An abstract concept may have no notation, because nothing is ever projected through it.
  Every concrete concept needs one, either its own or an inherited one.

Independently of that, a concept declares whether it is **offered in completion**
(`ConceptDeclaration(..., isOfferedInCompletion: false)`). The flag answers one question
only — "should this concept appear in the list a user picks from" — and is *not* a
statement about legality: a hidden concept is still a legal child of every slot that
accepts it, and it may still be what an empty slot is filled with by pressing Enter. That
separation is deliberate. `Expressions` hides `IntLiteral`, `BoolLiteral` and
`StringLiteral` because a literal is created to be typed into, not picked out of a list of
four — while keeping them reachable, since a slot declared over the abstract `Expression`
would otherwise have no way to produce a literal at all. The flag is per declaration and,
like `isAbstract`, is not inherited: a language whose literals descend from a shared base
repeats it on each one. See [completion.md](completion.md) for the query that applies it.

## Concept descriptions

A concept may declare one sentence about itself, which the editor shows on hover over a node of
that concept and beside the completion entry that offers it
([concept-descriptions.md](concept-descriptions.md),
[ADR 0018](../adr/0018-concept-description-is-language-data.md)):

```csharp
new ConceptDeclaration(
    "Rules.Rule",
    slots: [...],
    nameSlot: RulesLanguage.NameSlot,
    description: "One rule of a ruleset: a name, an optional condition and an outcome.")
```

The text is **language data, not model data and not a designated slot**: it is not persisted per
node, creates no undo step, and is not inherited - a super concept's sentence describes the super
concept, not the node the user is looking at. It is trimmed at declaration and an empty one becomes
`null`, so "declared nothing" and "declared a blank" are the same state and a language cannot ship
a tip that shows nothing. `null` is also the default, so no existing language changes.

A **slot** carries a sentence the same way (`PropertySlot.description`), shown over the cell while the slot is
empty and - when the slot also declares `showDescriptionWhenFilled: true` - over a filled value too. The flag is
the language author's answer to "this text does not explain itself": a key the notation never spells out, an
operator symbol, a flag whose two words are the whole meaning. It is off by default, and asking for it without a
description is refused at declaration. The hover and the built-in slots are
[concept-descriptions.md](concept-descriptions.md)'s business.

## The two designated slots

A concept may nominate one of its own properties as carrying a framework-wide meaning. There
are two such nominations today, and they work identically:

| Designation | Parameter | Meaning |
| --- | --- | --- |
| name slot | `nameSlot: "name"` | the property references display instead of `<Concept.Key>` |
| collapse slot | `collapseSlot: "collapsed"` | whether a node of this concept may be folded in the editor ([collapsing.md](collapsing.md), planned) |

The rules are the same for both, and they are checked once at registration rather than trusted:

- **The named slot must be declared and must be the right type** — a string for the name slot, a
  boolean for the collapse slot. A missing slot or the wrong type is a
  `ConceptDefinitionException`, thrown where the language author still sees the message.
- **The designation inherits.** A concept that declares none takes the first of its super
  concepts, in declaration order, that declares one — so a subconcept is named, and foldable, by
  default, and a language re-declares the slot only to override that.
- **The name slot is read from the model; the collapse slot is a *value* its own concept reads.**
  Nothing in the framework tests "was it set" — property defaults are materialised, so an unset
  `bool` defaulting to `true` reads as `true`.

Nominating a slot is model data, so it is language design, not a preference: a document cannot
change it, and a model saved before the nomination existed loads unchanged.

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
| Conditional | its inline cells, or nothing at all | as its content |
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
it is empty (`<empty>`, `<none>`, or for a property `<<operator>>`). The caret always needs something visible to
attach to, and an empty list still has to be fillable.

**A conditional cell is the one thing that is not there.** `ed.Conditional("isAbstract", "true",
ed.Keyword("abstract"), ed.Space)` draws its cells only while the node's `isAbstract` holds `"true"`, and
otherwise draws nothing - not an empty cell, which would be a caret stop pointing at nothing. That is what
lets one notation write `abstract concept Rule` for an abstract concept and `concept Rule` for a concrete
one, the way MPS does.

- **The condition is a property and a value, never a predicate.** A delegate could not be written down in a
  notation model, so a language declared as data would silently become weaker than one written in C# -
  the equality ADR 0014 is built on. Comparing a property's stored text is the smallest form that covers
  what notations actually need: a flag that turns a word on.
- **The value is validated at registration** against the property's type, so `"yes"` on a `bool` is a
  startup error rather than a cell that can never appear.
- **The gap belongs inside the conditional.** `ed.Conditional(..., ed.Keyword("abstract"), ed.Space)`, not
  `ed.Conditional(...), ed.Space` - a space outside it stays behind as a leading blank in every drawing
  where the word is gone.
- Inline only, for now. A block conditional is the same idea one level up and arrives when a notation needs
  to hide a whole line rather than a word.

```csharp
notation.Add("Sample.Ruleset", ed => ed.Sequence(
    ed.Line(
        ed.Keyword("ruleset"), ed.Space, ed.Property("name"),
        ed.Space, ed.Punct("{")),
    ed.Indent(ed.Children("rules", stacked: true)),
    ed.Line(ed.Punct("}"))));
```

A child slot is one line by default, because most slots hold a handful of words. The
`stacked` flag asks for the other reading - one child per line, which is what a block language
wants for its list of rules - and it is a property of the notation rather than of the model, so
the same concept can be published as a row of items in one language and a column in another.

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

### Variants

A concept may be drawn more than one way — a ruleset as a block, the same ruleset as a
table — so notation is registered per concept *and* variant:

```csharp
notation.Add("Sample.Ruleset", ed => /* the block form */);              // the default
notation.Add("Sample.Ruleset", "table", ed => /* the same concept as rows */);
```

Only one of them is drawn at a time. Which one is **view state**, decided when the
document is projected and never written into the model: two sessions over one document
may read it differently and still edit the same nodes. A variant resolves up the
super-concept chain like any other notation, so a language that draws its abstract base
as a table draws its refinements as a table too, and a variant is free to hide slots the
default shows.

The choice travels as `INotationChoice` / `NotationChoice`, which `EditorSession` carries
as `Choice` and answers through `Alternatives()` and `ChooseVariant()`. Every slot a
variant does show keeps the address the default would have given it, so switching
representation cannot invalidate the caret, a selection or an undo step — a test that
pins a caret address passes in both forms.

The table variant is where this pays off first: `ed.Table(slot, Column(…), …)` draws the
children of a slot as rows, one cell per column, with header cells belonging to the table
rather than to the row. See [projection.md](projection.md#tables) for how the shape is
projected, laid out and styled.

Notation is validated at registration, so a table with a column that names no slot, names
a slot of the wrong concept, or names a many-valued slot is refused when the language is
built rather than when a document is opened.

See [ADR 0012](../adr/0012-concept-rendered-more-than-one-way.md).

### Styles

Colours, fonts and borders come from a style sheet keyed by cell kind and concept
tag. Hard-coded visual properties in notation are rejected in review: they prevent
theming, dark mode and accessibility.

## Typing rules

Notation says what a concept looks like; a typing rule says what the user is *describing*
when they start typing into an empty slot. It is a language declaration for the same reason
the notation is one: it is a statement about how this language is written, and no amount of
inspecting the concepts can recover it. A slot declared over `Expression` holds an abstract
set, and `abc` is a valid value of both `Name` and `StringLiteral` — only the author knows that
a bare word is a name and a quoted one is a literal.

```csharp
public static ITypingRules TypingRules { get; } = TypingRuleRegistry.Empty
    .Add(ExpressionsConceptKeys.IntLiteral, text => text.All(char.IsAsciiDigit), "value")
    .Add(ExpressionsConceptKeys.BoolLiteral, text => text is "true" or "false", "value")
    .Add(ExpressionsConceptKeys.StringLiteral, text => text.StartsWith('"'), "value")
    .Add(ExpressionsConceptKeys.Name, ClaimsName, "name")
    .Add(ExpressionsConceptKeys.Parenthesized, text => text.StartsWith('('));
```

Three things about the shape are deliberate:

- **The predicate sees the whole burst of typed text, not one keystroke.** `true` is a single
  word no character announces on its own, so a per-character question could never answer it.
- **The property slot is stated, not searched.** Several properties of one concept can accept
  the same text, and which one the characters belong in is the author's decision.
- **The slot is optional.** A rule with no property creates the concept and stores no text: a
  parenthesis is a gesture that selects `Parenthesized`, not a character the model keeps.

`TypingRuleRegistry` is immutable — `Add` returns a new registry and leaves the one it was
called on alone — and refuses a second rule for the same concept, because two answers to one
question is a language bug that would otherwise surface as "sometimes the wrong concept
appears". `ITypingRules.ClaimFor(conceptKey, text)` is the only lookup; a concept that declares
no rule claims nothing.

The rules live in `Projectional.Language` beside `ICellEmphasis`, not in the editing layer, so a
language package can declare them without referencing the editor. What the editor does with a
claim is [editing.md](editing.md#typing-into-an-empty-slot).

## Triggers

A typing rule answers for an **empty** slot. The neighbour question - the caret is at the edge of a node that is
already there, and the character typed says what should surround it - is a *trigger*. It is declared **on the
concept it creates**, because every slot it names belongs to that concept and because a language borrows another's
concepts as declarations:

```csharp
new ConceptDeclaration(
    ExpressionsConceptKeys.Binary,
    [
        new PropertySlot("operator", ExpressionOperators.BinaryType),
        new ChildSlot("left", ExpressionsConceptKeys.Expression, Cardinality.One),
        new ChildSlot("right", ExpressionsConceptKeys.Expression, Cardinality.One),
    ],
    [ExpressionsConceptKeys.Expression],
    triggers:
    [
        new ConceptTrigger(
            ExpressionsConceptKeys.Expression,
            TriggerSide.Right,
            text => ExpressionOperators.Binary.Contains(text),
            anchorSlot: "left",
            propertySlot: "operator",
            caretSlot: "right"),
    ]);
```

A trigger names the anchor concept it applies to, the side of the anchor the caret must stand at, the slot of this
concept that receives the node, and - both optional - where the typed text goes and where the caret continues. It
does **not** name the concept to create: that is the concept declaring it. `ConceptRegistry.TriggersFor(Concept)`
answers in concept declaration order, which is the only tie-break where two concepts claim the same character, and
a subtype does not inherit the gesture (it would create the super concept).

The two optional slots exist for the two kinds of gesture. A gesture whose text *is* the value names `propertySlot`
and is done in one keystroke. A gesture that is **spelt** - `and` for `&&` - names no property and instead carries
`presets`, and the run of keystrokes that spells it is buffered by the editing layer, which is why `ConceptTrigger`
needs no notion of time. [triggers.md](triggers.md#spelling-a-word) covers the buffer.

The rule that decides everything: **text a property accepts is never given to a trigger.** `-` in front of `5`
writes `-5`, because a negative number is a number; only a refused keystroke asks the triggers.
[triggers.md](triggers.md) is the design of record, including the caret-to-anchor rules, the checks that turn a bad
declaration into a language error at load, and the limits.

## Applicability

A child slot says which *kind* of thing it holds, and it says the same thing wherever it appears. Some concepts
are not valid everywhere their type is accepted: a C# attribute may stand on a class, a method or a parameter
and nowhere else, and an XML comment only in front of a class or a method definition. Where such a concept must
not stand, the editor has to stop offering it - a completion list that proposes something the language forbids
teaches the user a mistake.

So the constraint is declared on the concept, as a list of places:

```csharp
new ConceptDeclaration(
    CSharpConceptKeys.Attribute,
    [new PropertySlot("name", PropertyType.String)],
    applicability: Applicability.Only(
        new ConceptPlace(CSharpConceptKeys.Class, "attributes"),
        new ConceptPlace(CSharpConceptKeys.Method, "attributes"),
        new ConceptPlace(CSharpConceptKeys.Parameter, "attributes")));
```

A `ConceptPlace` names a concept and, optionally, one slot of it. Naming a **super concept** is how "anywhere
inside a declaration" is written without listing every declaration: `new ConceptPlace(CSharpConceptKeys.Declaration)`
allows the concept in any slot of anything that is a declaration. `Applicability.Anywhere` is the default, so a
language that constrains nothing is written exactly as before.

It is consulted in three places and **enforced in one**:

| Where | What it does |
| --- | --- |
| `CompletionSource.Proposals` | does not offer a concept that may not stand in this slot |
| `InsertChildAction` (Enter) | skips an inapplicable default and takes the next candidate |
| `TypeTextAction` (typing rules) | skips an inapplicable claimant |
| `Model` (`InsertChild`, `InsertChildAt`, `InsertRootAt`) | **refuses the placement** with `ModelInvariantException` |

The last row is the guarantee and the first three are a courtesy: a bug in a filter can never produce a document
the language does not allow, because every placement - a new node, a move, a wrap, a load, an undo replay - goes
through one of those three primitives. A concept that names places can therefore never be a model root: a root
has no parent and no slot, so none of its places can match. That follows from the rule rather than being a
special case.

`ConceptRegistry.Create` validates every place at registration - the concept must exist, a named slot must hold
children, and that slot must accept this concept - so a typo is a language error at load rather than a keystroke
that silently does nothing.

**It is not `isOfferedInCompletion`.** That flag silences the list and nothing else, because hiding a literal
from a menu must not make it impossible to create; this one is about legality, so it holds everywhere. The two
must not be merged: `CompletionSource.Proposals` filters on both, and the default fill on applicability only.

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

Two of these are implemented, and both took a shape of their own: a constraint is an
`IConstraint` (`Projectional.Semantics/Constraints/`) and a type rule is an `ITypeRule`
(`Projectional.Language/Types/`, §Types in [semantics.md](semantics.md#types)).
**Where an aspect lives is decided by what it needs.** One that only answers a question
about a node belongs to the language, so the language package declaring the concepts can
declare it too - that is why typing rules, emphasis and types are in `Projectional.Language`.
One that produces *edits* has to reference `Editing`, so it lives beside the language in a
package of its own: `Projectional.Expressions.Intentions`, `Projectional.Samples.Checks`.
`ExpressionsLanguage.Types` is the worked example of the first rule, and the reason the
type model is not in `Projectional.Semantics`.

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

### What is implemented

`Projectional.Semantics/Scopes/` holds the model, and `DefaultScopeProvider` is the chain a
language gets when it says nothing.

| Type | Job |
| --- | --- |
| `ScopeEntry` | one thing a name can mean: the name, the node, the concept key |
| `IScope` | `Entries` (what may I reference here) and `Resolve(name)` (what does this name mean) |
| `Scope` | one level; **deliberately not deduplicated** |
| `ScopeChain` | the levels in priority order, first match wins |
| `ScopeResolution` | `Found` / `Ambiguous` / `Missing`, with the candidates on an ambiguity |
| `IScopeProvider` | the aspect: `ScopeOf(node, referenceSlot)` |
| `ScopeConstraints` | the framework's own rules about scopes, for a host to compose |

Four rules are worth knowing because each is a decision rather than a detail:

- **A level does not deduplicate.** Two entries with one name inside a level are exactly what makes a
  name ambiguous; collapsing them would turn a decision the user has to make into a silent pick.
- **The chain does not fall through an ambiguity.** A nearer level that knows the name twice makes the
  name ambiguous; looking further for a unique answer would make a nearer declaration lose to a farther
  one, which is the opposite of what a chain is for.
- **Only named nodes contribute**, and the node being resolved is excluded everywhere. A node with no
  name slot or an empty name is not something a name could mean, and a reference that could point at
  the thing holding it is a cycle the model already refuses.
- **The default chain is local, ancestors, model, project** - in that order, nearest first. The
  "imported models" step is not implemented: a project has no import list yet, so the project level
  stands in for it.

`EditorSession.Scopes` is the aspect, defaulting to `DefaultScopeProvider.Instance`. It is a property
rather than a constructor argument because a language package cannot reference the editing layer, so
the host is what installs a replacement.

**A language replaces a level by declaring a provider.** `ScopeProviderRegistry` is itself an
`IScopeProvider`: it maps a concept key to a provider, walks the super concepts in declaration order
(the same order notation and triggers resolve in), and falls through to `DefaultScopeProvider` for
everything else. A host installs one by assigning it to `EditorSession.Scopes`, so there is one place
that decides which provider wins rather than a second kind of provider.

### The two consumers

- **Completion.** A caret in a `ReferenceCell` offers the scope's entries that fit the slot's declared
  concept (`CompletionKind.Reference`). Accepting one re-resolves the *name* through the scope at the
  moment the key is pressed - the list carries a name, not an id - so a target that was renamed or
  removed after the list opened is refused instead of linked. A name that has become ambiguous is
  refused too, which is the "no silent pick" rule applied to the gesture.
- **A typed name.** Typing into a reference cell resolves the run of characters against the same
  scope: a name that means exactly one thing links the slot, a name that means nothing yet writes
  nothing and keeps the run (a name is spelt over several keystrokes), and a name that means two
  things is **rejected with a reason** rather than picked. Nothing is written while the name is
  incomplete, so a half-typed name costs no undo step.

Both go through `IScope`, which is the point: a list that offered something the name cannot mean, or a
name that resolved to something the list would not offer, would be two implementations disagreeing.

- **A finding, for the ambiguity no keystroke causes.** Both gestures above refuse an ambiguous name, so
  a document cannot be *made* ambiguous by typing. It can become ambiguous afterwards - rename one element
  and a reference elsewhere shares its name with another - and that reference still draws as resolved,
  because it points at a node id. `AmbiguousReferenceConstraint` (shipped in `ScopeConstraints.All`)
  reports it: it resolves the **target's current name** in the reference's own scope and reports a
  `Warning` on the reference slot when that name identifies more than one element. Resolving the stored
  `resolveInfo` instead would report names no user ever sees, because a rename does not rewrite it; and an
  unresolved reference is left alone, since it is already visibly wrong and its fallback text may be
  stale. The rule is declared for every node rather than per concept - a reference slot can be declared by
  any concept, and a language's concepts need not share a root.

All three go through `IScope`. A rule that resolved differently from the gesture that wrote the
reference would be a third answer to the same question.

## Languages come in two flavours

Both implement the same `ILanguage`; nothing else in the system distinguishes them.

| Flavour | Source | Loaded by | Used for |
| --- | --- | --- | --- |
| **Built-in** | C# fluent declarations, compiled into an assembly | assembly load | bootstrap languages, performance-critical languages |
| **Declared** | concept/notation nodes inside the project | reading project XML | DSLs authored in the workbench |

Neither flavour is temporary, and neither is a stage of the other
([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)): the C# path is the only one
that can carry behaviour and the only one that can ship as a package, so it stays after stage
C, while the declared path stays a front end over the same declarations. How a language is
actually written down - what a language project contains, what the definition language looks
like, and where the two paths have to agree - is
[language-authoring.md](language-authoring.md).

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

Stage C replaces the *content* of the bootstrap, not the code path. The definition languages
are declared, and the C# declarations of the same languages stay in the repository as the
parity oracle and the fail-safe
([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)).

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

The first two are the ground the rest stands on: a language project is written *in*
`Projectional.Structure` and `Projectional.Editor`, and the aspect languages below them let a
language declare its constraints, types and generation instead of coding them
([language-authoring.md](language-authoring.md)).

| Language | Purpose |
| --- | --- |
| `Projectional.Structure` | concepts, slots, superconcepts (stage C) |
| `Projectional.Editor` | notation cells, styles (stage C) |
| `Projectional.ConstraintRules` | declarative validation rules — **delivered**: a language project's `constraints` folder |
| `Projectional.Types` | type rules when they outgrow C# (M6) |
| `Projectional.Migrations` | migration rules when they outgrow C# (M6) |
| `Projectional.Generators` | generation rules as templates (M6) |
| `Projectional.Intentions` | actions offered at the caret, declared (M6) |
| `Projectional.TextGen` | text output rules, declared (M6) |
| `Projectional.Accessories` | completion, keymap, node factories (M6) |
| `Projectional.Expressions` | shared expression/name sub-language used by most DSLs — **delivered (M1)** |

`Projectional.Expressions` is not a convenience: nearly every DSL needs an identifier
and a boolean expression, and duplicating them per DSL is the most common way these
frameworks become unusable.

A host composes it rather than inheriting from it, which is what keeps a DSL's concept
set its own:

```csharp
public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [.. ExpressionsLanguage.ConceptDeclarations, .. ownDeclarations]);
```

A slot declared over `Expressions.Expression` then accepts every expression concept
without the host naming any of them, and adding an expression concept later extends
every such slot at once. `Expressions.Expression` is abstract, so a slot declared over
it cannot hold a bare base node — pressing Enter in an empty one creates the integer
literal, the first concrete concept below it. That literal is hidden from the completion
list (see above) and therefore reachable *only* by Enter, which is the point of the two
mechanisms being separate.

`Projectional.Samples` (the toy rules language the standalone editor opens) is the
working instance of that seam: two concepts of its own, one notation, and a condition
slot it never fills in itself.
