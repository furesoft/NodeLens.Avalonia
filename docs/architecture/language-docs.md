# Interactive language documentation

**Status: planned, not built.** This document is the design a session implements; nothing here exists yet beyond the
inputs it reads. The dependency requirement below is **decided** rather than proposed: a language's documentation
contains the documentation of every language it stands on.

## What it is for

A language that lives in a folder is readable by whoever wrote it and by nobody else. The registry knows everything a
reader needs - the concepts, their slots, what inherits from what, which notation draws them, what the constraints say -
but it is only visible through the editor, one document at a time. The documentation generator turns a
`LanguageProject` into a folder of static HTML a team can open in a browser, link into a wiki, or ship beside a
package.

A language is rarely alone. `declared-invoice` extends `Units`, `declared-atlas` depends on it and borrows
`Units.Quantity`, and a shipped language may stand on another shipped one. A reader who lands on `Atlas.Stop` and sees
a reference into `Units.Quantity` has to be able to follow it - so the documentation of a language is the
documentation of its whole chain, not of one folder.

**Static files with a script, not a server.** The output is a folder that works from `file://`, from a web server, and
inside a zip. That rules out anything that needs a process: no search endpoint, no live rendering. What makes it
*interactive* is therefore client-side and small - a search box that filters the concept index, collapsible sections,
anchors that link into a page, and a hover that shows a concept's description where its key is used.

## The chain: what a language stands on is documented with it

**The rule.** `LanguageDocumentation.Generate(language)` answers the pages of `language` *and* of every language it
resolved against, base first - the order `LanguageProject.Resolved` already promises and `LanguageExporter` already
walks. The chain is followed downward only: a language's documentation never grows the languages that depend on *it*,
because that set is the whole project and would be unbounded.

**Shipped bases are included, unlike in an export.** The exporter skips a base that ships with the host - `CSharp` is
the one today - because there is no folder of a user's to write a *package* into: the host already ships it. Documentation
has no such constraint. A shipped language is a `LanguageProject` with declarations, notations and type rules like any
other, so its pages can be generated from the instance the resolver answered, and a DSL that writes `depends on CSharp`
must document the vocabulary it borrowed. This is the one place the two features answer differently, and the reason is
the difference between "a package to install" and "a page to read": the exporter asks what a consumer still needs,
the documentation asks what a reader is looking at.

**One folder per language.** Every language gets `<Language>/` holding its own `index.html`, `concepts/`,
`aspects.html` and `examples.html`. Two languages cannot share one directory: both would have an `index.html`, and a
concept key is only unique within its language by convention rather than by rule.

```
docs/
  index.html                  the chain: the language, what it stands on, one search over all of it
  assets/docs.css, docs.js    one stylesheet and one script for the whole tree
  Atlas/
    index.html                Atlas' concepts, grouped by inheritance root
    concepts/Atlas.Stop.html
    aspects.html
    examples.html
  Units/
    index.html
    concepts/Units.Quantity.html
    aspects.html
    examples.html
```

**Links cross languages, they never inline.** A concept page links to its super concepts, its interfaces, its slot
targets and its reference targets wherever they live: `Atlas/concepts/Atlas.Stop.html` links to
`../Units/concepts/Units.Quantity.html`. The link is computed from one map - language name to folder - built once for
the chain, so a page cannot be written with a path the tree does not have. A key that belongs to no language in the
chain is drawn as text with no link and marked as declared but unresolved, which is the same honesty the module
settings dialog follows.

**A base's concept has exactly one page, and it is the base's.** Copying `Units.Quantity` into Atlas' folder would give
a reader two descriptions of one concept, and the copy would be the one that goes stale - the same reason the export
writes one package per language instead of one merged package. What a dependent's folder holds is its own concepts; what
it holds of its bases is the link, the dependency row, and the search entry.

**One search over the whole chain.** The search box on the root page indexes every concept of every language in the
chain, with the language's name as a second column. A search that covered only the language the reader opened would
fail exactly the reader who typed `Quantity` while reading about stops.

**A declared dependency that nothing answered is a fact, not an omission.** The root page lists every name the root
declares, with the version it asked for and whether the host resolved it. A dependency the host could not answer is a
row saying so - never a silently missing section.

**A cycle is refused rather than walked.** Loading already refuses one; the generator asserts that no language appears
twice in the chain and says which name closed the loop, because a generator that recursed would write pages until the
disk filled.

## What it contains

| Page | Content |
| --- | --- |
| `index.html` | the chain: the language's name and description, what it declares it stands on (with the version asked for and whether it resolved), a link into each language's pages, and a search box over every concept of the chain |
| `<Language>/index.html` | that language's concepts grouped by inheritance root, with the search box filtered to it |
| `<Language>/concepts/<key>.html` | one page per concept: key, name slot, description, super concepts and interfaces with links, every slot as a row (kind, key, type or target, cardinality, default), and the notation that draws it in the notation language's own spelling |
| `<Language>/aspects.html` | what lowering produced from each aspect folder: concepts, notations, type rules, constraints, scope rules, intentions, generators, importers - the same facts the module settings dialog shows, in a page a reader can link to |
| `<Language>/examples.html` | one example per concept the language can root, rendered by the projection's headless snapshot renderer, so the page shows what the concept *looks like* rather than describing it |
| `assets/docs.css`, `assets/docs.js` | the one stylesheet and the one script, both deterministic, both small enough to read |

