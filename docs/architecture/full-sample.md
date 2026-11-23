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
| `behaviour/` | one document: `Atlas.Stop` is called after the place it stops at, which is the name a reference to a stop shows | a computed name on a concept with no name slot, lowered before the registry exists |
| `editor/` | one line per concept, the place list with a separator and a group gap, a conditional cell on `Kind`, a table for the map's own summary | the notation cells: lines, collections, conditionals, tables, placeholders |
| `types/` | a fixed type, a switch over `Kind`, a `widest of` over two children, one complaint | type rules and a finding that comes from the type system rather than a constraint |
| `typing/` | digits make a coordinate, `place`/`route` are claimed as aliases, `any of` for the two kinds | the four claims, including the alias the completion list also offers |
| `evaluation/` | a place answers with a label built from its name and position, a stop with the label of the place it points at, a map with how many places it holds, a route with the list of its stops' labels | the declared evaluation bodies: a guard, joined text, a delegation through a reference, a count over a slot and a list of one |
| `constraints/` | a route with fewer than two stops is reported; a landmark cannot be its own neighbour | the declared constraint bodies, with a condition that gates one of them |
| `scopes/` | `neighbours` and a route's `stops` see the places of their own map, and the names the accessory model brings | scope providers: local, ancestor and a library |
| `intentions/` | `Add a stop` (insert into a list) and `Mark as landmark` (set a property) | the intention bodies the declared form can state |
| `generator/` | one generator writing the GeoJSON-shaped spelling | text generation from templates, including a loop and a conditional |
| `import/` | one declared importer reading that same spelling | text import written as rules, with a skipped token class |
| `accessory/` | `known-places.mps`: three `Atlas.Place` nodes every project of the language starts with | accessory models: a reference that points at a library node and survives save and load |
| `tests/` | one assertion per kind: `draws`, `round-trips`, `reports`, `generates`, `script`, a node-addressed finding | the whole test vocabulary |
| `map.mps` | a document that uses a place from the accessory, a route over two places and a landmark | what a user opens |

Aspects a folder cannot hold are named rather than faked: an `Evaluate` rule, a hand-written `ICheck` and a behaviour
*method* - the C# a concept's logic is written in, [csharp.md](csharp.md) - are C# beside the language (`Checks/`,
`Intentions/`, a package), so the full sample does not claim them. Where one of those exists in the per-axis samples it
stays there. A concept's *name* is not one of them: it is declared in `behaviour/`, and the sample carries one.

## The rule: a new aspect extends this sample

**An aspect the framework gains is added to `samples/declared-atlas` in the same commit that adds the aspect.** The
alternative is what this repository already had three times over: a delivered aspect with no sample (`accessory/`), and
an aspect no shell could open (`typing/`). The rule is a test rather than a habit:

