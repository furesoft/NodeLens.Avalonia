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
| structure | `structure/` | an abstract concept, inheritance, a reference, two root concepts, a borrowed concept |
| editor | `editor/` | a sequence with an indented child list, an inline line and a reference cell |
| types | `types/` | a declared record type (`place`) and three rules that name it, so three concepts answer one type |
| typing | `typing/` | two claims: letters in an empty slot are a place's name, and a landmark is claimed by the words it is usually called |

The remaining aspects - constraints, scopes, intentions, generator, import, accessory and the framework's own tests -
arrive one slice at a time; each one is added here and in the plan's aspect table.

## The documents

`map.mps` is one map with a place and a landmark; `route.mps` is one route whose two stops point at them.