Two rules come from the repository's own invariants:

- **The output is derived and byte-stable.** Same chain in, same bytes out - no timestamps, no machine-specific paths,
  no hash-ordered lists. Languages in chain order, concept pages by key, slots by declaration order, because those are
  the orders the registry already promises.
- **The pages never claim more than the model says.** A concept with no notation says so; a language with no
  constraints lists none; a declared dependency nothing answered says that. An empty aspect is a fact, not an omission.

## Where it lives

`src/Projectional.LanguageDocs/` - a new package, because it is a product a host can reference and call
(`LanguageDocumentation.Generate(language)` and `LanguageDocumentation.Write(language, directory)`), and because every
project under `src/` is a package ([ADR 0022](../adr/0022-every-project-is-a-package.md)). Adding it means the CI
`pack` job's package count moves from 34 to 35 in the same commit.

It references `Projectional.Languages` (the lowered project and its resolved chain) and `Projectional.Projection` (the
snapshot renderer for the example pages) - both downward arrows, so nothing new points up. It must **not** reference
Avalonia: documentation is generated by a build, a test, or a CLI, never by a window.

The shell gets one command for it - **File ▸ Export documentation…** - written like `LanguageExportWriter`, which is
the pattern this repository already has for "a menu entry that writes a folder".

## Packaged with the language

**Decided: a package carries the documentation of every language it holds.** `File > Package...` already writes one
package per language in the chain, so the archive is the one place a reader gets a language *and* its documentation:

- The generator runs once for the chain - the exported language and every language it was resolved against, base
  first, exactly the list the export already answers - and the tree above goes into the archive under `docs/`.
- The archive keeps the two halves it has: the sources variant holds the C# and the pages, the assemblies variant
  holds the DLLs and the pages - documentation depends on neither, and a reader of a binary is the one who needs it
  most.
- The output is needed as **values before it is needed as files**: `Generate` answers the pages (path and text), and
  `Write` is the folder caller for **File > Export documentation...**. The package consumes `Generate`; the folder
  command consumes `Write`. That is the same split `LanguageExporter`/`LanguageExportWriter` already follows, and it is
  why the generator belongs in a library package rather than in the shell.
- The pages stay byte-stable, so an archive stays byte-stable: the export already promises that, and a package that
  differed by the clock would break it at the last step.

## Decisions taken

1. **SVG, not a bitmap.** The snapshot renderer produces a layout, not an image. Drawing the notation as inline SVG
   keeps the output text - diffable, searchable, no rendering backend - and keeps the product's premise that a drawing
   is derived from the model.
2. **The search index is inlined** into the root page, capped. A separate `assets/index.json` would need a fetch, and
   `file://` refuses fetches; a language with thousands of concepts is a language whose index needs its own file, and
   that is a decision to take when one exists.
3. **The notation is drawn with placeholders**, the way the editor draws an empty slot, and the page says that it is
   the shape rather than a document. Synthesising children to fill a slot is impossible in general - a child slot with
   a minimum has no obvious content.
4. **An example is the tests' models when the language has them**, and a placeholder otherwise. A language that ships
   `tests/` already holds models that draw, which is real pictures for no synthesis; a language without them gets the
   placeholder and the page says so.
5. **Shipped bases are documented, not linked out of the tree.** A reader of a zip has no repository to follow a
   `https://` link into, and the shipped project has everything a page needs.

## How it is proven

- A dependent fixture (`declared-atlas`, which depends on `Units` and borrows `Units.Quantity`) is generated into a
  temporary directory, and the test asserts: the tree holds a folder per language of the chain, every concept of every
  language has a page, every link in every page resolves to a file that exists **including the cross-language ones**, a
  slot appears with its kind and its target, a concept with no notation says so, and two runs produce identical bytes.
- One test reads the root page as text and asserts that each declared dependency appears with the version it asked for,
  and that a declared dependency the resolver did not answer is shown as unresolved rather than left out.
- One test reads the generated HTML as text and asserts that no `<script>` of ours runs before the search index is
  defined - the failure mode of a static page with a script is a page whose search silently does nothing.
- One test gives the generator a language whose chain contains a cycle and asserts the refusal names the language that
  closed it.
- A picture is not part of it: the output is HTML, and the repository's screenshot tool is for the editor and the shell.

## Slices

1. **The chain, values first, proved on a language that stands on nothing.** `Generate(language)` answers the whole
   chain from the first commit - the folder map, the per-language folders, the root page, the assets - because a
   single-language generator would be a second code path for the one case that is not the rule. It is *proved* on
   `declared-pipeline`, whose chain is itself: the concept pages, `aspects.html`, every link resolved, byte-stable.
   `Write` and **File ▸ Export documentation…** on top of it.
2. **The dependency proved.** The same generator on `declared-atlas`, whose chain holds `Units`: Atlas' output contains
   Units' folder, the dependency table names `Units 1.0.0` as resolved, the search finds `Quantity`, and the link from
   `Atlas.Stop` into `Units.Quantity` resolves to a file that exists. One test with a declared dependency nothing
   answered asserts the unresolved row.
3. **The pictures.** The notation spelling per concept and `examples.html`, drawn from the `tests/` models when the
   language has them.
4. **Packaged.** `File > Package...` writes the tree under `docs/` in both variants, and the archive stays byte-stable.