```csharp
[Fact]
public void TheFullSampleDeclaresEveryAspectTheFrameworkKnows()
{
    var language = Language();                                  // opened with the base it stands on
    var folder = Path.Combine(AppContext.BaseDirectory, "Atlas");

    // The one list, so an aspect added to `AspectFolders` is an aspect this sample must carry.
    Assert.DoesNotContain(LanguageProject.AspectFolders, aspect => !Directory.Exists(Path.Combine(folder, aspect)));

    // And the other direction: a folder the framework does not name is a folder nothing opens.
    Assert.DoesNotContain(
        Directory.EnumerateDirectories(folder).Select(Path.GetFileName),
        aspect => aspect != TestRunner.Folder && !LanguageProject.AspectFolders.Contains(aspect!, StringComparer.Ordinal));

    // A folder is not an aspect: each one has to have lowered into something the language uses.
    Assert.True(language.HasNotation && language.HasTypes && language.HasTyping && language.HasConstraints);
    Assert.True(language.HasScopes && language.HasIntentions && language.HasGenerators && language.HasDeclaredImporters);
    Assert.NotEmpty(language.Accessory);

    // The tests folder is beside the aspects rather than in that list, and it is an aspect all the same.
    Assert.True(TestRunner.Run(language).Succeeded);

    // And the root states the one language this one was built on.
    Assert.Equal(["Units"], language.Dependencies.Select(dependency => dependency.Name));
}```

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
2. **types + typing** - **delivered**: one declared record type (`place`, two `string` fields) and three rules that
   name it - a place, a landmark and a stop are the same thing to anything reading a route - plus two claims (letters
   are a place's name; a landmark is claimed by `lake`, `peak` or `pass`). `DeclaredAtlasTests` asserts the type of
   nodes in both documents and both claims.
3. **constraints + scopes** - **delivered**: two findings (a place still called `unnamed` as a hint, a stop that points
   nowhere as an error) and three name sources - `local` for a map's entry, `ancestors` for a stop's target and
   `project` for a route naming the map of another document. The structure grew what there was to say something about:
   a map now holds its routes, and a route names the map it lies on. The slice also pinned what the five places mean,
   which nothing said before: `local` is the node's children and siblings, `ancestors` the children of each enclosing
   node, and `model`/`project` are the **roots** of the document and of the project's other documents - so a nested
   place is never reachable through `model`, and the sample says so in its README.
4. **intentions + generator + import** - the two offers, the text out and the text back in (the generator writes what
   the importer reads, which is the parity that keeps a spelling in one place). **Delivered, with one limit found by
   doing it**: `intentions/` holds the two offers (add the first stop to an empty route, delete the stop under the
   caret), `generator/` holds `AtlasText` (the map, its places, its route and its stops, one line each) and `import/`
   holds `places`, a declared format that reads a map and its places back. The limit is the parity itself: **an
   importer cannot read a reference back.** A template may write one, but the importer derived from that template
   refuses it ("writes something the node does not own and has nothing to read into"), and a declared rule fills
   properties and children only - so `AtlasText` is write-only and `places` is the spelling that can be read, without
   the entry and without the stops. Two smaller findings: a `TextGen.WhenFilled` is refused without an `otherwise`
   template even when the empty case writes nothing (an empty template is the answer), and a reference part writes
   `<none>` for an unset reference, so a conditional is what keeps that out of the text.
5. **accessory + tests + `map.mps`** - **delivered**: `accessory/known-places.mps` is an ordinary model of the language
   that every project starts with (three places, stable identities), the route document starts at one of them through
   the `project` scope - the demonstration the scope table asked for - and `tests/` holds three of the framework's own
   assertions (a round trip, the generated text of the map, and that the map reports no findings).
   `TestRunner.Run(language)` is what the test asserts on, so the sample's assertions are the framework's, not a copy
   of them. The accessory also made the shell's two sample guards skip a folder for the first time: they open every
   `.mps` of every aspect with the shell's language, and an accessory document is written *in* the language being
   defined, so it opens with that language and never with the shell - the same reason the shell's list of definition
   kinds leaves the accessory out.
6. **the guard test and the documentation** - **delivered**: `TheFullSampleDeclaresEveryAspectTheFrameworkKnows` reads
   `LanguageProject.AspectFolders` in both directions (no aspect without a folder, no folder the list does not name),
   asserts that every aspect lowered into something the language uses rather than merely existing as a directory, runs
   the sample's own tests, and states the one language it is built on. The README, this document and a row in
   `implemented.md` were finished with it, and the build's copy rule is what puts the folder behind **File ▸ Open
   examples**. It was proved by deleting `accessory/`: the guard goes red, together with the two tests that use the
   library.

Slices 2 to 6 are open. Until the last one lands the sample is incomplete on purpose, which is why the guard test
arrives with it rather than now: a guard that had to be told which aspects are still missing would be a second list to
keep in step with this one.

## What this document is not

It is not a second roadmap. The per-axis samples stay, the three worked examples ([worked-examples.md](worked-examples.md))
stay, and nothing here replaces a check in `Projectional.Checks` - a declared sample cannot declare C# behaviour, and
that gap is named in the table rather than closed by a workaround.
