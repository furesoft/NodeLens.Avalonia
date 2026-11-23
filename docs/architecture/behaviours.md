# Concept behaviours

Status: **B1 delivered** (M6) and **parked here by decision**. The seam exists:
`ConceptDeclaration(nameOf:)` and `Concept.NameOf(node)` answer "what is this node called" as a pure function of the
node, `Node.Name` asks it first and the name slot second, and the body vocabulary a name is written in lives in
`Projectional.Language/Names/` (`NameLiteral`, `NameProperty`, `NameChild`, `NameReference`, `NameJoin`,
`NameFallback`, `NameChoice`, with a depth budget and empty-answering slots). The declared form of that vocabulary is
its own language and its own folder: `Projectional.Behaviours` declares the `behaviour` document
(`BehaviourLanguage`), its notation (`BehaviourNotations`) and the lowering that answers the delegate a declaration
takes (`BehaviourLowering`), and `LanguageProject` reads a `behaviour` folder before it builds the concept registry,
so `behaviour for Atlas.Stop` is what a reference to a stop shows. What is **not** started: `Describe`, the samples
and the parity test beyond the one the atlas carries, and the override finding. Further behaviour work is
deliberately deferred.

The nine consumers no longer each ask the slot: a reference cell (`EditorTreeBuilder`), sort by name (`ChildOrder`),
the outline, the problems panel, the usages header, the scope index (`NameIndex`), ambiguity
(`AmbiguousReferenceConstraint`) and the stored `resolveInfo` (`EditContext.SetReference`) all read the one answer.
Rename and the naming checks still read the slot, because they **write** it - a computed name has nothing there to
write, which is a language's business rather than the framework's.

MPS' [concept functions](https://www.jetbrains.com/help/mps/concept-functions.html#example) page is
the source for this plan. It describes two things that share the name "behaviour", and they are
handled separately here: the **Behaviour aspect** (methods on a concept, invoked polymorphically by
the framework and by generated code) and **concept functions** (hooks through which a *language user*
supplies code that the generator emits). The first is planned below in the only form this framework
can honour without a code base language; the second is not planned here at all.

## Why

`Node.Name` (`src/Projectional.Core/Model/Node.cs`) is the single answer to "what is this node
called", and nine places ask it. The list matters because **every one of them degrades the same way**
when a concept declares no name slot, and none of them can be satisfied by a concept whose name is
spread over two properties or is a value:

| Consumer | Where | What happens without a name slot |
| --- | --- | --- |
| the text of a reference slot | `ReferenceCell` (`Projectional.Projection/Tree/`) | draws `<Concept.Key>` instead of a name |
| sort by name | `ChildOrder.ByName` (`Projectional.Editing/`) | refuses the **whole list**, because a partial sort would order the unnamed ones arbitrarily |
| the model browser | `Outline.Of` (`Projectional.Workbench/Outline/`) | the row falls back to the concept key |
| the problems panel | `ProblemList.Of` (`Projectional.Workbench/Problems/`) | the row falls back to the concept key |
| what a typed name means | `DefaultScopeProvider` (`Projectional.Semantics/Scopes/`) | the node is not something a name could mean, so a reference cannot point at it |
| ambiguity | `AmbiguousReferenceConstraint` | stays quiet, so two unnamed nodes can share nothing |
| the stored `resolveInfo` | `EditContext.SetReference` (`Projectional.Editing/`) | the fallback text is empty, so the reference degrades as soon as the target moves |
| rename | `StandardIntentions` (`Projectional.Intentions/`) | no rename offer, so the name cannot be edited from the menu |
| naming a fresh node | `PlaceholderNameCheck`, `SurroundingWhitespaceCheck` (`Projectional.Checks/`), `DefaultNames` (`Projectional.Workbench/Checks/`) | no help naming it |

Three shapes are therefore inexpressible, and all three are ordinary in a DSL:

- a **composite** name, where the interesting word is in a child (`Class1.method2`, `order.items`);
- a name that **is a value**, on a concept whose name property would only repeat it (`IntLiteral` -> `42`);
- a name that is a **sentence**, for a node that is better described than labelled.

