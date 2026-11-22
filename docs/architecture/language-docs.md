# Interactive language documentation

**Status: built, all four slices** (`src/Projectional.LanguageDocs`, one test class, the shell's **File ▸ Export
documentation…** and **File ▸ Package…**, and a CI job that publishes the site to GitHub Pages). The dependency
requirement below is **decided** rather than proposed: a language's documentation contains the documentation of every
language it stands on.

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
_site/
  index.html                  the set: one row per language, one search over all of them
  assets/docs.css, docs.js    one stylesheet and one script for the whole tree
  Atlas/
    index.html                what Atlas says it stands on, and its concepts grouped by inheritance root
    concepts/Atlas.Stop.html
    aspects.html              every aspect at a glance, and a link to each
    enumerations.html         one page per aspect: what that folder lowered into
    notations.html
    types.html  typing.html  constraints.html  scopes.html  intentions.html
    generators.html  importers.html  accessory.html
    examples.html
  Units/
    index.html
    concepts/Units.Quantity.html
    aspects.html + one page per aspect
    examples.html
```

**A site is one set, not one tree per language.** `Generate(language)` documents that language and its chain.
`Generate(languages)` documents many at once, which is what CI publishes: the same tree, with every language's folder
present exactly once and one root page that lists them all. Doing it as one set rather than as one tree per language is
what keeps a shared base to *one* folder - `Units` would otherwise be written under every language that stands on it, and
each copy would be a page that can disagree with the others. The chain collects the union in base-first order, so the
listing reads as the layering it is, and two different folders claiming one name are refused before anything is written
rather than discovered in a browser.

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

**A dependency the host could not answer never reaches a page, and the loader is why.** `LanguageProject.Open` refuses a
language whose declared dependency nothing answered ("open that language first, or give the host a resolver that answers
it"), so a `LanguageProject` that exists has had every dependency answered; a page that offered an "unresolved" row would
be a branch nothing can reach. The page therefore lists what the host *answered*, which is the other half of the same
worry: a resolver is asked by name only, so it can answer `Units 2.0.0` to a language that wrote `Units 1.0.0`. That row
is marked, because a reader told that 2.0.0 backs a 1.0.0 language is being told something the language author did not
write.

**A cycle is refused rather than walked.** Loading already refuses one; the generator asserts that no language appears
twice in the chain and says which name closed the loop, because a generator that recursed would write pages until the
disk filled.

## The shape of a page

Every page is the same three parts, so a reader learns one layout and then reads:

- **A header** with the set's name, and **one search box**. It is in the header rather than on a front page, because a
  reader who has already followed a link to a concept is exactly the one who now wants another one.
- **A tree beside the content.** A language opens into its folders - `Overview`, `Aspects`, `Concepts` - and a folder
  into what it holds, the way the project explorer opens a language's folders. The language and folder the current page
  lives in start open and the entry for the page is marked, so arriving from a search engine shows where you are
  instead of a tree with everything shut.
- **The content**, under a breadcrumb.

**The folding is the browser's.** The tree is `details`/`summary`, so it works with the script blocked, unloaded, or
still loading - and a documentation folder that needs JavaScript to be *readable* would be a static site with a single
point of failure. The script adds two things only: the filter, and the drawer.

**The search filters the tree**, and it searches the whole set from every page, because every page carries the whole
tree. Typing narrows it to the entries that match, opens the branches that hold them, and Enter goes to the first one.
A language or folder whose own name matches keeps everything under it. Clearing the box restores the folds the reader
had before typing rather than leaving every branch a search happened to open.

**A narrow screen gets the drawer.** Under roughly 820 pixels the tree stops being a column and becomes a panel that
slides in from the left over the content, with a backdrop, `Escape`, and the same button that opened it as the three
ways out. It starts *below* the header rather than over it, so the search stays reachable while the drawer is open. The
toggle is drawn as three lines in inline SVG so it needs no icon font, and `prefers-reduced-motion` turns the slide
off. Tables scroll sideways rather than wrapping, because a slot table has six columns and half of them are keys.

## Where an aspect page stops

Three parts of a language cannot be written out, and the page says so instead of inventing a name:

- **A constraint** holds a `Check(ConstraintContext)` method. There is no name, no message and no summary to print, so
  the page counts the rules and names the concepts they sit on - and says that no rule applies everywhere when none does.
- **An intention** is a provider asked what it offers *at a caret*. The registry cannot be enumerated, so the page can
  say that one is registered and nothing more.
- **An enumeration** loses its key when lowering resolves it into the symbols a property may hold. What survives is
  `enum(public, internal, private, protected)`, so that - plus the slots that hold it - is how the page names it. Two
  enumerations with the same symbols in the same order are one type here, as they are everywhere.

The generated text is the opposite case and is written out in full: a generator's template is a tree of parts, so the
page spells it - `map {name}[if entry: at ->entry]`, a list with the separator that joins it - because the shape of what
a language emits is what a reader wants before trusting it with a model.

**An importer is written out in full as well, both kinds.** A declared importer holds its rules as lowered objects - the
concept, the anchor and the ordered parts - and the token classes those parts read, so the page prints them in the words
the import language draws them with (`capture name from Text`, `repeat stages`, `token Space is skipped as whitespace`).
A generator's inverse has no document at all, because what it reads *is* the generator's template, so the page spells
that template the way the generators page does. Both pages also name the concepts a document may start with and the
codes a refusal carries, because those are the rest of what decides whether a file is read. Naming the importer's CLR
type - which is all the page said before - is a name for a class rather than for a format.

## What it contains

| Page | Content |
| --- | --- |
| `index.html` | the set: one row per language - name, version, how many concepts it declares, and what it stands on |
| `<Language>/index.html` | what that language declares it stands on (name, relation, the version asked for and the version answered, with a mismatch marked), then its concepts grouped by inheritance root, each with the slot it is named by and the naming convention that slot is held to |
| `<Language>/concepts/<key>.html` | one page per concept: key, name slot, description, super concepts and interfaces with links, and the concepts of the set that extend or implement it, the notation written out as the shape it spells (`map <name> at <entry>`, its body indented), every slot as a row (kind, key, type or target, cardinality, default) with the naming convention beside the name slot, then every slot of the set that can hold the concept - exactly or as a kind of what it names - so the page answers "who refers to it" as well as "what is it" |
| `<Language>/aspects.html` | every aspect at a glance: the folder it comes from, how much the language declares, one sentence saying what that means, and a link to its page - plus the concepts by kind, which is what `structure/` holds |
| `<Language>/enumerations.html` | the enumerations by the symbols they offer and the slots that hold them |
| `<Language>/notations.html` | every notation *as the shape it spells* |
| `<Language>/types.html` | the type rules, with subtypes, comparisons and replacements |
| `<Language>/typing.html` | what a burst of typing claims, and into which slot |
| `<Language>/constraints.html` | the constraints per concept |
| `<Language>/scopes.html` | the scope providers, beside the concept's own reference slots |
| `<Language>/intentions.html` | whether a provider is registered, and why a page can say no more |
| `<Language>/generators.html` | every generator *as the template it emits* |
| `<Language>/importers.html` | per importer: the concepts a document may start with, then its rules - the anchor and the parts, in the words the import language draws - with the token classes they read, or, for a generator's inverse, the templates it reads the text back from - plus the codes a refusal carries |
| `<Language>/accessory.html` | the documents that are models of the language itself |
| `<Language>/examples.html` | every document the language ships, drawn by the headless renderer and coloured by the kinds the editor paints - so the page shows what the language *looks like* rather than describing it. Omitted for a language that ships none |
| `assets/docs.css`, `assets/docs.js` | the one stylesheet and the one script - the header, the tree, the drawer and the responsive rules; and the filter, the drawer toggle and Enter. Both deterministic, both small enough to read |

Two rules come from the repository's own invariants:

- **The output is derived and byte-stable.** Same chain in, same bytes out - no timestamps, no machine-specific paths,
  no hash-ordered lists. Languages in chain order, concept pages by key, slots by declaration order, because those are
  the orders the registry already promises.
- **The pages never claim more than the model says.** A concept with no notation says so; a language with no
  constraints lists none; a declared dependency nothing answered says that. An empty aspect is a fact, not an omission
  - and it is said **once**: the page leads with a sentence saying how much the aspect holds, and when that is nothing
  the tables below it would only repeat it, so they are not rendered at all. A page that led with "no property is
  declared over an enumeration" and then repeated it in an empty table row read as a defect rather than as a fact.

## Where it lives

`src/Projectional.LanguageDocs/` - a package, because it is a product a host can reference and call
(`LanguageDocumentation.Generate`/`Write`, each overloaded for one language and for a set), and because every project
under `src/` is a package ([ADR 0022](../adr/0022-every-project-is-a-package.md)). It moved the CI `pack` job's package
count from 34 to 35 in the commit that added it.

It references `Projectional.Languages` (the lowered project and its resolved chain), `Projectional.Projection` (the
types a run and a classification are made of) and `Projectional.Toolkit` (the public half of the renderer, which is what
ADR 0019 exists for) - all downward arrows, so nothing new points up. It must **not** reference Avalonia:
documentation is generated by a build, a test, or a CLI, never by a window, and the highlighting is the proof that it
does not have to - the classification travels as `TextStyleKind`, and the colours are the stylesheet's.

The shell has one command for it - **File ▸ Export documentation…**, written like `LanguageExportWriter`, which is the
pattern this repository already has for "a menu entry that writes a folder". It offers a submenu of the languages opened
from a folder, because a language is what the documentation is *of* and the shipped ones are documented by CI; it asks
for a folder rather than a file, because the output is a tree whose pages link to each other with relative paths.

## Packaged with the language

**A package carries the documentation of every language it holds.** `File > Package...` already writes one package per
language in the chain, so the archive is the one place a reader gets a language *and* its documentation:

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

1. **Text with a class per piece, not SVG.** The decision this replaces said "inline SVG, not a bitmap", on the ground
   that the output must stay text. It is text - but not SVG: the renderer answers *runs*, each with its row, its column
   and the `TextStyleKind` that `CellStyler` gives the cell, and the page writes them into a `<pre>` with a span per
   kind. That is smaller than SVG by an order of magnitude, it is diffable, and it makes the page's colours the
   editor's palette's colours by construction rather than by a drawing that imitates them. What SVG would add is
   geometry the text grid does not have (wrapped lines, emphasis as weight); when a notation needs that, the runs are
   already the input an SVG writer would want.
2. **The search index is the tree**, which every page already carries, so there is no second index to keep in step and
   nothing to fetch - `file://` refuses fetches, and a `<script>` that defines an array would be a copy of the tree. The
   cost is that each page carries the whole set's entries: 94 pages of about 11 KB today. A set ten times larger would
   want the entries in `assets/search.js` loaded only when the box is used, and that is a decision to take when one
   exists.
