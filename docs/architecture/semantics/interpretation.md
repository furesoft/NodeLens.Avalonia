# Interpretation

> Part of [semantics.md](../semantics.md), the design of record for the semantic aspects. The pass order,
> the diagnostics, the error recovery and the performance numbers stay in the parent.


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

**Two authoring paths, like every other aspect.** A rule is a model: `Projectional.EvaluationRules` declares the language
- a `rule for <concept>` holding one of seventeen bodies: a value read from the node (`property`, `child`, `variable`), a
question about the node's own shape (`has`, which asks whether a slot holds anything, and `count`, which says how many
entries a child slot holds), the value of the node a reference points at (`reference`), a value written into the rule
(`constant`), a total over a list (`sum over ... of ...`), a record of named values (`record`) and one field of one
(`field ... of ...`), the values of a slot as a list (`list of ...`), arithmetic and logic over other bodies
(`operator`), a choice (`choose` with `condition`, `then`, `otherwise`), the first entry of a slot whose condition holds
(`first` with `over`, `when`, `then`), a name bound while another body answers (`bind`) and the value read back under
that name (`bound`), and a refusal with a sentence (`fail`) - and `EvaluationRuleLowering` turns each into the same
`DelegateEvaluationRule` the C# path writes by hand.
What a body cannot say is what an operator *means*, because that is a computation and this repository has no language for
computation: an `operator` body either writes its symbol itself or names the property that holds one, and
`OperatorSemantics` answers whole-number arithmetic
(including a power), the four comparisons, equality by value, the two logical operators, and `+` over two texts, which
joins them - that last one because a declared rule has no way into C# at all, so a label built out of two parts would
otherwise be inexpressible. Text is joined and not ordered: `<` over two texts is a decision about a culture rather than
about a value, and stays out. Both authoring paths read that one class - the expression vocabulary delegates to it rather
than keeping a copy, so a symbol cannot mean one thing in a declared rule and another in a coded one. A concept whose meaning is a
computation of its own - resolving a call, counting the entries of a slot - keeps a hand-written rule, which is the escape
hatch the aspect being an interface exists for.

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

Four decisions are worth keeping:

- **A concept with no rule has no value.** Unlike a type, there is no `UnknownValue` to fall back on: "nobody
  said what this means" is a failure with a message, not a value. A half-written document therefore stops the
  run where it stopped being meaningful.
- **Nothing is cached.** Asking for the same child twice costs two steps and appears twice in the trace, which
  is what makes the trace a record of what happened rather than of what could have been shared.
- **A logical operator does not evaluate what it does not need.** `false && x` and `true || x` answer without
  evaluating `x`, which is what C# does and what makes a rule mean the same thing in the editor and in the code it
  generates. The trace therefore has no row for an operand that was never evaluated - it is a record of what
  happened, not of what could have been. `OperatorSemantics.ShortCircuits` states the rule once, so the
  hand-written expression rules and a declared operator body cannot disagree about it.
- **Values are records, not objects.** `IntValue`, `FloatValue`, `BoolValue`, `StringValue`, `DateValue`, `RecordValue`
  and `ListValue` compare by content, so an interpreter test is an assertion. The last two carry their comparison in a
  field type of their own (`RecordFields`, `ValueSequence`), because a record's *generated* equality compares its fields and
  a bare list of pairs or of values would compare by identity: a record is a mapping and compares by name whatever order it
  was written in, a list compares entry by entry because its order is part of the answer. `PropertyValue` maps a property
  through its declared `PropertyType.TryParse`, which is the same authority the model validates writes with.

### An evaluator for a language of your own

Nine bodies are enough to answer for a whole language. The worked example is a grade report: `Grade.Report` has a `name`
and the `points` it scored, and the language owes it one of two words. That is one rule.

```text
rule for Grade.Report
  choose
    condition   operator >= over (property points) and (constant int 50)
    then        constant string pass
    otherwise   constant string fail
```

It answers `fail` for 0, 42 and 49 points, and `pass` for 50 and 97. Four things in it are worth copying.

- **The symbol is written on the body.** `operator >=` is what lets a rule compare a slot against a value the rule names:
  a report has no operator property of its own for the body to read one from. Leaving `symbol` empty is the other form -
  `operatorSlot` then names the property whose *value* is the symbol, which is how a language whose operator varies per
  node (arithmetic, `<`, `&&`) is written. A symbol on the body wins over the node's, because the rule is the narrower
  statement of the two.
- **The answers are constants, not concepts.** `pass` and `fail` are written into the rule, so the language's own
  vocabulary does not need them: a word that only an evaluation produces is not a concept, and adding one would make the
  document spell out the answer the meaning is supposed to compute.
- **A refusal is part of the meaning.** A report whose `points` is empty is refused with `'points' of Grade.Report holds
  no valid int.` rather than answering `fail`. That is the interpreter's existing rule - nobody stated a value, so there
  is none - and it is what keeps a half-written document from reporting a result nobody would trust.
- **Nothing about it is C#.** The language's structure is its own declared concepts and the whole of its meaning is the
  rule above; deleting the assembly's evaluation code would remove no part of it.

A rule that has to tolerate an empty slot asks before it reads. `has <slot>` answers whether a property, a child slot or a
reference holds anything, and `count <slot>` how many entries a child slot holds - neither is derivable from the other
bodies, because a body that reads a slot refuses when it is empty and nothing else can stand in a condition. Two meanings
in the Atlas sample are written this way: a map answers how many places it holds (`count places`), and a place answers with
a label only while it has a position (`has at`), because a position is the slot the language leaves empty. **The guard has to
name the slot that really can be empty**: the first version of that rule asked about `name`, which the language gives the
default `unnamed` - so the guard was always true and the test that "passed" was passing for the wrong reason, which is what
a guard on a defaulted property looks like from the outside.

