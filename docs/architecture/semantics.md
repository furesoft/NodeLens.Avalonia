# Semantics

Everything that gives a model meaning beyond shape: constraints, types, scope
resolution, generation and interpretation. All of it is expressed as aspects on
concepts and evaluated against snapshots.

## Pass order

Fixed and documented, because aspects depend on each other:

```text
1. Structure      slots valid against concept          (always, cheap)
2. Scope          references resolvable                (needed by everything after)
3. Types          type of each expression              (needs scope)
4. Constraints    language rules                       (needs types)
5. Generation     text or model output                 (needs a valid model)
6. Interpretation run/evaluate                         (needs generation or direct eval)
```

Each pass produces diagnostics and never mutates the model. Only migrations mutate.

## Diagnostics

One diagnostics model for everything the user sees:

```csharp
sealed record Diagnostic
{
    Diagnostic(
        DiagnosticSeverity severity,   // Hint | Info | Warning | Error, in that order
        NodeId target,
        string message,
        string? role = null,           // null = the node's own text
        int? index = null,             // which child of a many-valued role
        string? code = null,           // stable identity, e.g. "RULE_002"
        CharacterRange? range = null); // null = all of the value; otherwise part of one text

    DiagnosticSeverity Severity { get; }
    NodeId Target { get; }
    string Message { get; }
    string? Role { get; }
    int? Index { get; }
    string? Code { get; }
    CharacterRange? Range { get; }
}
```

A quick fix is *not* on the record. Fixes are intentions — edit actions that change the model —
and a diagnostic that carried them would make "what is wrong" and "what to do about it" one
type, decided by the same check. The mapping from diagnostic to fix list lives beside the set
and is keyed by this record; see [quick-fixes.md](quick-fixes.md). That is the part of
[context-actions.md](context-actions.md) which lands **first**: repairs for the findings that
already exist, before the general intention list they will later join.

- Diagnostics are rendered as markers/underlines, never as blocking dialogs.
- Errors do not prevent editing, saving, or opening.
- Quick fixes are edit actions, so they are undoable and testable headlessly.
- Generation and interpretation refuse to run when errors exist in the affected
  subtree — but still run for unaffected subtrees, so a large model can be partially
  generated while being edited.

The record says *what is wrong*; it says nothing about where on screen that is or what it
looks like. Turning a diagnostic into a squiggle is the editing layer's job, and the plan for
it is [markers.md](markers.md). Two properties in the record exist for that: `Range` narrows
the complaint to a slice of one property's text when the constraint knows which characters
are wrong, and `Code` is what a test and a documentation page refer to without matching prose
that will be reworded.

## Constraints

```csharp
concept.Validate((node, ctx) =>
{
    var scope = ctx.Scope(node);
    if (node.Child("condition") is null)
        ctx.Error(node, "condition.required", "A rule needs a condition.");
    return ctx.Ok;
});
```

### What is implemented

`Projectional.Semantics/Constraints/` holds the engine, and the shape is the one above with the
collector made explicit:

| Type | Job |
| --- | --- |
| `IConstraint` | the aspect: `Check(ConstraintContext)`, reports and never mutates |
| `ConstraintContext` | the node, `ScopeOf(slot)`, and `Error`/`Warning`/`Hint`/`Report` |
| `ConstraintSet` | the language's rules: keyed by concept, or declared for every node, immutable |
| `ConstraintEngine` | walks a document (or one subtree) and answers the findings |

- **A constraint reports; it never repairs.** The repair is a `DiagnosticFix` built by whoever knows how
  to fix the finding, which is why a language author can write a rule without knowing what the editor
  can do about it. A finding with no repair reaches the squiggles and the problems panel and not the
  quick-fix list - which is the right answer for "a rule needs a condition", where inventing a condition
  would be worse than saying one is missing.
- **A concept inherits its super concepts' constraints**, in declaration order - the same rule notation,
  triggers and scope providers follow, so a language has one answer to "which declaration applies".
