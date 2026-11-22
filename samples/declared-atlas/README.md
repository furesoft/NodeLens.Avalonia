# Atlas - the full sample

Atlas is the sample language that declares **every aspect the framework knows**, so that a new aspect has a place
to be shown and a reader has one folder to look at instead of seven. It is planned aspect by aspect in
[docs/architecture/full-sample.md](../../docs/architecture/full-sample.md).

## What it is

A map holds places; a route stops at them. `Atlas.Spot` is the abstract set a map's places are declared over,
`Atlas.Place` is a spot with a name and a position, `Atlas.Landmark` is a place famous enough to need no kind, and
`Atlas.Stop` is one link of a route - a reference to a place.

It depends on **Units** (1.0.0) and borrows that language's `Units.Quantity` for a place's height, which is why the
folder also proves that a language may be built on another language's concepts.

## What is declared so far

| Aspect | Folder | What it shows |
| --- | --- | --- |
| root | `language.mps` | the name, the version and one declared dependency |
| structure | `structure/` | an abstract concept, inheritance, references, two root concepts (one of them nestable), a borrowed concept |
| editor | `editor/` | a sequence with an indented child list, an inline line and a reference cell |
| types | `types/` | a declared record type (`place`) and three rules that name it, so three concepts answer one type |
| typing | `typing/` | two claims: letters in an empty slot are a place's name, and a landmark is claimed by the words it is usually called |
| constraints | `constraints/` | two findings: a place that is still called `unnamed` (hint) and a stop that points nowhere (error) |
| scopes | `scopes/` | four name sources: a map's entry is `local`, a stop's target comes from `ancestors`, a route names its map and the place it starts at through the `project` - the second of those is a library node |
| intentions | `intentions/` | two offers: add the first stop to an empty route, delete the stop under the caret |
| generator | `generator/` | `AtlasText`, the language as lines: the map with its places and its route, one stop per line |
| import | `import/` | `places`, a declared format that reads a map and its places back |
| accessory | `accessory/` | `known-places`: three places every project of the language starts with, and a route that starts at one of them |
| tests | `tests/` | the language's own assertions, run by the framework: a round trip, the generated text, and that the map reports nothing |

`import/` reads a **different** spelling than the generator writes, and that is the interesting part: an importer
cannot read a reference back. A generator's template may write one, but the importer derived from that template refuses
it - "it has nothing to read into" - and a declared rule fills properties and children only. So `AtlasText` is
write-only (`DeclaredAtlasTests` pins the refusal), and `places` is the declared format that can be read: a map line
and one line per place, with no entry and no stops, because those are references.

That is every aspect the framework knows, and `DeclaredAtlasTests.TheFullSampleDeclaresEveryAspectTheFrameworkKnows`
keeps it that way: it reads `LanguageProject.AspectFolders` - the one list the explorer and the shell open - and refuses
the folder if an aspect is missing, if a folder exists that the list does not name, or if an aspect lowered into
nothing. So a new aspect in the framework is a red test here until this folder carries it.

The three name sources are the three that mean something different here: `local` is the node's own children and
siblings, `ancestors` is the children of each enclosing node, and `model` and `project` offer the **roots** of the
document and of the project's other documents. A place nested in a map is therefore reachable through `local` or
`ancestors` and never through `model`, which is why the sample uses the three it does.

## The documents

`map.mps` is one map with two places and a route that stops at both of them; `route.mps` is a second route, in its own
document, which names the map it belongs to across the project boundary.