A reference is followed, not only tested. `reference <slot>` answers with what the node in that slot answers, through that
node's own rule - so a language composes a meaning out of other meanings, and a stop answers with the label the place it
points at answers with. Three refusals are separate sentences, because they need different answers: the slot is not a
reference (the rule names the wrong shape), it points nowhere (the document is half-written), or it points at a node the
model does not hold (the reference dangles).

A total is a fold, and no other body could express one: nothing in the vocabulary accumulates, so "the retries this
pipeline allows" had no spelling. `sum over <slot> of <body>` asks each entry for a value and adds them, refusing an entry
that is not a whole number and a total beyond what an `int` holds - a wrapped number would be a wrong answer rather than a
refusal, the same reason a power refuses one. The total of an empty list is zero, unlike a `first` with nothing that
applies: an empty list has an answer and no rule is missing.

A node with several properties answers with a record, and the two bodies that make one useful are `record` (named values,
each of them any body) and `field <name> of <body>` (one value read back out of one). A quantity answers `{ value = 12, unit
= kg }` this way, and an invoice line that holds one answers with the amount alone. Both kinds are values like any other, so
they compare by content, travel through `bind`, appear in the trace and can be compared with `==`.

The four composed meanings in the samples are one chain each, and between them they use every body except the three that
handle a name rather than a value (see below):

- **A total** - `Invoice.Bill` answers `sum over lines of (field value of (child quantity))`: a fold over a child slot, whose
  each-body reads one field of the record the base language answers with. Twelve and five and thirty are added, the units are
  left alone, and the number is `47`.
- **A label** - `Atlas.Place` answers `name + " at " + at` while it has a position, and its name alone otherwise: two texts
  joined, one of them a constant the rule writes, so a language builds a phrase without a single line of C#.
- **A list** - `Atlas.Route` answers `list of stops`, which is the labels of the places the route stops at, in order: the list
  body asks each stop, the stop's rule follows its reference, and the place's rule builds the label.
- **A choice out of a list** - `Workflow.Workflow` answers `first over library when (has command) then (property command)
  otherwise (fail "no step in the library runs a command.")`: the command of the first step in the library that runs one, and
  a sentence when the library holds none. The two documents the folder ships prove both halves without a model built for the
  test - `release.mps` keeps one step in its library and answers `dotnet build`, while `draft.mps` has no library at all and
  answers the refusal.

**Reading a name is two bodies, and the split is the point.** `bind` writes the name it binds as a literal in the rule, and
`variable` reads a name **from a slot of the node** - so the two do not compose, and a rule that binds a value could not read
it back. `bound` is the missing half: it writes the name it reads into the rule, so `bind` and `bound` agree on a name
neither of them takes from the document. `variable` stays for the other case, a name the *run* supplies: the expression
language's `Name` node is exactly that, so a rule for `Name` reads the run's value through the name its document carries.
The first version of the vocabulary had only `variable`, and the pair `bind`/`variable` could only be written on a concept
whose slot happened to hold the name the rule had already written - which is why the lowering test for `bind` had to reach
for the expression vocabulary's `Name` to write it at all.

None of the three appears in a shipped sample, and that is not a gap in the samples: a sample's name slots are labels
(`build`, `Release`), not names a run supplies, and no sample rule needs the same value twice. The three bodies are
demonstrated in `EvaluationRuleLoweringTests`, where a model can be built to say exactly what the body needs.

The example lives as a fixture rather than as a shipped sample, because it exists to prove the path end to end on a
language this repository does not otherwise need: `GradeLanguage` declares the two properties, `SampleEvaluatorTests`
writes the rule and pins the five answers and the refusal.

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
one moves the caret there. The variables come from one line of `name=value` pairs (`VariableInputs`, which the
shell's `RunInputs` hands the line to): a value is read as a number, then a flag, then an `yyyy-MM-dd` date, and
otherwise as text, and a malformed or repeated name is refused as a whole line rather than half-applied. The
parser sits here rather than in the shell because a declared test's `Testing.Evaluates` assertion reads the same
pairs, and a run in a test and a run in the shell must not disagree about what a value is. A run is a snapshot,
so an edit drops the report, exactly as an edit replaces the findings.

**Stepping is a replay, and the vocabulary is now a type.** `TraceCursor` (in
`Projectional.Semantics/Interpretation/`) walks a finished run's trace: it starts *before* the first step (`-1`, so a
reader who has just run a document is at the beginning of the story), and `Step`/`Back` answer whether they could
move rather than throwing, which is what lets a shell drive a button without a second test. Nothing in the
interpreter changed to gain it, and nothing could: the runtime already records every node it evaluated and what came
out, so there is nothing to suspend.

What it is *not*: an enter/exit tracer. The trace is post-order - a child's value is recorded before the parent that
consumes it - so stepping shows a cascade of finished values, not a call stack being pushed and popped. Showing entry
as well would need the interpreter to record a second event, and that is a different feature. The cursor exposes the
`NodeId` and not a caret address, because `Projectional.Semantics` references only `Core` and `Language`; the shell
maps it to a `CellAddress`, exactly as the existing "click a trace row to move the caret" already does.

Still open: the cross-check against generated code, which needs the generator first. The shell's side of stepping is delivered: the
menu's **Step** (F10) and **Step back** mark the row and put the caret on the node, enabled from whether the report in
front carries a cursor. Step back has no gesture on purpose - Shift+F10 is the Windows context-menu key, and claiming it
globally would take the context menu away from every control in the application.

