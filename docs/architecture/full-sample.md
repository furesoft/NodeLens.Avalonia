# The full sample: one language that shows everything

**Status: planned.** Nothing of `samples/declared-atlas` exists yet. This document is the spec a session
implements, and the rule that keeps it honest afterwards.

## Why one sample and not only the per-axis ones

The declared samples explain one axis each - `declared-pipeline` the M2 gate, `declared-workflow` references and
intentions, `declared-invoice` resolution, `declared-expressions` types and typing, `declared-units` a base without a
document. That is the right shape for *reading* a feature. It is the wrong shape for proving that the features compose,
because every sample can be green while the combination of two aspects is broken: an accessory model has only ever been
tested against a fixture in `LanguageProjectAccessoryTests`, a types folder has never met a scope rule, and nothing in
the repository has ever loaded one folder that holds all of them.

So the repository gains a second kind of sample: **one language that declares every aspect at once**, loaded by the
tests the way a user loads it. The per-axis samples stay - they are documentation - and this one is the regression
harness.

## The language: `samples/declared-atlas`

A map of places and routes, because that shape needs a name index, a list, typed values and a library:

```
map Berlin
  place Brandenburg Gate : landmark at 52.516, 13.378
  place Museum Island   : sight    at 52.517, 13.401
  route City walk       : Brandenburg Gate -> Museum Island
```

| Aspect | What it holds | Which engine surface it proves |
| --- | --- | --- |
| `language.mps` | name `Atlas`, version 1.0.0, `depends on Units` | the root: identity, a declared dependency, the vocabulary it borrows (`Units.Quantity` as a child slot type) |
| `structure/` | `Atlas.Map` (root), `Atlas.Route` (root), an abstract `Atlas.Spot`, `Atlas.Place` and `Atlas.Landmark` extending it, an enumeration `Atlas.Kind` | concrete, abstract and enumerations; inheritance; a slot over an abstract concept; a slot typed by a borrowed concept |
| `editor/` | one line per concept, the place list with a separator and a group gap, a conditional cell on `Kind`, a table for the map's own summary | the notation cells: lines, collections, conditionals, tables, placeholders |
| `types/` | a fixed type, a switch over `Kind`, a `widest of` over two children, one complaint | type rules and a finding that comes from the type system rather than a constraint |
| `typing/` | digits make a coordinate, `place`/`route` are claimed as aliases, `any of` for the two kinds | the four claims, including the alias the completion list also offers |
| `constraints/` | a route with fewer than two stops is reported; a landmark cannot be its own neighbour | the declared constraint bodies, with a condition that gates one of them |
| `scopes/` | `neighbours` and a route's `stops` see the places of their own map, and the names the accessory model brings | scope providers: local, ancestor and a library |
| `intentions/` | `Add a stop` (insert into a list) and `Mark as landmark` (set a property) | the intention bodies the declared form can state |
| `generator/` | one generator writing the GeoJSON-shaped spelling | text generation from templates, including a loop and a conditional |
| `import/` | one declared importer reading that same spelling | text import written as rules, with a skipped token class |
| `accessory/` | `known-places.mps`: three `Atlas.Place` nodes every project of the language starts with | accessory models: a reference that points at a library node and survives save and load |
| `tests/` | one assertion per kind: `draws`, `round-trips`, `reports`, `generates`, `script`, a node-addressed finding | the whole test vocabulary |
| `map.mps` | a document that uses a place from the accessory, a route over two places and a landmark | what a user opens |

Aspects a folder cannot hold are named rather than faked: an `Evaluate` rule, a hand-written `ICheck` and a behaviour
method are C# beside the language (`Checks/`, `Intentions/`, a package), so the full sample does not claim them. Where
one of those exists in the per-axis samples it stays there.

## The rule: a new aspect extends this sample

**An aspect the framework gains is added to `samples/declared-atlas` in the same commit that adds the aspect.** The
alternative is what this repository already had three times over: a delivered aspect with no sample (`accessory/`), and
an aspect no shell could open (`typing/`). The rule is a test rather than a habit:

```csharp
[Fact]
public void TheFullSampleDeclaresEveryAspectTheFrameworkKnows()
{
    var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Atlas"), out var error);

    Assert.Null(error);
    Assert.NotNull(language);

    // The one list, so a folder added to LanguageProject.AspectFolders is a folder this test demands the sample holds.
    var missing = LanguageProject.AspectFolders
        .Where(folder => !Directory.Exists(Path.Combine(language.Directory, folder)))
        .ToList();

    Assert.Empty(missing);

    // And each aspect is not merely a folder: it lowered into something the language uses.
    Assert.True(language.HasNotation);
    Assert.True(language.HasTypes);
    Assert.True(language.HasTyping);
    Assert.True(language.HasConstraints);
    Assert.True(language.HasScopes);
    Assert.True(language.HasIntentions);
    Assert.True(language.HasGenerators);
    Assert.True(language.HasDeclaredImporters);
    Assert.NotEmpty(language.Accessory);
    Assert.Equal(2, language.Dependencies.Count);   // nothing, and Units
}
```

The first half is what makes the rule self-enforcing: `LanguageProject.AspectFolders` is the list the explorer draws
and the shell opens, so an aspect added there turns this test red until the sample carries it. The second half is the
part a folder cannot fake - a `types/` directory that lowered to nothing would still pass the `Directory.Exists` check.

## How it is built, in slices

Each slice is a commit that leaves the repository green; the guard test arrives with the last one, because until then
the sample is incomplete by design.

1. **structure + editor + `language.mps`** - **delivered**: six concepts (`Spot` abstract, `Place`, `Landmark`,
   `Stop` with a reference to a place, and the roots `Map` and `Route`), one notation each, a root that names the
   language, says what it is for and declares the dependency on `Units`, and the two documents `map.mps` and
   `route.mps`. `DeclaredAtlasTests` pins the load, the drawing of both documents and the byte-identical round trip
   of every file.
2. **types + typing** - the rules and the claims, with `DeclaredAtlasTests` asserting one type and one typed burst.
3. **constraints + scopes** - the two findings and the three name sources.
4. **intentions + generator + import** - the two offers, the text out and the text back in (the generator writes what
   the importer reads, which is the parity that keeps a spelling in one place).
5. **accessory + tests + `map.mps`** - the library nodes, one document, and the `tests/` folder with one assertion per
   kind.
6. **the guard test and the documentation** - `TheFullSampleDeclaresEveryAspectTheFrameworkKnows`, the README, a row in
   `implemented.md`, and the sample added to `File > Open examples` by the build's copy rule.

Slices 2 to 6 are open. Until the last one lands the sample is incomplete on purpose, which is why the guard test
arrives with it rather than now: a guard that had to be told which aspects are still missing would be a second list to
keep in step with this one.

## What this document is not

It is not a second roadmap. The per-axis samples stay, the three worked examples ([worked-examples.md](worked-examples.md))
stay, and nothing here replaces a check in `Projectional.Checks` - a declared sample cannot declare C# behaviour, and
that gap is named in the table rather than closed by a workaround.