3. **The notation is drawn with placeholders**, the way the editor draws an empty slot, and the page says that it is
   the shape rather than a document. Synthesising children to fill a slot is impossible in general - a child slot with
   a minimum has no obvious content.
4. **An example is a document the language ships**, not a model synthesised for the page: the accessory documents it
   declares and the ones saved beside `language.mps` - the same rule the app follows when it opens a language's
   documents, so a page and a window do not disagree about what a document is. A language that ships none gets no
   examples page at all rather than an empty one, because an empty room with a sign on the door is worse than no room.
   The tests' models were the other candidate and are the wrong one: they are assertions about a language, not examples
   of it.
5. **One page per aspect, not ten sections on one page.** The overview compares the aspects and links them; each aspect
   has a page of its own and an entry in the tree, because a reader looking for "which concepts generate text" wants
   `Generators` under the language and not a heading they have to scroll to. An aspect a language does not declare still
   gets its page, saying so: "no generator" is an answer to "does this language generate code", which is different from
   an examples page nobody would miss.
6. **An example is drawn without the type annotations.** A `TypeOf` cell is the editor telling a language author what
   its type rules are missing, and `unknown` is that complaint - not part of the language's shape. The documentation
   therefore asks the renderer for the shape and gets no cell at all (`EditorTreeBuilder(annotations: false)`), together
   with the punctuation that separates it, because a cell dropped on its own leaves `1 :` behind.