MPS answers all three with `getName()`/`toString()` in the Behaviour aspect. Here the same answer fits
the framework's shape: **one pure function per concept, declared as data**, lowered like a type rule
or a template.

## Why the function lives on the concept, not on a session

Every other aspect is installed on the editor session (`EditorSession.Scopes`, `.Typing`, `.Markers`,
`.Fixes`) or on the document tab (`DocumentTab.Constraints`). A name cannot be: two of its consumers
have no session and no document.

- `ReferenceCell` is built by `EditorTreeBuilder`, which is handed a `NotationRegistry` and two purely
  visual options - nothing that could answer a name.
- `ChildOrder.ByName(parent, slot)` is a static function of a parent node.

A name is therefore a property of the **language**, reachable from a `Concept`, which every consumer
already has through `node.Concept`. The seam is a declaration member beside `NameSlot`, so
`Node.Name` stays the one answer and no consumer changes:

```
Concept.NameOf(concept, node)   // the declared function, else the name slot, else null
```

This keeps `Projectional.Core` reflection-free: the function is a delegate *passed in a declaration*,
exactly like `NameSlot` is a string passed in a declaration. Core learns that a concept may name its
nodes; it learns nothing about language models, notation or projection.

## The closed set, and why it is closed

Two functions are planned, each with a consumer that exists today. Everything else the Behaviour
aspect can compute either already has a home in this framework or has no consumer here, and a
vocabulary that grows a function per MPS example would be a programming language in disguise.

| Function | Answers | Consumer | Lowered into |
| --- | --- | --- | --- |
| `Name` | the one word this node is called by | the nine consumers above | `Concept.NameOf` |
| `Describe` | one sentence about this node, given the framework's own default | a trace row (`TraceEntry` prints `ConceptKey = value`; a language wants "step 3 of the loop") and a constraint message that interpolates it | a `DescribeOf` delegate the interpreter and the constraint runner consult |

`Name` is the whole of slice B1. `Describe` is B2 and is only worth building with a consumer that
adopts it in the same slice - the MPS example is exactly this function (`customizeMessage`), so it is
the right second one, but a hook nobody calls is worse than no hook.

### The four methods in the screenshot, and why three have no analogue

The example defines a concept function by overriding `getExpectedReturnType`, `getParameterConcepts`,
`showName` and `getName`. They are methods on a **Java class** that MPS generates for the function
concept, and this framework generates no such class, so three of the four are not gaps:

| MPS method | Here |
| --- | --- |
| `getExpectedReturnType()` | `TypeRules` ([semantics.md](semantics.md)) answers what a node means; there is no generated method whose return type could be declared |
| `getParameterConcepts()` | a function's parameters are its **children**, declared as slots. The concept already says what it can hold |
| `showName()` | whether the name is drawn is the **notation's** business (`ed.Keyword(...)`, `ed.Property(...)`), not a flag on the concept |
| `getName()` | `Name`, planned above - the one that survives, because it is asked about *nodes*, not about a Java class |

This is the same rule as the one about `instance can be root` and `alias` in
[mps-gap-analysis.md](mps-gap-analysis.md): a method that exists to describe a generated class has
nothing to describe here, and copying its name would add a declaration that lowers nowhere.

## The body vocabulary

A body is a closed set of parts, in the spirit of `TextPart` and the constraint language's four
bodies. It reads and it chooses; it never computes, compares numbers or calls anything.

| Part | Emits |
| --- | --- |
| `Literal(text)` | a fixed word |
| `Property(slot)` | the value of a property slot, as text |
| `Child(slot)` | the declared `Name` of the one child in that slot - **this is how a composite name is written**; the slot must hold one child, not a list |
| `Reference(slot)` | the `Name` of the reference's target |
| `Join(separator, parts)` | parts, one after another, empty ones left out |
| `Children(slot, separator)` | the `Name` of every child in that slot, empty ones left out - **this is how a name over a list is written** |
| `Fallback(parts)` | the first non-empty part - how a concept says "the name, else the value, else `<anonymous>`" |
| `Choose(property, value, then, else)` | one of two parts, by a property's value - the `Conditional` rule, one level up |

