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
one moves the caret there. The variables come from one line of `name=value` pairs (`VariableInputs`, which the
shell's `RunInputs` hands the line to): a value is read as a number, then a flag, then an `yyyy-MM-dd` date, and
otherwise as text, and a malformed or repeated name is refused as a whole line rather than half-applied. The
parser sits here rather than in the shell because a declared test's `Testing.Evaluates` assertion reads the same
pairs, and a run in a test and a run in the shell must not disagree about what a value is. A run is a snapshot,
so an edit drops the report, exactly as an edit replaces the findings.

Still open: stepping (a UI vocabulary, open question 11), value kinds for records and lists (they arrive with
the first language that evaluates one), and the cross-check against generated code, which needs the generator
first.

