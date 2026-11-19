# ADR 0022 — Every project is a package, versioned by GitVersion and published from a tag

- Status: Accepted
- Date: 2026-11-19
- Related: [ADR 0021](0021-one-project-per-language.md),
  [ADR 0019](0019-toolkit-is-the-language-surface.md), [packaging.md](../architecture/packaging.md),
  [overview.md](../architecture/overview.md)

## Context

The repository's products were only ever *project references*. A host could consume the editor by cloning
this repository and referencing the projects, and nothing else - there was no way to depend on
`Projectional.Avalonia` from a repository that does not have this one's folder layout, and no way to say
"this is version 1.2.0 of the projection layer".

That is the wrong default for a framework whose stated audience is people who build their own DSLs or embed
a DSL editor as a component. An embedder's first step is a `PackageReference`, and the languages are the
place where "one package per language" pays off: a host that wants JSON wants `Projectional.Json` and its
transitive closure, not a folder to add to its solution.

Three things had to be decided together, because each one alone produces a worse answer:

1. **What is packable.** "Everything" is tempting and wrong: a test project, a screenshot tool and a host
   sample are not things to depend on. "Only the public-API assemblies" is also wrong, because the
   languages are products here and a language that cannot be referenced is not a product.
2. **Where the version comes from.** A hand-typed `<Version>` in each of 27 projects drifts on the first
   release; a version computed at pack time cannot be reproduced from the commit.
3. **When a package is published.** Publishing from `main` makes every merge a release and makes "which
   commit is 1.2.0" unknowable.

## Decision

**Every project under `src/` is a package.** `Directory.Build.props` sets `IsPackable` to `true` as the
repository default and carries the metadata every package shares (authors, licence, project and repository
URL, tags, description, the README, and a `.snupkg` beside each `.nupkg`). The four things that are not
products opt out where they are declared:

| Opted out | Why |
| --- | --- |
| `tests/*` | A test project is not a dependency. |
| `tools/Projectional.Screenshots` | A repository tool. |
| `samples/host-app` | A sample host, not a library. |
| `templates/projectional-language` | A `dotnet new` template, installed from a folder. |

**The version comes from the repository, through GitVersion.** `GitVersion.yml` asks for
`ContinuousDelivery` with a floor of `0.1.0`; a `v1.2.3` tag is version `1.2.3` and every other commit is a
prerelease of the next patch labelled by its branch. `Version` in `Directory.Build.props` stays as the
fallback for a checkout with no git history to read. Nothing types a version by hand, and the version a
package carries is reproducible from the commit it names.

**Only a tag publishes.** `.github/workflows/ci.yml` packs on every build (so "is this repository
publishable" is answered continuously and a broken package fails the build), but the `publish-nuget` job is
guarded by `startsWith(github.ref, 'refs/tags/v')`. A merge or a pull request can never ship.

## Consequences

- `dotnet pack` on the solution produces 27 packages: thirteen framework assemblies (`Core`, `Language`,
  `Persistence`, `Projection`, `Semantics`, `Editing`, `Checks`, `Intentions`, `Toolkit`, `Avalonia`,
  `Workbench`, `Languages`, `LanguageExport`), the ten languages and the four sample languages. CI asserts
  that count so a project that silently stops being packable fails the build rather than disappearing from a
  release.
- **The count is a tripwire, not a policy.** Adding a project means updating the assertion in the same
  commit; that is deliberate friction, because "the package vanished" is the failure mode nobody notices.
- A package's dependencies are pinned to the same version as the package itself, so a host that references
  `Projectional.Avalonia` gets a `Projectional.Projection` of the same version rather than a floating one.
- `Projectional.Workbench` is a package even though it is an application: it is under `src/`, it is a
  product, and its `--self-check` entry point makes it usable as a tool. A host that wants the shell as a
  library takes `Projectional.Workbench` and ignores the executable.
- The `v*` tag is now load-bearing: it is both the trigger and the version. A tag pushed by mistake cannot
  be unpublished, only unlisted, so `--skip-duplicate` makes a re-run of a partly failed publish safe
  rather than a second failure.
- CI needs `fetch-depth: 0` everywhere it builds. A shallow clone has neither the tags nor the commit count
  GitVersion reads, so a shallow checkout produces a wrong version rather than an error.

## Alternatives considered

- **Publish from `main` without tags.** Every merge becomes a release and the version is a commit count.
  It removes the tag step and loses the answer to "which commit is 1.2.0", which is the question a bug
  report asks.
- **A hand-typed `<Version>` per project.** Simple and honest, and it drifts: the first release that
  forgets one project ships two versions of the same commit.
- **Only the three public-API assemblies as packages** (`Core`, `Avalonia`, `Toolkit`). It matches ADR
  0019's "three assemblies expose public API" exactly, and it leaves the languages unpublishable - which
  is the audience this framework is for. A language package is a product even though its surface is not
  the framework's.
- **Nerdbank.GitVersioning instead of GitVersion.** A fine tool with a similar contract. GitVersion was
  chosen because its configuration is one small YAML file and its `ContinuousDelivery` mode matches the
  "tag is the release" rule without further work.
- **NuGet Trusted Publishing (OIDC) instead of an API key.** The better long-term answer: no long-lived
  secret, and the publishing identity is the workflow. It needs a nuget.org account configured for it and
  cannot be verified from here, so the API key in the `nuget` environment is the shipped mechanism and
  switching is a change to one job.