Two guarantees the lowering provides, because they cannot be left to the author:
- **Total.** Every part answers a string; `Child` on an empty slot answers the empty string, which
  `Join` and `Fallback` then omit. A name function that could throw would make `ReferenceCell`'s build
  fail for a half-written document, which is the state a document is in most of the time.
- **Bounded.** `Child` and `Reference` recurse into another concept's `Name`, so a cycle is writable
  (`A` names itself after its child, the child names itself after its parent). The runtime keeps a
  depth budget for it (`NameBody.MaxDepth`), so the cycle answers the empty string instead of a
  `StackOverflowException`, which cannot be reported to a user. A cycle the declarations show is not
  rejected yet; refusing one needs the graph over concept keys that the lowering does not build, and it
  would have to be careful about targets declared over an abstract concept.

The lowering also refuses four bodies that would answer something other than what their author meant,
because a name is asked where nobody can see the document that produced it:

- **A slot the concept does not have, or one of the wrong kind.** `property` reads a `PropertySlot`,
  `child` a `ChildSlot`, `reference` a `ReferenceSlot`.
- **A `child` part on a list.** `Child` means "the one child in that slot", and a list would answer its
  first element - a name the author never asked for and cannot see. Naming a node after *all* children is
  `Children`, which says so.
- **A `choose` value the property cannot hold.** `choose isCollapsed = yes` would answer the otherwise
  arm for ever, with nothing visibly wrong in the document.
- **A `choose` value the property would store differently.** A choice compares what the property
  *stores*, which is the canonical spelling of its type, so `choose count = 01` never matches a stored
  `1`. The refusal names the spelling to write instead.

A name may be declared on an **abstract** concept, and then every subtype inherits it: one document
names a whole family of nodes.

## What this must not become

- **Not code.** No statements, no variables, no arithmetic, no loops. A name that needs a loop is a
  name that should be stored. This is the line between this plan and
  [csharp.md](csharp.md), whose behaviour *methods* are a set of named methods with real bodies and
  arrive with the projected C# base language.
- **Not cached.** `Node.Name` is asked once per reference cell, once per sort, once per browser row
  and once per scope entry. A cache would need invalidation on every property and child change, which
  is a second change-notification path. Computing a short string per call is cheaper, and a language
  whose name is expensive should store it instead. `Children` is the one part that can make a name cost
  more than a constant - it is proportional to the children it names - which is the author's decision
  and is still bounded by the document.
- **Not `alias`.** MPS' alias is *concept*-level data - the word completion offers and typing creates
  an instance of. `Name` is *node*-level. They are the two halves of one idea and they are declared in
  two places on purpose; implementing either by reusing the other would make a concept that offers a
  different word than its nodes are called.
- **Not `Framework.Named`.** The interface plan gives a concept a name *slot* that a slot can be declared
  over; a behaviour answers a computed name there is no slot for. They compose rather than compete: a concept
  with both still declares its name slot, and `Name` is asked first, so the slot stays the thing the interface
  promises while the drawing shows the computed word. A behaviour must not become the way a language avoids
  declaring a name slot: a slot declared over `Named` reads the name property, so a concept that only computed
  its name would leave that slot with nothing to read.
- **Not a second lookup for what a name means.** `DefaultScopeProvider` and
  `AmbiguousReferenceConstraint` move to `node.Name` in the same slice that changes it, so a document
  cannot be sorted by one name and resolved by another.

## The declared form

A `behaviour` folder in a language project, beside `structure`, `editor`, `types`, `generator` and
`constraints`, holding one document per concept (`behaviour/Rules.Rule.mps`). One definition per
document is already this project's rule ([language-projects.md](language-projects.md)), and it makes a
diff of one concept's behaviour a diff of one file.