7. **A slot stands for what the child draws, punctuation included**, and both legends now say so. A notation spelled
   `for (<initializer> <condition>; <increment>)` shows one semicolon although the printed line has two, because the
   initializer is a statement and a statement draws its own: that is the reading cost of the placeholder rule above.
   A reader took it for a missing `;` in the language itself, so the sentence that explains the placeholders names the
   case - punctuation the child owns belongs to the child and is not repeated beside it. The generators page had no
   legend at all and gets the same sentence for its own spelling, where the same confusion was possible.
8. **A naming convention is named where it applies**, not only in the facts table. `NameRule` carries two words that are
   not the same: `Text` is the identity a declaration is written with (`pascal`) and `Requirement` is the shape a finding
   quotes (`PascalCase`). The pages show the shape, because a reader asks what a name looks like; the concept page keeps
   the identity in its facts, which is what an author writes in `structure/`. It appears twice for that reason - as
   a column of the concept list, where "how do names read in this language" is asked, and beside the name slot in the
   slot table, where the rule belongs to one cell.
7. **The picture is composed from the chain, like the shell's.** A document of a dependent language draws its base's
   concepts - Invoice holds a `Units.Quantity` - and a language's notation registry holds only what that language
   declares. So the chain builds one concept registry over every declaration and one notation registry over that, which
   is the composition `WorkbenchLanguage` performs; without it the page showed `<no notation for Units.Quantity>` where
   the editor draws a quantity.
