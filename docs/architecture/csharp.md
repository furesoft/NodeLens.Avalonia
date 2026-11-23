# A projected C# base language

MPS gives a user who writes a DSL a *base language*: the logic inside a behaviour, a generator
or a constraint is written in the same editor as the DSL, not in a text file next to it. This
document plans the .NET equivalent - projected C# - and is deliberately a plan for **M6**, not
for M1.

Status: **partly delivered, and shipped as the base language**. A subset of C# - the concepts below,
their notation, seven rules, five intentions and a generator, and no importer - is the package
`Projectional.CSharp` (`src/Languages/Projectional.CSharp`) and is proved by `CSharpLanguageTests`. It
is registered in the shell's own registry rather than opened from a folder, so it is available in every
document the way `jetbrains.mps.baseLanguage` is: a DSL declares a slot over `CSharp.Expression` and the
user writes code there. A *declared* DSL reaches it by writing `depends on CSharp` in its `language.mps` -
the shell answers a declared name from the folders it opened and then from the packages it ships, which
is what `LanguageProject.Shipped` is for. What is still planned is the rest of this document: the remaining constructs,
the behaviour-method attachment, and execution. Generation itself is planned in
[semantics.md](semantics.md#generation); the trust boundary that makes this safe is in "Execution"
below.

## What projecting the logic buys, and what it does not

The point is not "a C# editor". It is that **the logic is model data**:

| Because the logic is a node tree | Consequence |
| --- | --- |
| it is inside the transaction system | `Ctrl+Z` after a refactoring that rewrote ten expressions undoes all ten |
| it is a concept tree | completion offers the concepts the slot accepts, not every string |
| it is validated | a type error is a diagnostic with a red squiggle on the node, not a compiler run later |
| it is diffable | a model diff covers logic, which a text diff of generated code cannot align |
| it is generated | the same tree feeds the generator, the interpreter and the diff |

What it does **not** buy, and must not pretend to:

- **The model is not compiled.** Nothing in `Projectional.CSharp` produces an assembly. The host
  runs its own build over the *generated text*, with its own compiler, its own references and
  its own errors.
- **The editor is not the compiler.** A `CSharp.Call` node does not resolve overloads. Name and
  type resolution is a semantic analysis pass the framework does not have, and pretending
  otherwise would put a Roslyn-shaped dependency at the centre of the model layer.

## The subset

A base language is useful long before it is complete, so the subset is chosen by "what does a
behaviour or a generator body need" rather than by C# grammar order.

**What is delivered today is smaller than what this section plans.** `Projectional.CSharp` holds `Int`,
`Long`, `Double`, `String`, `Bool` and `Null` literals (not `Char`), `Name`, `This`, `MemberAccess`, `Call`,
`Binary` with ten operators, `Unary` in both its prefix and its postfix form, `Conditional`,
`Parenthesized`, `Cast`, `Is`, `As`, `Index`, `New` and **collection expressions**; namespaces are
**file-scoped or braced** (one property, two forms); `Block`, `ExpressionStatement`,
`LocalDeclaration`, `Assignment` with the compound operators, `If`, `While`, `For`, `ForEach`,
`Return`, `Break`, `Continue`, `Throw`, `Try` with its `Catch` handlers and optional `finally`, and
`Switch` with its `SwitchSection` sections (`case` or `default`);
and `TypeReference`, `Method`, `Constructor`,
`Property` (an auto-property, or one whose accessors have bodies, with `get` and `set`), `Indexer` (a member named `this`, so its own concept rather than a property with an empty name slot), `Signature` (a bodiless interface member),
`Parameter`, `Field`, `Class`, `Interface` with its inheritance list, `Enum` with its `EnumMember` values,
`Behaviour` (a concept's behaviour class, a `TypeDeclaration` holding methods),
`Using` (a file's imports, whose name is a property because a namespace is not a type), `Namespace` and `File`. It has no
importer, so a document is written in the editor and the text is a derived artefact. Read the rest of
this section as the target, and the package as the evidence that the notation, constraint, intention
and generator machinery can carry a C#-shaped base language at all.

**A number is a number, and its suffix is notation.** `Long` and `Double` were added last, and the
interesting part is that they do not store a suffix. `10L` and `10` are one number and two literals in
C#, so the `L` belongs to the concept rather than to the value: `LongLiteral`'s value is an integer and
the `L` is drawn beside it, which keeps "what number is this" answerable while still generating the
spelling that makes it a `long`. `Double` needs no suffix at all - `1.5` says what it is, and an
integral value is not a mistake there, because `double ratio = 3;` is legal C# and the literal widens on
assignment. Its value is a `Float` property rather than text, so `3.1.4` cannot be stored.

Both then borrow the framework's own primitives: a `double` is `float` and a `long` shares `integer`
with `int`, because the closed property-type set holds one number with a fraction and one whole number.
That is a real limit and it is written down rather than papered over - `int` and `long` are the same type
to every rule that reads them, and only the literal's spelling tells them apart.

**`var` is a decision, not a type.** A local declaration carries an `isVar` flag, and the type slot is
optional because a local that says `var` genuinely holds no type node. The flag is what the notation reads
(drawing `var` instead of the type cell) and what the type rule reads (taking the initializer's type
instead of the declared one) - three layers, one property, which is the same bargain `isFileScoped` makes
for the namespace. A type left behind by switching to `var` is **kept rather than deleted**: it is not
drawn and not generated while the flag holds, and switching back shows the author the type they wrote
instead of an empty slot. That is deliberate - the alternative is an edit action that destroys a value the
user typed, for a view change.

One hole comes with it and is recorded rather than hidden: C# requires an initializer with `var`, and a
`var` local without one still generates `var name;`, which no compiler accepts. The type rule answers
*unknown* there, which is truthful, but nothing warns - the check for "a `var` needs an initializer" is a
rule for the constraint layer, not a projection fact.

**`this` is a concept, not a reference.** `Name` is a reference because a name *is* the declaration it
resolves to, which is what makes rename and go-to-definition work. `this` resolves to nothing of the
kind: it is the instance the code runs in, not a declaration anyone can point at, so it is a slotless
concept drawn as one keyword. What forced it is not idiom but a real hole: C# resolves a bare `message`
inside a constructor to the *parameter*, so `message = message` assigns the parameter to itself, and
`this.message = message` is the only spelling that reaches the field. Without the node the language could
not write the most ordinary constructor body there is, while constructors, parameters and fields were all
already present.

Its type is deliberately not claimed. `this` is the enclosing class, and a class is not a type this
language can describe - a `TypeReference` naming it is unknown too, for the same reason. Claiming one
here would invent a type system the rest of the layer does not have. `base` is left out for the plainer
reason: nothing in the subset needs it, and a node nobody can use is a row in the completion list nobody
should pick.

**`Char` is still absent, and deliberately.** A `char` literal needs a guarantee the property-type set
cannot express: "exactly one character". A `String` property would accept `'ab'` and generate it, which
is invalid C# produced without a single finding - worse than not offering the concept, because today the
author simply cannot write a character and therefore cannot be misled. It lands when there is something
that validates the length, whether that is a narrower property type or the check layer.

A base language is useful long before it is complete, so the subset is chosen by "what does a
behaviour or a generator body need" rather than by C# grammar order.

**Expressions (E0)**

| Concept | Notation |
| --- | --- |
| `CSharp.Literal` | abstract; `Int`, `Long`, `Double`, `String`, `Bool`, `Null`, `Char` below it |
| `CSharp.Name` | an identifier, resolved by the host's compiler, never by us |
| `CSharp.MemberAccess` | `target.member` |
| `CSharp.Call` | `target(args, ...)` with `MemberAccess` as the target for a method call |
| `CSharp.Binary` | `left op right`, operator as an enum property |
| `CSharp.Unary` | `op operand`, or `operand op` for `++`/`--` - the side is a boolean property, not a second concept |
| `CSharp.Conditional` | `condition ? whenTrue : whenFalse` |
| `CSharp.Parenthesized` | `(inner)` |
| `CSharp.Cast` | `(Type)operand` |
| `CSharp.Is` | `operand is Type` |
| `CSharp.New` | `new Type(args)` |
| `CSharp.Index` | `target[index]` |

**Statements (S0)**

`Block`, `ExpressionStatement`, `LocalDeclaration` (`var` or an explicit type), `Assignment`
(all compound operators as an enum), `If` (with `else if` as a nested `If`), `While`, `For`
(initializer, condition and increment), `ForEach`, `Return`, `Break`, `Continue`, `Throw`, `Try`.

A `for`'s initializer is a concept of its own, `ForInitializer`, and not a `LocalDeclaration`: both
semicolons of the header are the `for`'s, as `for (;;)` shows, so the initializer draws no terminator of
its own. `ForInitializer` carries the same four slots and implements `NamedDeclaration`, which is what makes
the shape the documentation spells the line the editor draws.

**Types and declarations (T0, D0)**

`TypeReference` (a name plus an optional arity, nothing more), `Method` (name, return type,
parameters, body), `Parameter`, `Field`, `Class`, `Namespace`. This is enough to generate a
class that compiles; it is not enough to describe an existing codebase.

### Deliberately not in the subset

| Left out | Why |
| --- | --- |
| LINQ query syntax | it is sugar over method calls; `CSharp.Call` already expresses the same tree |
| pattern matching beyond `is` | every new pattern form is a new concept and a new generator branch, and `is` plus a cast covers the behaviour bodies people actually write |
| `async`/`await`, `yield` | they change what the generator must emit around a body (state machine, iterator class), which is a large jump in generator complexity for a small jump in expressiveness |
| attributes | they are metadata, not logic; a host that needs one can put it in a handwritten partial (see "Behaviour methods") |
| generics with variance constraints | `TypeReference` can *name* a constructed generic; it cannot *declare* one |
| `unsafe`, `fixed`, stack allocation | the trust boundary already says we do not execute user code; there is no reason to also let it address memory |
| preprocessor directives | a model has no compilation symbols |
| expression-bodied members | a body is a `Block`; a one-statement block generates the same IL and one notation is one thing to maintain |

## Style rules, and the one thing the model cannot see

A checker that "checks C# code style" is a set of `IConstraint`s on the language's own concepts, in
`Checks/CSharpConstraints.cs` beside the correctness rules. The two kinds are deliberately kept in one
place and told apart by severity: a correctness rule may `Error`, a style rule `Warning`s or `Hint`s,
because the order of two imports changes nothing about what the code does.

**Delivered.** Eight rules, five of them correctness and three of them style:

| Rule | Code | Severity | What it says |
| --- | --- | --- | --- |
| A method needs a body | `csharp.method.body` | Warning | a body-less method generates a signature nobody calls |
| A method is still called its default | `csharp.method.name` | Hint | the name is the placeholder, so the author has not named it yet |
| A field's name carries whitespace | `csharp.field.name` | Warning | the padding is invisible in the drawing and part of the generated identifier |
| A constructor is named after its class | `csharp.constructor.name` | Warning | the generator writes the constructor's own name, so a mismatch is `public Grete()` inside `class Greeter` |
| An `if` condition must be a boolean | `csharp.if.condition.not-boolean` | Error | the first rule that needed types; a condition of unknown type is allowed, because the editor is not the compiler |
| Imports are in the conventional order | `csharp.using.order` | Warning | `System` first, then ordinal by name, the order the .NET style guide asks for |
| Members are in the conventional order | `csharp.member.order` | Warning | fields, then constructors, then properties and indexers, then methods |
| An interface's name starts with `I` | `csharp.interface.name` | Warning | the one naming convention the style guide states for a declaration |

**What is deliberately *not* here: naming, in general.** The concepts already declare it - every
`ConceptDeclaration` in `CSharpLanguage` carries a `nameSlot` and a `nameRule` (`Pascal` for a type, a
member and a namespace, `Camel` for a field, a parameter and a local, `PascalDot` for a dotted
namespace), and the standard catalogue's `IdentifierNameCheck` (STD004) reads that declaration and
respells a name that breaks it, with a repair. So a rule here that restated "a method is PascalCase" would
be the second answer to one question, and the two would drift. The same holds for two adjacent
temptations: a duplicated `using` is already `DuplicateNameCheck` (STD002), because the imports are
children of one slot and two of them share a name. What is left for this file is what the declared rule
cannot say - the conventions about *arrangement* (order, grouping) and about a name that has to agree
with a node that is not itself, like the constructor above.

**A correction: the catalogue did not reach this language.** The paragraph above said `STD004` and `STD002`
applied to C#, and neither did. A `LanguageProject` could carry constraints, intentions, types and scopes but
no check set, so C# - the one language the shell always has - was the one language the standard catalogue
never ran on, and the naming conventions the concepts declare were enforced by nothing. `LanguageProject.Checks`
closes that, and `CSharpChecks` takes `STD002` and `STD004` from the name group plus the structural and the
reference group whole. The two rules in the table above stay: `PlaceholderNameCheck` (`STD001`) and
`SurroundingWhitespaceCheck` (`STD003`) answer the questions `csharp.method.name` and `csharp.field.name`
already answer, and two sources for one finding is the duplication the shell removed once already.

**What is deliberately absent: an unused `using`.** It is not undecidable in principle, but it is
undecidable here: the language models a name as text the compiler resolves, so nothing in the model
knows that `Console` came from the `using System;` line. A rule that guessed from the text would be
wrong about every name that arrives some other way.

**The one thing the model cannot see.** "A blank line after a block of fields" is a *whitespace*
convention, and a projectional model holds no whitespace: the projection draws the line breaks and the
generator writes them. So the checker cannot state the blank line, and it does not pretend to. It states
the arrangement that makes the blank line land in the right place - **the fields are contiguous** - and
the projection is what puts the line where the block ends. That split is the honest one: a rule that
claimed to see a blank line would be reading the projection's output, not the model.

**Delivered: the projection half.** The blank line itself is a projection fact, and it now has a
declaration of its own rather than an engine convention: `ChildNotationCell.Gaps` draws
it and `ChildrenPart.Gaps` writes it, and both read `ChildGroups` for *where* a group
ends so the drawing and the generated text cannot disagree. Three details were what made it more than a
flag:

- **A group is a run of a declared kind, not of one concept.** `ChildGroups.IsBoundary` asks a tight set
  the language supplies: a boundary is a group boundary unless *both* neighbours are in it. C#'s set holds
  one entry, `Field`, which is the "blank line between members, except inside a block of fields" convention
  spelled out. An empty set therefore separates every child, which is the convention's own default.
- **The blank line is a bare newline, not a doubled separator.** `LineBreakPart` indents eagerly, so a
  second break would leave a whitespace-only row - the trailing space the earlier "empty member list" fix
  deliberately removed. The generator writes one unindented newline at the boundary and lets the ordinary
  separator start the next row, so the generated text has no trailing space and the drawing has a gap.
- **The projection asks for a gap, not for a cell.** A spacer cell would be a leaf and therefore a caret
  target, so the caret would stop on a row nobody drew. `ChildCell.BlankLineBefore` names the positions
  instead and `CellLayouter` advances the cursor by one line height at each, which keeps the caret on the
  members and the blank row out of the tree.

**Where the lists differ.** The rule is the same everywhere; the *value* is not, and that difference is the
convention rather than an accident. `ListGap` names the three positions a list can breathe at, and a list
takes the flags it wants:

| List | Gaps | Why |
| --- | --- | --- |
| A class's members | `BetweenGroups`, tight set `{Field}` | a run of fields stays together, every other member is separated |
| A namespace's types | `BetweenGroups`, empty tight set | every type is separated from the next, two classes as much as a class from an interface |
| A file's imports | `AfterLast` | the blank line separates the last directive from the namespace declaration, not two directives from each other |

Members of one kind belong together; types of any kind never do. That is why the two *group* lists carry
different sets instead of one shared "same concept is tight" rule - that rule would glue two methods
together as well.

Two of the three positions are ones a group boundary structurally cannot describe, because a boundary
separates two *neighbours*: there is nothing above the first child, and nothing below the last. So
`ListGap.BeforeFirst` and `ListGap.AfterLast` say it directly, and the projection keeps one mechanism -
a gap always sits *above* a position, so the index set is a range and position `Count` means the row
after the last child. A class's members take no gap before the first because the brace above already
separates it; a namespace's types take one in the file-scoped form only, where the header is the row
above. That asymmetry is expressible because the same concept's `ClassesSlot` appears in two conditional
blocks, so the two forms declare different gaps for one list.

The imports' trailing gap lives *inside* the `WhenFilledBlock` that wraps them, which is what keeps a
file without directives from opening with a blank row. Its position is also why the generator's `File`
template lost the `LineBreakPart` it used to carry after the imports: the list writes that break now, so
the notation and the template cannot disagree about one newline or two.

The rules a style checker cannot state in a model at all - line length, brace placement, trailing
whitespace, `var` versus an explicit type - are projection choices here, not model facts, and belong to
the notation's own conventions rather than to a constraint.

## The two ways logic attaches

**1. A slot typed over `CSharp.Expression`.** The simplest case, and the one a DSL author reaches
for first: a concept declares a slot whose content is an expression, and the notation shows it
inline. This is exactly what `Rules.Rule` already does with `Expressions.Expression`.

**2. Behaviour methods, one partial class per concept.** This is the MPS behaviour aspect: a
concept declares methods that belong to it, the generator emits them into
`partial class <Concept>Behaviour`, and the host writes the other half of the partial by hand
when it needs something the model cannot express. That is what makes attributes, `unsafe` or a
hand-tuned method possible without adding a concept for each.

**Delivered: the concept and its two halves.** `CSharp.Behaviour` is a `TypeDeclaration` holding
`Method` children, so a behaviour class is declared in a namespace beside the classes it serves. Three
decisions are worth the words:

- **It is a type, not a second root.** The language has exactly one root - the document - and that is what
  makes "new document" one question. A behaviour class is not a document; it is a type, and being one is
  also what lets its `partial` merge with the half the host writes, since C# merges by name *and*
  namespace. It is declared after `Class`, `Interface` and `Enum` so it is never the concept a fresh
  namespace slot is filled with, because `DefaultInstantiableFor` answers with the first concrete concept
  below the slot's.
- **The concept key is a property, and it is generated as a comment.** C# has nothing a concept could be
  referenced by, so the attachment is text. It is emitted as `// behaviour Rules.Rule` above the class,
  because the host's hand-written half says nothing about the model - the generated half is where "which
  concept is this for" survives once the file stands alone. That also settles open question 12 for this
  slice: a behaviour is generated into the document it is declared in, not into a file of its own.
- **The key and the class name both carry placeholders.** `Concept.Key` and `ConceptBehaviour` keep the
  slots non-empty, because there is no cell for "draw this while the property is filled" - `WhenFilled` is
  a child slot's cell and the conditional compares a *value*. An empty property would have generated
  trailing whitespace inside the comment, the defect the empty member block fix removed.

**The traceability this slice was grouped with was already delivered, and had a defect.** `GeneratedText.Regions`
records, per emitted stretch, the node and slot it came from; `RegionAt(offset)` answers which node wrote a
position; `DocumentGenerate.RowsOf` turns that into the shell's one-row-per-generated-line report whose rows
carry the node to jump to. Two things were wrong with it before this slice looked: nothing tested the
consumer, and it asked about each line's *first* offset - which is the indentation the parent's separator
wrote, so every indented line answered "nobody" and had nothing to jump to. The row now asks about the first
visible character, and `DocumentGenerateTests` pins the whole answer.

**The other half is proven, not assumed.** `CSharpBehaviourCompilationTests` generates a behaviour, adds a
hand-written partial beside it and compiles both with the repository's own `AssemblyCompiler` - the same one
the language export uses, which reports warnings as errors. The test then loads the assembly and asks the
type for both methods, so the claim being checked is the C# contract itself (two `partial` declarations
merging into one class) rather than the spelling of the generated text. Comparing strings would have pinned
the text without ever asking whether the two halves are one class.

That closes the slice. What is *not* closed is the next question it exposes: a behaviour method cannot yet
name the node it is about. `Name` is a reference to a declaration in the model, and the node has no such
declaration - in MPS the generated class holds it as `this`, and here the hand-written half would hold it in
a field the model cannot point at. A behaviour method is therefore useful today for what its parameters
carry, and reaching the node needs a decision (a declared node field, or a `This`-like concept for a
behaviour) that this slice deliberately left open.

The part of the behaviour aspect that needs **no** code is planned separately and comes first:
a node's `Name` and `Describe` as pure functions declared as data ([behaviours.md](behaviours.md)).
What is left here is the part that genuinely needs a language: a method with statements in it.

The difference from M4's `Evaluate` delegate is worth stating because they look alike:
`Evaluate` is **one function per concept** that the framework calls during interpretation;
behaviour methods are **a set of named methods** that the generated source calls. A DSL that
wants a `ToString`-like method needs the second; a DSL that wants "this node's meaning is this
expression" needs the first. Both generate from the same template tree.

## Composition with `Projectional.Expressions`

There are already eight expression concepts, delivered in M1, and a DSL may already have slots
typed over `Expressions.Expression`. Two rules make projected C# add to that without a
migration:

- **A `CSharp.*` expression concept takes the corresponding `Expressions` concept as its super
  concept.** `ConceptDeclaration` takes `superConcepts`, so
  `new ConceptDeclaration("CSharp.Binary", slots, superConcepts: [ExpressionsConceptKeys.Binary])`
  means a slot declared over `Expressions.Binary` accepts a `CSharp.Binary` with no language
  change. A slot declared over `Expressions.Expression` accepts every C# expression, which is
  the case that matters.
- **`Expressions` must not reference `CSharp`.** The arrow is `CSharp -> Expressions ->
  Language`, so the sample language stays usable without the base language, and a host that
  wants only the small expression language does not pull in a C# generator.

**Do not merge the two languages.** `Expressions.Binary` has an operator enum of arithmetic and
comparison symbols; `CSharp.Binary` needs `??`, `is`-style operators and the full precedence
table. Making one concept serve both would mean the small language grows C#'s whole operator
set, and its notation - which is a `Line`, because `ChildInline` demands it - would have to
stay one line while C# needs parentheses by precedence.

The overlap is real and needs a decision record: **an ADR at M6** must say whether
`CSharp.Binary` supers `Expressions.Binary` (as above, so a mixed model has one hierarchy) or
whether they are siblings with a shared abstract parent. The recommendation is the former,
because it needs no change to any already-written slot.

## Generation

**Where the C# concepts come from** is not this document's subject: a DSL that must *become*
C# - a decision table that lowers to a class with a method - reduces its own concepts into
`Class`, `Method`, `If`, `Return` nodes first, and only then are those printed
([model-generation.md](model-generation.md)). Writing C# from templates instead would keep the
grammar in strings as well as in the notation and skip the target's checks. What follows is
about printing the concepts, which is what makes the target a language rather than a naming
convention.

Reuse the template tree from [semantics.md](semantics.md#generation) - `TextConstant`,
`PropertyRef`, `ChildExpansion`, `ChildrenExpansion`, `Conditional`, `Loop`, `LineBreak`,
`Indent`. Nothing about C# needs a new node kind; what it needs is two rules.

**Parenthesise by precedence, and only then.** The generator walks the tree knowing each
concept's precedence, and wraps a child in parentheses when

- the child's precedence is **lower** than the parent's, or
- it is **equal** and the child sits on the side where associativity does not hold
  (the right operand of `-`, `/`, `%`, `<<`, `>>`, and both sides of `??`).

This is not a new policy, it is the one `ExpressionOperators` already states: precedence is
deliberately not part of the projection, "associativity and precedence belong to whoever
analyses the tree". The generator is that consumer for C#, and the interpreter is that consumer
for the small expression language.

Parenthesising more often than that produces `((a + b) * c)` for a tree the user wrote as
`(a + b) * c` and no one can tell which parentheses are theirs. Parenthesising less produces a
program that computes something else. This is the single most likely place for a silent
semantic bug in the whole feature, so it gets a dedicated corpus of "same tree, three
spellings" cases.

**Identifier mapping is the generator's job.** The model keeps the name the author typed; the
generator escapes it: a C# keyword gets `@`, characters that are not legal in an identifier are
replaced deterministically (not stripped, so two distinct model names cannot collide into one
identifier), and a leading digit gets a prefix. The mapping is a pure function of the model
name, so regenerating gives the same output.

**The namespace and the assembly identity belong to the host**, not to the language (open
question 12): the framework returns text, the host decides where it lands.

**Traceability.** The generator records, per emitted range, which node produced it, so a host
can offer "go to source" from generated text back to the model, and a compiler error reported
at a generated line can point at a node. The map is returned next to the text and is not stored
in the model.

## Verification

Three levels, because the interesting failures are semantic, not textual:

1. **Golden files.** A corpus of models that each generate to a checked-in `.cs` file. A byte
   that moves is a change to the generator and must be reviewed, exactly like the `.mps`
   corpus: never regenerate a golden file to make a test pass.
2. **The host's build.** CI generates the corpus and compiles it with `dotnet build` on the
   generated output. This is the only test that proves the text is C# and not merely stable.
3. **The parity gate.** The same document is run twice: the M4 interpreter walks the model, and
   the generated text is compiled and executed. Both have to reach the decision the corpus
   states, or neither is evidence for the other. **Landed** for the pilot:
   `RulesGenerationParityTests` holds one corpus of rulesets - whole numbers, text and dates: a
   threshold either side, arithmetic at three levels, division's truncation, rule order, a
   conjunction and a disjunction, a negation, a conditional expression, text equality and
   concatenation, two dates, and a ruleset with no rule - wraps each generated fragment in the
   host's method, compiles it with `AssemblyCompiler` and invokes it with the same arguments the
   interpreter was bound with. A ruleset that matches nothing is stated as no decision: the
   interpreter reports a failure, the host's method throws, and neither may invent an outcome. It
   found a defect on its first run - a condition that is not a comparison generated `if !flag`,
   because only a binary expression brought parentheses of its own - and the `if` now supplies the
   ones C# requires. It also found a semantic divergence it first recorded and then closed: the
   interpreter evaluated both operands of `&&`/`||` where C# short-circuits, so
   `OperatorSemantics.ShortCircuits` now states the rule once for both the hand-written expression
   rules and a declared operator body, and the corpus covers it. One divergence it still records
   rather than covers: `^` is a power here and XOR in C# (under "Risks").

Optional fourth level: a separate **`Projectional.CSharp.Check`** assembly that uses Roslyn to
compile the generated text in memory and report diagnostics against the traceability map. It is
a separate assembly on purpose - core stays Roslyn-free, so the model layer never depends on a
compiler, and a host that does not want Roslyn never restores it. Its value is error messages
with node positions in a test, not a new capability.

## Execution, and the trust boundary

**The framework never compiles or executes projected C#.** It generates source. The host's own
build compiles it, under the host's references and the host's sandboxing decisions.

That sentence is what resolves the roadmap row "User-defined executable logic | trust boundary;
would require a sandbox" - it stays out of scope **for the framework**. Generating source that
the host compiles is not the framework executing user code, and the difference is not
cosmetic: the host already runs a build of its own code, so a generated file is no new
privilege. Running a DSL *inside* the editor, without a build, is the M4 interpreter, and that
runs framework-defined semantics over framework-defined nodes - also not user code.

Anything that would make the editor itself compile and load an assembly is out of scope before
1.0 and needs an ADR with a sandbox design.

## Milestone and slices

M6, after the interpreter exists, because the interpreter is the oracle the generator is tested
against.

- **C1 - expressions only.** `CSharp.*` expression concepts, notation, generator with the
  precedence rule, golden corpus, the "same tree, three spellings" cases, and the super-concept
  composition test against a slot typed over `Expressions.Expression`.
- **C2 - statements and declarations.** Blocks, `if`/loops, methods, classes, identifier
  mapping, and the first corpus that CI compiles.
- **C3 - behaviour methods and traceability.** One partial class per concept, the hand-written
  other half in a sample, the traceability map, and the "go to source" test. **Landed:** the
  behaviour class, the "go to source" test (the map was already in the generator), and the
  hand-written half as a compilation test rather than a checked-in sample - a test proves the merge
  on every run, where a sample proves it once.

**The gate:** the compiled generated code and the M4 interpreter produce identical results on
the shared corpus. Two independent implementations agreeing is the only evidence that either is
right; a generator that merely round-trips its own output proves nothing. **Landed** for the
pilot: `RulesGenerationParityTests` is the corpus and the comparison, and its first run found the
`if !flag` defect that "Verification" records.

## Risks

| Risk | Mitigation |
| --- | --- |
| Two type systems disagree (model types vs C# types) | the model's `TypeReference` is a name, not a type; the compiler is the only authority and the interpreter mirrors it only for the subset in the corpus |
| `^` means a power in the interpreter and XOR in C# | the fragment prints the operator symbol verbatim, and a template cannot map a symbol to an expression, so a rule that uses `^` generates different behaviour. Found by `RulesGenerationParityTests`, which therefore does not cover it; fixing it needs a part that maps a symbol, not a corpus case |
| No overload resolution, so a call's meaning is invisible in the editor | accepted for M6: completion offers names, not overloads; a diagnostic for an unresolvable name is a later pass |
| Statement notation is long on screen | that is what `Indent` and the caret-by-cell model are for; the measured caret path in the rules sample is the test that it stays navigable |
| `Indent` blocks change how the caret moves | covered by the existing caret tests; a `Block` is a `Sequence` under an `Indent`, so it adds no new cell kind |
| Users try to type `if (x) y;` as text | it must be refused like any other letter into a non-text cell - the model is the only source of truth (ADR 0002), and a text-to-tree path is exactly what import is for and what editing is not |
| Precedence bugs read as "the editor is wrong" | the corpus of equivalent spellings, plus the interpreter cross-check |

## Open questions

1. **Does `CSharp.Binary` super `Expressions.Binary`?** Recommend yes; needs the M6 ADR either
   way (see "Composition").
2. **One behaviour class per concept, or per concept-and-DSL?** Recommend per concept: a concept
   key is unique per project, so the generated class name is unique too.
3. **Where does the generated file go?** Host's decision (open question 12), including whether
   it is one file or one file per concept.
4. **Should the editor know any C# at all?** No. Everything C#-specific lives in the language
   package and its generator, so a host that ships its own base language replaces the package
   without touching the core.
