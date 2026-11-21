# A projected C# base language

MPS gives a user who writes a DSL a *base language*: the logic inside a behaviour, a generator
or a constraint is written in the same editor as the DSL, not in a text file next to it. This
document plans the .NET equivalent - projected C# - and is deliberately a plan for **M6**, not
for M1.

Status: **partly delivered, and shipped as the base language**. A subset of C# - the concepts below,
their notation, three rules, five intentions and a generator, and no importer - is the package
`Projectional.CSharp` (`src/Languages/Projectional.CSharp`) and is proved by `CSharpLanguageTests`. It
is registered in the shell's own registry rather than opened from a folder, so it is available in every
document the way `jetbrains.mps.baseLanguage` is: a DSL declares a slot over `CSharp.Expression` and the
user writes code there. What is still planned is the rest of this document: the remaining constructs,
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
`String` and `Bool` literals (not `Long`, `Double`, `Null` or `Char`), `Name`, `MemberAccess`, `Call`,
`Binary` with ten operators, `Parenthesized` and `New` (not `Unary`, `Conditional`, `Cast`, `Is` or
`Index`); `Block`, `ExpressionStatement`, `LocalDeclaration`, `Assignment`, `If`, `Return` and `Throw`
(not `While`, `For`, `ForEach`, `Break`, `Continue` or `Try`); and `TypeReference`, `Method`,
`Parameter`, `Field`, `Class`, `Namespace` and `File`. It has no importer, so a document is written in
the editor and the text is a derived artefact. Read the rest of this section as the target, and the
package as the evidence that the notation, constraint, intention and generator machinery can carry a
C#-shaped base language at all.

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
| `CSharp.Unary` | `op operand` and postfix `++`/`--` as an enum property |
| `CSharp.Conditional` | `condition ? whenTrue : whenFalse` |
| `CSharp.Parenthesized` | `(inner)` |
| `CSharp.Cast` | `(Type)operand` |
| `CSharp.Is` | `operand is Type` |
| `CSharp.New` | `new Type(args)` |
| `CSharp.Index` | `target[index]` |

**Statements (S0)**

`Block`, `ExpressionStatement`, `LocalDeclaration` (`var` or an explicit type), `Assignment`
(all compound operators as an enum), `If` (with `else if` as a nested `If`), `While`, `For`
(three optional parts), `ForEach`, `Return`, `Break`, `Continue`, `Throw`, `Try`.

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

## The two ways logic attaches

**1. A slot typed over `CSharp.Expression`.** The simplest case, and the one a DSL author reaches
for first: a concept declares a slot whose content is an expression, and the notation shows it
inline. This is exactly what `Rules.Rule` already does with `Expressions.Expression`.

**2. Behaviour methods, one partial class per concept.** This is the MPS behaviour aspect: a
concept declares methods that belong to it, the generator emits them into
`partial class <Concept>Behaviour`, and the host writes the other half of the partial by hand
when it needs something the model cannot express. That is what makes attributes, `unsafe` or a
hand-tuned method possible without adding a concept for each.

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

Two levels, because the interesting failures are semantic, not textual:

1. **Golden files.** A corpus of models that each generate to a checked-in `.cs` file. A byte
   that moves is a change to the generator and must be reviewed, exactly like the `.mps`
   corpus: never regenerate a golden file to make a test pass.
2. **The host's build.** CI generates the corpus and compiles it with `dotnet build` on the
   generated output. This is the only test that proves the text is C# and not merely stable.

Optional third level: a separate **`Projectional.CSharp.Check`** assembly that uses Roslyn to
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
  other half in a sample, the traceability map, and the "go to source" test.

**The gate:** the compiled generated code and the M4 interpreter produce identical results on
the shared corpus. Two independent implementations agreeing is the only evidence that either is
right; a generator that merely round-trips its own output proves nothing.

## Risks

| Risk | Mitigation |
| --- | --- |
| Two type systems disagree (model types vs C# types) | the model's `TypeReference` is a name, not a type; the compiler is the only authority and the interpreter mirrors it only for the subset in the corpus |
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