- **A rule about any node is declared with `Everywhere`, not with a concept key**, and runs before the
  concept's own. That is not a convenience: "this reference no longer identifies one element" is a fact
  about references in general, and a language's concepts need not share a root, so a rule declared over
  one root would silently skip every concept that does not inherit from it. `With(set)` composes two
  sets in a predictable order, which is how a host adds the framework's scope rules to its own.
- **The context is deliberately small**: the node, the scope and the collector. It cannot mutate, cannot
  reach the editor and cannot see a cell, so a rule that runs on a model nobody is drawing (a load, a
  batch check) works the same as one that runs on the open document.
- **`Check(Node)` is the incremental seam.** It answers that subtree's findings alone, so a caller that
  keeps the rest merges them itself - only the caller knows what it kept. The dependency-set machinery
  below is not implemented yet: today a caller rechecks the subtree that changed, which is correct and
  cheaper than the whole document, but it does not yet know that a *referencing* node's rule may have
  changed too.
- **The consumers are two.** `SampleConstraints` in `Projectional.Samples.Checks` is a language's own
  sentence (a rule needs a condition). `ScopeConstraints` in `Projectional.Semantics/Scopes/` is the
  framework's: `AmbiguousReferenceConstraint` reports a reference whose name no longer identifies the
  element it points at, which is the finding that closes the scope model. It reads the target's
  **current** name rather than the `resolveInfo` it was written with, because a rename does not rewrite
  the stored text and resolving it would report names no user ever sees; and it stays quiet on an
  unresolved reference, which is already visibly wrong. The app composes both and merges the findings
  with the check catalogue's in `PublishFindings` - one list, because the panel and the markers must not
  be two answers to "what is wrong". It also passes `editor.Session.Scopes` to the engine, so a finding
  about a name is about the scope the editor actually uses rather than the default chain.
- **A rule can also be written down.** `Projectional.ConstraintRules` is the constraint language: a
  `constraints` folder of documents lowers into this same set, so a language defined in project files
  states its own rules ([language-projects.md](language-projects.md)). The app checks each document with
  the rules of the language it is written in (`MainWindow.PublishFindings` reads `DocumentTab.Constraints`,
  which comes from `LanguageProject.Constraints`), and the sample language's rules stand in where a caller
  passes none - so a document of a language without a `constraints` folder behaves exactly as it did.

### Incrementality

Naive full revalidation does not scale to a million nodes. The mechanism:

- Each node has a **dependency set**: the nodes its check read (children, targets of
  followed references, properties).
- A change dirties the checked node plus every node whose dependency set contains the
  changed node.
- Dirty nodes are rechecked on the thread pool, in batches, cancellable, against a
  snapshot.
- Rechecks publish diagnostics only if their snapshot generation is still current.

Scope and type results are cached per snapshot, keyed by node, and invalidated the same
way. Caches are discarded, not patched, on generation change — simplicity beats
cleverness here until profiling says otherwise.

### Language of constraints

C# and **written rules**, since the constraint language landed ([language-projects.md](language-projects.md)): a
`constraints` folder holds one document per rule, and `ConstraintLowering` turns each into the same `IConstraint` a
C# language writes, so nothing downstream changes. A rule names the concept it is about (or names none, which makes
it a rule about every node), says what is wrong, how much it matters, and asks one of a closed set of questions: is
this single-child slot empty, is this property still the default its concept declares, has this value whitespace
around it, does this reference point at nothing.