8. **Shipped bases are documented, not linked out of the tree.** A reader of a zip has no repository to follow a
   `https://` link into, and the shipped project has everything a page needs.

## Published by CI

The repository publishes its own documentation, so the generator is exercised by every merge instead of by whoever
remembers to run it. The `docs` job in `ci.yml`:

- builds the shell in `Release`, because the documented languages *are* the shell's: `CSharp` is shipped with it and
  every example is a folder the build copies beside the executable;
- runs the shell with `--docs _site`, a second headless switch next to `--self-check` (`DocsExport`). It runs before
  Avalonia starts for exactly the reason the self check does - no display, so a build agent can do it;
- asserts that `_site/index.html` exists, uploads the folder, and deploys it to GitHub Pages. Only a push to `main`
  deploys; a pull request builds the site, which is the check that the generator still works.

The switch takes the languages from the two places the shell itself takes them from - the shipped set and
`ExampleProjects.Catalogue`, opened with the examples they stand on - so what is published cannot drift from what the
app offers. An example that fails to open is printed and the rest is still published, and the step fails afterwards:
a site that silently lost a language is worse than a failed job, but a job that publishes nothing because one example
broke is worse than both.

Turning this on needs one setting in the repository that no workflow can make: **Settings ▸ Pages ▸ Source =
GitHub Actions**.

## How it is proven

`tests/Projectional.Core.Tests/LanguageDocumentationTests.cs`, on `declared-atlas` and the `Units` it stands on:

- **Every concept of the chain has its own page** - and the base's concept has *no* page in the dependent's folder,
  which is the one-declaration rule read from the other side.
- **Every link on every page resolves to a file that exists**, absolute paths included as a failure, for both a
  one-language set and a set of two.
- **Two runs write identical bytes**, and the bytes carry no byte-order mark and no CRLF.
- **A language's page names each declared dependency** with the relation, the version asked for and a link to the
  language it resolved to.
- **A version the host answered that is not the version asked for is marked** - the reachable half of the dependency
  worry, built on a generated one-concept language folder that declares `Units 9.9.9`.
- **The link from `Atlas.Place` into `Units.Quantity` resolves**, which is the whole reason a chain is documented
  instead of a language.