A behaviour document names its concept by key, exactly as a `TextGen.Generator` mapping names its
concept, and holds one function per kind - which is what the `kind` property says, `name` being the
only kind the lowering answers today. The lowering refuses a function for a concept the language does
not have, a concept that declares two names, a kind it does not lower yet, and a body naming a slot
that does not exist or has the wrong kind.

**The language is delivered, and so is the folder.** `Projectional.Behaviours` declares the document
(`Behaviour.Document`, one per concept, with `concept` and `functions`), the function (`kind`, `body`),
and the eight body parts, drew them in the shape the definition languages use, and lowers a document to
the delegate `ConceptDeclaration(nameOf:)` takes. `LanguageProject` reads the `behaviour` folder
**before** it builds the registry - a name has to be part of the declaration, so it cannot be installed
afterwards - and hands the answers over through `ConceptDeclaration.WithNameOf`, which answers a copy
because a declaration is immutable and the registry is built from it. The lowering checks a body against
a registry built from the declarations as they stand, which is what lets it refuse a slot that does not
exist before the language is usable.

A name belongs to the concept that declares it: a document naming a concept a base language declares is
refused rather than dropped, because overriding an inherited name is the finding B4 plans.
`samples/declared-atlas` carries one behaviour - a stop is called after the place it stops at, which is
the case this whole vocabulary exists for.

**`overrides` is a finding, not a word.** The screenshot draws `overrides ConceptFunction.getName()`
because the Java class says which method it overrides. Our notation is data and cannot know the super
concepts, so an author who redeclares a function their super concept already declares gets a
**finding** naming the super concept, not a drawn word. Drawing it would need a second place to say
which concept this one inherits from, and the registry already knows.

## Slices

| Slice | Content | Effort |
| --- | --- | --- |
| B1 | `Concept.NameOf`, `Node.Name` routed through it, the seven body parts the seam started with, `Projectional.Behaviours` with the declared document, its notation and its lowering - **delivered** | 1 week |
| B1b | The `behaviour` folder in `LanguageProject`: lowered before the registry is built, handed to the declarations as `nameOf` through `ConceptDeclaration.WithNameOf`, with `samples/declared-atlas` carrying one - **delivered** | 2-3 days |
| B1c | The refusals that keep a body from answering what it never meant (a `child` part on a list, a `choose` value the property would store differently), and `Children` as the eighth part, so a name over a list is writable - **delivered** | 2 days |
| B2 | `Describe` with the two consumers (a trace row, a constraint message that interpolates it), only if both adopt it | 3-4 days |
| B3 | `Projectional.Samples` and `samples/declared-pipeline` grow one behaviour each, proving the declared path, plus a parity test that a C# lambda and a declared body answer the same name for every node of the corpus | 3-4 days |
| B4 | The override finding, an intention that removes a redeclaration, and refusing a name cycle the declarations show | 2 days |

## Non-goals

- **No arbitrary code on a concept.** That is [csharp.md](csharp.md), and it is the only honest way to
  offer real logic; a half-interpreter here would be a second, weaker one.
- **No `toString` for the framework's own messages.** A diagnostic's `Message` stays the sentence the
  check wrote; `Describe` is what a check may interpolate, not what replaces its message.
- **No behaviour for notation, triggers or applicability.** Each of those is already declared, in the
  aspect that owns it.

## Open questions

1. **May `Name` be empty?** A concept could compute an empty string where the name slot holds a value.
   Recommend: yes, and it reads as "not called anything", which is what a half-written node is.
2. **Is the spelling `behaviour` or `behavior`?** The documents here use the British form in prose and
   MPS uses the American one. Recommend `behaviour`, matching the prose, including the folder and the
   concept keys, and accepting one spelling rather than two.
3. **Does a behaviour document need to exist for a concept to have a name?** No: no document means the
   name slot, which is today's behaviour, so a language that never opens this aspect is unaffected.
4. **What does the completion list show for a node?** Today the concept key. Recommend the `Name`, once
   it exists, because it is the same word the reference cell will print.
