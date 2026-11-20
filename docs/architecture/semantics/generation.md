# Generation

> Part of [semantics.md](../semantics.md), the design of record for the semantic aspects. The pass order,
> the diagnostics, the error recovery and the performance numbers stay in the parent.

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
([model-generation.md](../model-generation.md)): a target with its own concepts - the projected C#
base language - cannot be printed from templates without keeping its grammar in string
fragments and leaving its checks unrun. The earlier decision to leave that stage out was right
while every consumer was text: it doubles the conceptual surface, and only a target that *is* a
language pays for that.

Generation is only half of the text story. The other half is import, which is the single
place where a parser exists and the only path by which text becomes a model
([import.md](../import.md)). The two are tested as a pair: `generate(import(text))` must equal
`text` for text the reference languages map completely, which is a property of the *text*, not
of the model - import mints fresh node ids, so the models are never equal.

Two consumers of the same template tree are planned on top of this: projected C# as a base
language, whose generator has to parenthesise by precedence ([csharp.md](../csharp.md)), and the
JSON, XML, TOML and HOCON reference languages, which exercise it in both directions.

### What is implemented

The template is data in `Projectional.Language/Generation/`: `TextTemplate` is an ordered list of `TemplatePart`s -
`TextPart` (a literal), `PropertyPart` (a slot's value, verbatim), `ReferencePart` (a reference's target name, or
the stand-in its language names), `ChildPart` (the single child's text),
`ChildrenPart` (every child of a list, joined by a separator *template*), `LoopPart` (a body applied per child),
`IndentPart` (one level deeper), `LineBreakPart` (a newline plus the current indentation), `ConditionalPart` (a
property/value choice), `WhenFilledPart` (a choice on whether a slot holds anything), `ParentPart` (a property of the
node that holds this one) and `SelfPart` (the node's own
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

`ParentPart` is the twelfth and the only part that reads a node **other** than the one being expanded: a property of
the node that holds it. A template that walks into a child loses the child's place, so before this part a fragment
could not name what it belonged to - the SQL schema example could write the columns a key points at but never the
table the key hangs on, and its DDL stayed a fragment. The slot is named rather than inferred, like every other part
(`ParentPart("name")` is how a name is read, because a role is a property the holding concept happens to declare), the
outermost node emits nothing because nothing holds it, and the emitted characters are recorded as a region of the
**holding** node - that is where they came from, so that is where "go to source" lands. It is also the one part whose
slot a load cannot check: the holding concept is not declared anywhere, so the check runs in the engine and names both
concepts. The declared twin is `TextGen.Parent`, drawn as `^slot`.

The walk is `Generator` in `Projectional.Semantics/Generation/`: it owns the indentation, a depth guard against a
template that expands itself, and the **traceability map**. Every emitted stretch is recorded as a
`GeneratedRegion(Node, Slot, Start, Length)`, and `GeneratedText.RegionAt(offset)` answers what wrote a character -
which is what makes "go to source" from generated text possible without a second generator, and what the shell uses to
let a click on a generated line move the caret to the node that wrote it.

- **A concept with no template is a failure; an empty template means "emits nothing".** The first is "nobody said what
  to emit", the second is a decision, and collapsing them would leave a silent hole in the output.
- **An entry that emits nothing takes no separator with it.** `ChildrenPart` and `LoopPart` write each entry into a
  buffer first, because whether it produced anything is only known afterwards and a separator already written cannot be
  taken back. Without that, "this concept is ignored" would mean two different things depending on where the node
  stands: nothing at all at a single-child slot, and a stray comma in a list - a syntax error dressed up as a feature.
  A region written into a buffer learns where the buffer ended up, so "go to source" still points at the right
  character.
- **The decision to emit nothing is declarable.** `TextGen.Ignore` is a mapping body beside `TextGen.Template` (both
  conform to the abstract `TextGen.Body` the mapping's `template` slot is declared over), and it lowers to the empty
  template. A separate concept rather than an empty template node because an empty box reads as unfinished work: an
  author has to be able to say it on purpose.
- **A missing template is also a finding, not only a stopped run.** `GenerationCoverage.Of(model, generator)` lists what
  the chosen generator cannot print - one finding per concept, at its first node in document order, because the defect
  belongs to the language and not to each occurrence. That is what lets an editor mark the document instead of
  surprising its author with a failure when they press Generate. It carries no repair, so it is a diagnostic and not a
  fix: writing a template is an author's decision, and no edit action can guess it.
- **A language with several generators is asked once per generator.** `GenerationCoverage.Of(model, generators)` is the
  overload a shell uses, because a document can be complete for one target and not for another - and the message names
  the generator that stops, so a user reading a mark knows which menu entry to avoid. A language that declares no
  generator reports nothing: "cannot be printed" is not a defect of a document there is no target for.
- **The shell shows it, through the same list as everything else.** `DocumentFindings.Of(..., reports:)` is the join:
  reports are the caller's findings that carry no repair, appended after the constraint findings, so `PublishFindings`
  hands the coverage of *every* generator the document offers to the markers and the problems panel. One list stays one
  list - a shell that drew coverage in a second place would be a second answer to "what should I look at".
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
A mapping's body is either a `template` or `ignore` - the second is the declared spelling of "this concept emits
nothing", so a language whose target cannot express one of its concepts writes the decision down instead of leaving an
empty box that reads as unfinished work.
The parity rule of ADR 0014 is executed by `TextGenLoweringTests`, which asserts a declared generator produces exactly
the text of its hand-written twin.

Not implemented: the model-producing second stage, planned in
[model-generation.md](../model-generation.md); and precedence-aware parenthesising, which is the C# base language's job
([csharp.md](../csharp.md)).

**A value is emitted verbatim, and that is a boundary rather than a gap.** A template that puts a string inside quotes
does not escape it, and the framework deliberately offers no "as JSON" part: spelling a format is the business of the
language that owns that format, in its own generator, and converting a model into JSON, XML, TOML or HOCON means
producing a **model of that language** and letting *its* generator print it
([import.md](../import.md#converting-a-model-into-one-of-these-languages)). A per-value escaping part would be a second
answer to "how is this value written" - one in the language that owns the format and one in every language that prints
it - and it would escape at the wrong moment anyway, because the source template runs before the target is chosen.