- **A concept page states every slot** of the concept, and a slot over a concept of another language links there.
- **The aspects page counts what the folder declares** and says how many further concepts are visible through the
  chain.
- **A set documents each language exactly once** and its root page names itself as a set rather than as a language.
- **Two languages of the same name are refused** before anything is written.
- **Every page carries the header, the tree and exactly one search**, and every page that has an entry in the tree marks
  itself as current - resolved, not merely present, so the link it marks is the page it is.
- **The tree offers every concept of the set from every page**, because a language that appears only on the front page
  is a language nobody finds from a concept page.
- **A concept page writes out the notation it is drawn with**, escaped, with the body indented - and a concept with no
  notation of its own says so rather than leaving the section out.
- **The examples page draws every document the language ships** and colours each piece with the kind
  `CellStyler` gave it: `s-keyword` on a keyword, `s-ref` on a resolved reference, `s-text` on a stored string,
  `s-placeholder` on an empty slot - and a word that merely looks like a keyword is never a keyword.
- **A language with no document gets no examples page and no navigation entry**, asserted per language rather than per
  page, because the tree carries every language of the set.
- **`LanguageSnapshot.Runs` answers where a piece is and what it means** (in `Projectional.Toolkit`), which is the
  public half of the change and the reason a language author can colour a picture without reaching into the projection.
- **Every aspect has its own page and the overview links it**, and an aspect page holds what that aspect declares.
- **An example is drawn without the editor's annotations**: no `unknown` anywhere, and the punctuation with it.
- **An example draws a concept of the language it stands on**, which is the composed registry.
- **Every sample language declares an accessory document** (`ExampleProjectsTests`), so the examples of a language are
  what it ships rather than a promise the documentation makes on its behalf.
- **The pages travel inside a package**: the archive holds the code of the chain and, after it, `docs/index.html`,
  `docs/Atlas/concepts/…`, `docs/Atlas/examples.html` and the base's folder - so a reader of the one file can follow a
  link into the language it stands on. `LanguagePackage.Zip` grew a list of extra files for this: the export layer takes
  bytes and asks nothing about where they came from, which is what keeps two products from having to know each other.

Not proven by a test, and checked by running it instead: the whole site - seven languages, 94 pages - was generated and
opened from `file://`; the search box was typed into and read back through the DOM (one match visible, the other ninety
hidden, the reader's folds restored when the box was cleared); and the layout was measured at a 390-pixel width, where
the toggle appears, the tree is off-canvas until asked for, the backdrop and `aria-expanded` follow, and the page has no
horizontal overflow. Two defects were found exactly that way and are invisible to a test that reads HTML as text: a
`display` rule that beat the filter's own by specificity, so nothing was ever hidden, and a drawer that covered the
header, so the button that opened it was gone.

## Slices

1. ~~**The chain, values first, proved on a language that stands on nothing.**~~ Built: `Generate` answers the whole
   chain from the first commit - the folder map, the per-language folders, the root page, the assets - and `Write` puts
   those bytes on disk.
2. ~~**The dependency proved.**~~ Built: the atlas tests are the dependency proof, and `declared-atlas` is the fixture.
3. ~~**The pictures.**~~ Built: the notation spelling per concept, and `examples.html` with every shipped document
   drawn by the renderer and coloured by the kinds the editor paints.
4. ~~**Packaged.**~~ Built: `File > Package...` writes the tree under `docs/` beside the code in all three variants, and
   the archive stays reproducible - the pages carry no timestamp and the entries come in a fixed order.

Two things moved out of the original plan and are worth recording, because both were decisions rather than slips:

- **The root page of a set does not carry a dependency table**, because a site has as many dependency tables as it has
  languages. The facts live on each language's own page, so one rendering serves a one-language export and a site of a
  dozen.
- **`index.html` lists languages rather than the chain of one language.** For a one-language export the set *is* that
  chain, so nothing is lost; a page that said "this documentation is for X" in one mode and "these are the languages" in
  the other would be two pages pretending to be one.