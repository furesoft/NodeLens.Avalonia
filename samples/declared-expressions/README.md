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
| `expressions.mps` | a document of the language |

## What the types folder says

`Decl.Int` is `type int` and `Decl.Bool` is `type bool`. `Decl.Binary` switches on its `operator`: `==` and `<` answer
`type bool`, and anything else falls back to `type of left`, which is what makes `1 + 2` an `int`. `Decl.If` is
`widest of then and else` - the common supertype of its two branches, so `if 1 < 2 then 3 else 4` is an `int` and a
conditional with a number on one side and a truth value on the other is `unknown` rather than a claim nobody made.

`operands-not-comparable.mps` is a complaint with a condition:

    rule Decl.Binary is
      report error "The operands are not comparable, so no value of one could be used as the other." type of left is not comparable with type of right

It reports only where the condition holds, which is why `1 == true` is a finding and `1 == 2` is not.

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