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

## Open it

In the standalone editor: **File ▸ Open language...**, choose this folder, then **File ▸ New document of an opened
language**. It is not compiled into the app - like the other declared samples it is what a user opens.

From code:

```csharp
var language = LanguageProject.Open("samples/declared-expressions", out var error);
var model = ModelFile.Load(language!.NewProject(), "samples/declared-expressions/expressions.mps");

// language.Types      the four rules
// language.Constraints the one complaint
```

`DeclaredExpressionsTests` loads exactly these files, so the sample cannot drift from what the code accepts without a
test failing.