The set is closed on purpose. A general expression body would mean shipping an interpreter for a language whose only
job is to say what is wrong - the same line the type rules draw by having three bodies instead of a statement list
([§The declared form](#the-declared-form)). A rule that cannot be lowered - no message, no body, a slot the concept
does not declare or declares as another kind - is a `ConceptDefinitionException` at load, where the author is
looking, because a rule that could never fire is the one failure nobody sees from the outside.

The parity rule of [ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) applies here too:
`ConstraintLoweringTests` writes the rules the shipped catalogue implements as models and demands the same findings -
at the same places, with the same severities - as `Projectional.Checks` produces on one document.

Constraint order within a concept is
unspecified; constraints must not depend on each other's results.

## Types

`IType` is a runtime value, and in the declared stage types are nodes. Starting point:

| Kind | Examples |
| --- | --- |
| Primitive | `bool`, `int`, `string`, `decimal` |
| Named/declared | user-defined types in the model |
| Structural | tuple/record, list, optional |
| `Unknown` | result when an expression's type cannot be computed |

`Unknown` is a first-class type: it suppresses follow-on errors so one mistake does not
produce fifty diagnostics, and it never compares equal to anything, so it cannot leak
into generation unnoticed.

Rules are per-concept, not a global unification engine. There is no type inference
beyond local propagation; if a language needs more, its concepts provide it.

### What is implemented

`Projectional.Language/Types/` holds the model; the primitive, structural, named and `Unknown` kinds are
done.

**It lives in `Projectional.Language`, beside the notation and the typing rules, because a type is a
statement about what a language means.** The layer diagram puts `Semantics` above `Language`, so a
language package that declares a type rule - the expression language does - could not reference it from
there. The consumers sit above and read the registry the language hands them.

| Type | Job |
| --- | --- |
| `IType` | `Name` and `Accepts(actual)` - assignability, asked in that direction |
| `PrimitiveType` | wraps a `PropertyType`, so a language declares its primitives where it already declares properties |
| `RecordType` | a fixed set of named fields, compared **by name, not by order** |
| `ListType` | its element type; a length is a fact about a value, not about its type |
| `OptionalType` | the inner type or nothing; accepts the inner type, and nesting is not collapsed |
| `NamedType` | a name for a type: `Name` is what it is called, `Expansion` is what it means |
| `UnknownType` | the answer when nothing says; accepts and is accepted |
| `ITypeRule` | the aspect: `TypeFor(TypeContext)` |
| `TypeContext` | the node and `TypeOf(node)` / `TypeOfChild(slot)` - the whole of local propagation |
| `TypeRegistry` | the rules, keyed by concept, inherited by the concepts below it |

Four rules worth knowing:

- **`expected.Accepts(actual)` is the only question.** It reads the way the rule does, and a caller that
  got the direction wrong would be comparing two types for equality - a different and much weaker
  question.
- **`Unknown` accepts and is accepted**, which is what suppresses follow-on errors: one mistake in a
  condition must not produce a finding on every expression that reads it. It is therefore **not** a
  wildcard to rely on - a caller that must not proceed on a guess (a generator) asks
  `type is UnknownType` explicitly. That is the "cannot leak into generation unnoticed" rule: the type
  does not stop the caller, it makes the caller say what it does about not knowing.
- **A concept with no rule is `Unknown`**, not an error. Nobody said, so nothing is known, and every
  consumer is told that rather than handed a guess.
- **A record is compared by name, not by order.** Two records that differ only in the order their fields
  were written are the same shape; the order is kept for display.
- **An optional accepts its inner type, and a plain type does not accept an optional.** "May be absent"
  is a promise about what a reader has to handle, not a different kind of value - so a slot that promised
  a value must not be handed one that might not be there. Nesting is not collapsed: `int??` says the
  absence may itself be absent, and a language that does not want that should not declare it.
- **Every structural type accepts `Unknown`**, for the same reason `PrimitiveType` does: one mistake must
  not become a finding on every expression that reads the value.
- **A name is a spelling, not a kind of value.** `NamedType` keeps `Name` (what the language calls it) and
  `Expansion` (what it means) apart, and it stays structural: `Accepts` looks through a name on both sides,
  so `Amount` that means `int` is accepted by `int` and two names for one type accept each other. The
  looking-through is not the alias's privilege - every `Accepts` starts with `NamedType.Unwrap(actual)`,
  because the *accepter* is who would otherwise refuse an alias. A consumer that has to know what kind of
  type it has (a generator) calls `NamedType.Unwrap` explicitly, for the same reason it asks
  `type is UnknownType` rather than relying on `Unknown` accepting everything.
- **`ITypeRule.TypeFor`, not `TypeOf`**: CA1716 forbids a reserved word as an interface member, the same
  reason `ICheck.Find` is not called `Of`. The class member `TypeContext.TypeOf(Node)` keeps the natural
  name, because the rule does not apply to it.

**The first consumer is a constraint**: `SampleConstraints.ConditionMustBeBoolean` asks
`context.TypeOf(condition)` and reports a condition that is not a truth value. `ConstraintContext`
therefore carries the `TypeRegistry` beside the `IScopeProvider`, and `ConstraintEngine` takes both - a
rule that needs a type and a rule that needs a scope are the same kind of rule.

`ExpressionsLanguage.Types` declares the expression language's types: a literal is its own type, a
parenthesised expression is as wide as what it wraps, a comparison is a `bool`, arithmetic is as wide as
its left operand, and a name is `Unknown` because what it means depends on a declaration this language
does not have. They ship **with** the language, not beside it: a host that declares a slot over
`Expression` asks the language it borrowed what may stand there, and composes nothing extra. That is the
one thing the type rules share with the notation and the typing rules rather than with intentions and
checks - those two produce edits, so they must live beside the language; a type rule only answers a
question.

### The declared form

`Projectional.TypeRules` is the language a type rule is written in, and it is the first *declared
aspect*: a rule is a model, and `TypeRuleLowering` turns it into the same `ITypeRule` a C# author would
have written ([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)).

| Concept | Says |
| --- | --- |
| `TypeRule.Rules` | the document: one language's rules |
| `TypeRule.Rule` | `conceptKey` and a body |
| `TypeRule.Fixed` | a type written out, so it is the same whatever the node holds |
| `TypeRule.Child` | the type of one of the node's children |
| `TypeRule.Switch` | a choice between bodies, decided by a property's value, with an `otherwise` |
| `TypeRule.Case` | one value of a switch and the body it selects |
| `TypeRule.TypeExpr` | a type written as data, abstract: one of the four kinds below |
| `TypeRule.Primitive` | `name`: `string`, `int`, `bool`, `date` |
| `TypeRule.Record` | `fields`: a fixed set of named fields |
| `TypeRule.Field` | `name` and `type` |
| `TypeRule.List` | `element` |
| `TypeRule.Optional` | `inner` |
| `TypeRule.Ref` | `name`: a declared type, expanded where it is used |
| `TypeRule.Named` | `name` and `type`: a type the language declares, written as a root of the types model |

**A type is a separate family from a body, and that is the load-bearing split.** A body may read the node
(`type of left`, `switch on operator`); a type may not. Mixing them would allow a record field whose type
depends on the rule's node - a question with no answer, because a field is part of a type and not of a
node. `TypeRule.Fixed` therefore holds a `TypeExpr` rather than a name, which is also why `type int` reads
exactly as it did before.

**The drawing of a type expression is exactly what `IType.Name` prints.** `(amount: int, currency:
string)`, `[int]`, `int?` - so the rule an author reads and the sentence a finding quotes about it are the
same text. `ATypeExpressionDrawsExactlyWhatTheTypeIsCalled` in `TypeRuleNotationTests` is that invariant as
a test. It holds for a *complete* type expression: an empty record draws the shared `<< ... >>`
empty-list placeholder, because an empty slot has to stay visible and fillable.

**Three bodies, not a statement list.** MPS' typesystem is imperative - `do { typeof(node) :== int; }` -
and copying that would mean shipping an interpreter for a language whose only job is to answer one
question per node. A fixed type, a child's type and a switch over a property cover what a DSL's rules
actually say, and each lowers to a delegate the C# path could have written by hand.

**A declared type is a root of the types model, not a second list inside the document.** So a types model
keeps one shape a reader has to learn: the rules, and the names they may use. `TypeRule.Ref` names one by
its `name` - a string the lowering resolves, the same way `TypeRule.Rule.conceptKey` names a concept -
because the alternatives are worse here: a reference slot would buy completion and `<unresolved>` drawing,
and it needs a scope provider over the document, which is a slice of its own.

Three things are load-time errors, because nothing later could decide them: a name the model does not
declare, a name declared twice, and a name written in terms of itself (`type A = [A]`, or `A` through
`B`). The last is not a recursive type but a definition with no base - it would expand forever - so the
lowering carries the chain of names being expanded and reports it with the chain in the message.

**A body that cannot be lowered is a load-time error**, not a rule that silently answers `Unknown`: a
rule with no body, a type name the framework does not have, a fixed body or a field with no type, a record
that names one field twice, a switch that declares one value twice, a switch with no `otherwise`, and the
three alias errors above all throw `ConceptDefinitionException` when the language is loaded, where the
author is looking.

**Only the primitives are nameable by a `Primitive` node.** An enumeration is a type a language declares,
and a rule that named one would have to be resolved against that language's registry - the declared
stage's job, not thisone's.

**The parity rule applies to types too.** `TypeRuleLoweringTests` writes the sample's type rules as a
model and asserts that the declared registry and `ExpressionsLanguage.Types` answer the same type for
every node of the sample document - the same "two authoring paths, one answer" check the language layer has.

## Generation

Text generation is a template tree per concept:

| Node | Emits |
| --- | --- |
| `TextConstant` | literal text, with indentation control |
| `PropertyRef` | property value, optionally escaped/transformed |
| `ChildExpansion` | the child's generated text |
| `ChildrenExpansion` | all children, separated and indented |
| `Conditional`, `Loop` | control flow in the template |
| `LineBreak`, `Indent` | whitespace |

Properties:

- Output is deterministic: same model → same bytes.
- Indentation is computed by the template, not by inspecting child text.
- Every emitted region is traceable to a node (kept in a side map), so a later
  "go to source" from generated code is possible without reworking the generator.
- Generation is pure: it does not read the file system or environment.

A second stage that produces a *model* rather than text is planned for M6
([model-generation.md](model-generation.md)): a target with its own concepts - the projected C#
base language - cannot be printed from templates without keeping its grammar in string
fragments and leaving its checks unrun. The earlier decision to leave that stage out was right
while every consumer was text: it doubles the conceptual surface, and only a target that *is* a
language pays for that.

Generation is only half of the text story. The other half is import, which is the single
place where a parser exists and the only path by which text becomes a model
([import.md](import.md)). The two are tested as a pair: `generate(import(text))` must equal
`text` for text the reference languages map completely, which is a property of the *text*, not
of the model - import mints fresh node ids, so the models are never equal.

Two consumers of the same template tree are planned on top of this: projected C# as a base
language, whose generator has to parenthesise by precedence ([csharp.md](csharp.md)), and the
JSON, XML, TOML and HOCON reference languages, which exercise it in both directions.

### What is implemented

The template is data in `Projectional.Language/Generation/`: `TextTemplate` is an ordered list of `TemplatePart`s -
`TextPart` (a literal), `PropertyPart` (a slot's value, verbatim), `ReferencePart` (a reference's target name, or
the stand-in its language names), `ChildPart` (the single child's text),
`ChildrenPart` (every child of a list, joined by a separator *template*), `LoopPart` (a body applied per child),
`IndentPart` (one level deeper), `LineBreakPart` (a newline plus the current indentation), `ConditionalPart` (a
property/value choice), `WhenFilledPart` (a choice on whether a slot holds anything) and `SelfPart` (the node's own
template, which is what a loop body needs). A generator is a
`TextGenerator` - a name and a `TemplateRegistry` - and a language declares as many as it has targets.

`ReferencePart` is the tenth and arrived late, with the SQL schema example: a DDL script names the column a key
points at, and without the part no language could emit a reference at all. It reads the model rather than a cell, so
the three states a reference has (unset, unresolved, resolved) mean in generated text exactly what they mean on
screen - the same argument `PropertyPart` makes for a value.

`WhenFilledPart` is the eleventh and the only one that asks a question about a *slot* rather than about a value: is
there anything in it. It accepts all four kinds of slot, which is what makes three things expressible that a template
could not say before - brackets that close around a list only when it has an entry, a stand-in where a value is
missing, and a tree, where a node expands its children only once they exist. It is a separate part rather than an
overload of `ConditionalPart` because that one compares a property's value with a text and is validated as such,
while this one is validated as a slot of any kind; merging them would have made one total case stand for two
questions. A defaulted property counts as filled, exactly as it draws and as `PropertyPart` emits it.

The walk is `Generator` in `Projectional.Semantics/Generation/`: it owns the indentation, a depth guard against a
template that expands itself, and the **traceability map**. Every emitted stretch is recorded as a
`GeneratedRegion(Node, Slot, Start, Length)`, and `GeneratedText.RegionAt(offset)` answers what wrote a character -
which is what makes "go to source" from generated text possible without a second generator, and what the shell uses to
let a click on a generated line move the caret to the node that wrote it.

- **A concept with no template is a failure; an empty template means "emits nothing".** The first is "nobody said what
  to emit", the second is a decision, and collapsing them would leave a silent hole in the output.
- **Indentation comes from the template, never from counting the child's text.** A separator is a template as well, so
  a list on separate lines is `ChildrenPart(slot, Template(LineBreakPart))` inside an `IndentPart` - no "indent" flag,
  and a nested block cannot be indented by whatever its child happened to produce.
- **Generation is pure by construction**: nothing reads the file system, the clock, the culture or the model's place,
  so two runs produce the same bytes.
- **A binary expression is parenthesised by the pilot's generator.** A template cannot compute precedence - no part asks
  how an operand binds - so omitting the parentheses would be right only for the tree it happened to see.

**The declared form is `Projectional.TextGen`**: a document holds any number of generators, a generator holds one
mapping per concept, and a template is a list of parts, all of it drawn in MPS' shape. `TextGenLowering` turns that
model into the same `TextGenerator` a C# author writes, and checks what a compiler would have checked - unique names,
a concept the language has, a slot of that concept named as the kind it is - at load. `LanguageProject` loads
a `generator` folder beside `structure`, `editor` and `types`, so a language in a folder declares its generators too.
The parity rule of ADR 0014 is executed by `TextGenLoweringTests`, which asserts a declared generator produces exactly
the text of its hand-written twin.

Not implemented: the model-producing second stage, planned in
[model-generation.md](model-generation.md); and precedence-aware parenthesising, which is the C# base language's job
([csharp.md](csharp.md)).

**A value is emitted verbatim, and that is a boundary rather than a gap.** A template that puts a string inside quotes
does not escape it, and the framework deliberately offers no "as JSON" part: spelling a format is the business of the
language that owns that format, in its own generator, and converting a model into JSON, XML, TOML or HOCON means
producing a **model of that language** and letting *its* generator print it
([import.md](import.md#converting-a-model-into-one-of-these-languages)). A per-value escaping part would be a second
answer to "how is this value written" - one in the language that owns the format and one in every language that prints
it - and it would escape at the wrong moment anyway, because the source template runs before the target is chosen.

## Interpretation

An `Evaluate` aspect per concept, walking the tree:

```csharp
concept.Evaluate((node, ctx) => node.Child("condition") is { } c
    ? ctx.Eval(c) is true
    : throw ctx.Error(node, "condition.required"));
```

Requirements that make it a debugger rather than an evaluator:

| Requirement | Reason |
| --- | --- |
| Explicit evaluation stack of `(node, depth)` | needed for step in/out and for mapping a runtime state back to a caret position |
| Trace of evaluated nodes with values | shows the user *why*, in their own model |
| Cancellation and a step budget | a runaway model must not hang the UI |
| Deterministic ordering | generation tests and interpreter tests must agree |

Because both a generator and an interpreter exist, they cross-check: for the pilot DSL,
a test asserts that generated code and interpretation produce identical results for the
same model (`SampleCrossCheckTests`: one input, one decision, derived three ways - the
interpreter, the rule a separately assembled expression evaluator finds, and the reason the
generated JSON records). That is the cheapest correctness net available for a language framework.

### What is implemented

The aspect is `IEvaluationRule` in `Projectional.Language/Evaluation/`, beside the notation, the typing rules
and the type rules - a language package declares what its concepts mean without referencing the layer that runs
them. `EvaluationRegistry` answers per concept and inherits from the super concepts, exactly as the type
registry does; `EvaluationContext` gives a rule `Eval(node)`, `EvalChild(slot)`, `Variable(name)`,
`PropertyValue(slot)` and `Error(message)`, and nothing that could mutate or reach the editor.

The runtime is `Interpreter` in `Projectional.Semantics/Interpretation/`, because resolving a name is a
semantic question and because a language package must not own the budget. It keeps the explicit stack, records
every node that produced a value in **post-order** (a child's line precedes the line of the node that asked for
it), counts steps, stops at `DefaultBudget` (10 000) or at the caller's token, and answers
`InterpretationResult` - a value, or an `InterpretationFailure` naming the node it happened at.

Three decisions are worth keeping:

- **A concept with no rule has no value.** Unlike a type, there is no `UnknownValue` to fall back on: "nobody
  said what this means" is a failure with a message, not a value. A half-written document therefore stops the
  run where it stopped being meaningful.
- **Nothing is cached.** Asking for the same child twice costs two steps and appears twice in the trace, which
  is what makes the trace a record of what happened rather than of what could have been shared.
- **Values are records, not objects.** `IntValue`, `BoolValue`, `StringValue`, `DateValue` compare by content,
  so an interpreter test is an assertion. `PropertyValue` maps a property through its declared
  `PropertyType.TryParse`, which is the same authority the model validates writes with.

The shipped meanings: `ExpressionsEvaluation` (literals, names, arithmetic and comparison over whole numbers,
equality by content, `&&`/`||`, unary `-`/`!`, `if`, parentheses) and `RulesEvaluation` (a rule is the truth of
its condition; a ruleset is the outcome of the **first** rule that applies, and no match is a failure rather
than a default). The rules registry of the pilot contains the expression rules, so one registry runs a
document.

**A language is runnable only when it defines an interpreter.** The registry is the language's, so the shell
answers "can this document be run" from the document's root concept rather than from the command that opened
it: `WorkbenchLanguage.InterpreterFor(model)` returns the sample language's rules for one of its documents and
`null` for everything else - a structure definition, a notation, a type ruleset, a document of a language
opened from a folder - and a document that carries no interpreter is refused instead of being run against
another language's rules. Running a structure model through the expression rules would report a missing rule
for every concept, which is a wrong answer to a question nobody should have been able to ask.

The shell's Run command (`Projectional.Workbench/Run/`, `F5`) is the whole of the app's part: `DocumentRun.Of`
takes the document, its interpreter and the variables the user typed, and answers a `RunReport` - the answer or
the reason there is none in one line, and the trace as rows that each name the node they came from, so clicking
one moves the caret there. The variables come from one line of `name=value` pairs (`RunInputs`): a value is read
as a number, then a flag, then an `yyyy-MM-dd` date, and otherwise as text, and a malformed or repeated name is
refused as a whole line rather than half-applied. A run is a snapshot, so an edit drops the report, exactly as
an edit replaces the findings.

Still open: stepping (a UI vocabulary, open question 11), value kinds for records and lists (they arrive with
the first language that evaluates one), and the cross-check against generated code, which needs the generator
first.

## Error recovery during editing

- Unresolved reference → `Unknown` type, error marker, generation blocked for that
  subtree only.
- Missing mandatory child → slot rendered empty with a hint, constraint error.
- Deleted node with incoming references → references become unresolved in the same
  transaction (never dangling IDs), so the model remains loadable.

## Performance discipline

| Pass | Strategy |
| --- | --- |
| Structure | validated on mutation, not on read |
| Scope | resolved lazily per reference, cached per snapshot |
| Types | computed on demand, cached per snapshot |
| Constraints | incremented via dependency sets, batched |
| Generation | per subtree, on demand, cached by node version |
| Interpretation | always explicit, never cached |
