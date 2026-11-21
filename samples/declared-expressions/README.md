# A language whose types are documents too

The other two declared samples show scopes, intentions and a generator. Neither has a type: their slots are strings,
counts and references, so every node would type as `unknown` and a `types/` folder would be inventing a need instead
of recording one. This folder is the small language that does have types, so the type vocabulary is exercised by a
document someone can open rather than only by the test suite.

| File | What it is |
| --- | --- |
| `structure/` | five concepts: an abstract `Decl.Expression`, an integer and a truth value, a binary form and a conditional |
| `editor/` | one line each - `1`, `true`, `1 + 2`, `if 1 < 2 then 3 else 4` |
| `types/` | the rules: two fixed types, a switch over the operator, a `widest of` and one complaint |
| `typing/` | which node a burst of typing creates: a digit makes an `int`, `true`/`false` make a `bool`, and the aliases `binary` and `if` make those two |
| `import/` | one declared importer: a second spelling of the same expressions, read by `import/sexp.mps` |
| `tests/` | four assertions the language states about itself |
| `expressions.mps` | a document of the language |
| `uncomparable.mps` | a document the complaint reports on |

## What the types folder says

`Decl.Int` is `type int` and `Decl.Bool` is `type bool`. `Decl.Binary` switches on its `operator`: `==` and `<` answer
`type bool`, and anything else falls back to `type of left`, which is what makes `1 + 2` an `int`. `Decl.If` is
`widest of then and else` - the common supertype of its two branches, so `if 1 < 2 then 3 else 4` is an `int` and a
conditional with a number on one side and a truth value on the other is `unknown` rather than a claim nobody made.

`operands-not-comparable.mps` is a complaint with a condition and a code:

    rule Decl.Binary is
      report error "The operands are not comparable, so no value of one could be used as the other." code decl.type.operands type of left is not comparable with type of right

It reports only where the condition holds, which is why `1 == true` is a finding and `1 == 2` is not. The code is the
identifier a report groups the finding under and a suppression keys on; a complaint that states none reports uncoded,
which draws as `code <none>`. That placeholder is the whole reason the slot needs no hiding: a property cell is never
hidden, because the caret has to stand somewhere to write the value.

## What the typing folder says

A typing rule answers the question nothing in the model can: the caret is in an empty `Decl.Expression` slot and the
user presses `4` - which of the four concrete concepts did they mean? Only the author knows, so the author says it.

```
typing Decl.Int claims digits writes value
typing Decl.Bool claims any of true false writes value
```

`Decl.Int` claims a run of digits and writes it into its `value`, and `Decl.Bool` claims the two words and does the
same. Typing `4` into an empty expression slot therefore produces `4`, and typing `true` produces `true`, instead of the
keystroke being refused for lack of an answer.

Two more rules claim an **alias** rather than a text: `Decl.Binary` declares `binary` and `Decl.If` declares `if`, so
typing either word in an empty slot creates that node with its slots still empty. That is the claim that names no word
of its own - the word lives on the concept declaration, which is the one the completion list offers, so the list and
the keystroke cannot disagree. `bin` is not `binary`: an alias is claimed whole, not as a prefix.

The two remaining claims - `starts with` and `name` - are unused here because an expression has no name slot, and no
construct in this language is entered by a leading character alone.

Without this folder the language still opens and edits; it just claims nothing, and the completion list stays the only
way into an empty slot.

## What the tests folder says

Four rows, one assertion per file because a test names one assertion:

| File | Assertion |
| --- | --- |
| `types.mps` | the literal `3` in `expressions.mps` is `int` |
| `operator-type.mps` | the operator `<` of `expressions.mps` is `bool`, which is the switch over the operator |
| `clean.mps` | `expressions.mps` reports nothing |
| `reports.mps` | `uncomparable.mps` reports exactly one finding, `decl.type.operands` as an error |

**Run > Run tests** runs them, and so does `TestRunner.Run(language)`. They are the reason the runner hands the
constraint engine the language's own scopes and types: a complaint that asks a slot for its type reads `unknown`
without them and stays silent. `reports.mps` is the row that notices - it is the one that fails when that plumbing is
removed - while `clean.mps` would pass either way, which is exactly the trap: a "reports nothing" assertion is true
whether the engine could see or not.

A row names the node by the text the document *shows and the user can change*, so it reaches a property cell - the
literal `3` or the operator `<`. A keyword is drawn from the notation, not from the document, so no row can name the
conditional itself by the `if` it draws; the `widest of` rule is therefore pinned by
`DeclaredExpressionsTests.AConditionalIsAsWideAsItsTwoBranches` rather than from here.

## What the import folder says

`import/sexp.mps` is one importer, written in the importer language, that reads a *second* spelling of the same
model - which is the case a declared rule exists for, because inverting a generator only reads what the generator
writes and this language ships no generator at all:

    (if (binary < (int 1) (int 2)) (int 3) (int 4))

Four rules, one per concrete concept, all of them anchored by their keyword - `(int `, `(bool `, `(binary `, `(if ` -
which is what makes the choice decidable without backtracking: the reader looks at the text and takes the one rule
whose anchor stands there. The operator is a declared token class (`Word` plus `+-*/<>=!`), the numbers use the
built-in `Number` class, and `Space` is declared as *skipped*, so whitespace costs no rule.

Open the folder in the app and the importer appears as an **import** node in the project explorer; **File ▸ Import…**
offers it for any file that starts like one of those four rules.

## Open it

In the standalone editor: **File ▸ Open language...**, choose this folder, then **File ▸ New document of an opened
language**. It is not compiled into the app - like the other declared samples it is what a user opens.

From code:

```csharp
var language = LanguageProject.Open("samples/declared-expressions", out var error);
var model = ModelFile.Load(language!.NewProject(), "samples/declared-expressions/expressions.mps");

// language.Types      the four rules
// language.Typing     what a burst of typing creates
// language.Constraints the one complaint
```

`DeclaredExpressionsTests` loads exactly these files, so the sample cannot drift from what the code accepts without a
test failing.
