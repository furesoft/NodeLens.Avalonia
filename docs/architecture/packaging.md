# Packaging

Every project under `src/` is a NuGet package. That is [ADR 0022](../adr/0022-every-project-is-a-package.md);
this document is the working contract - what is packable, where the version comes from, and how a package
reaches nuget.org.

The framework's audience is people who build their own DSLs or embed a DSL editor as a component. An
embedder's first step is a `PackageReference`, so "consumable from another repository" is a product
requirement rather than a convenience. A host that wants the JSON language takes `Projectional.Json` and its
transitive closure; nothing about this repository's folder layout leaks into its solution.

## What is packable

`Directory.Build.props` sets `IsPackable` to `true` as the repository default. A project opts out where it is
declared, because a package is a thing to depend on and these are not:

| Opted out | Why |
| --- | --- |
| `tests/*` | A test project is not a dependency. |
| `tools/Projectional.Screenshots` | A repository tool. |
| `samples/host-app` | A sample host, not a library. |
| `templates/projectional-language` | A `dotnet new` template, installed from a folder. |

Everything else ships. That includes the three assemblies ADR 0019 names as public API (`Core`, `Avalonia`,
`Toolkit`) **and** the internal-plus-`InternalsVisibleTo` band beneath them (`Language`, `Projection`,
`Persistence`, `Semantics`, `Editing`, `Checks`, `Intentions`, `Testing`, `Languages`, `LanguageExport`), because a
language package's dependencies reach into that band and a dependency that cannot be referenced is not a
dependency. It also includes `Projectional.Workbench`: it is an application, but it is under `src/`, it is a
product, and its `--self-check` entry point makes it usable as a tool. A host that wants the shell as a
library takes the package and ignores the executable.

`dotnet pack Projectional.slnx` therefore produces **35 packages**:

- **15 framework assemblies** - `Core`, `Language`, `Languages`, `LanguageDocs`, `LanguageExport`,
  `Persistence`, `Projection`, `Semantics`, `Editing`, `Checks`, `Intentions`, `Testing`, `Toolkit`,
  `Avalonia`, `Workbench`
- **16 languages** - `CSharp`, `Expressions`, `Json`, `Xml`, `Toml`, `Hocon`, `Yaml`, `Structures`,
  `Editors`, `TypeRules`, `TypingRules`, `TextGen`, `ConstraintRules`, `ScopeRules`, `IntentionRules`,
  `ImportRules`
- **4 sample languages** - `Samples`, `Samples.StateMachine`, `Samples.FeatureModel`, `Samples.SqlSchema`

The CI `pack` job asserts that number. Adding a project means updating the assertion in the same commit:
that is deliberate friction, because "the package silently vanished from a release" is the failure mode
nobody notices.

## The metadata every package carries

All of it lives in `Directory.Build.props`, so a `.csproj` never repeats it:

| Property | Value |
| --- | --- |
| `Authors` | `furesoft` |
| `PackageLicenseExpression` | `Apache-2.0` |
| `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType` | the GitHub repository |
| `PackageTags` | `projectional;editor;language;dsl;mps` |
| `PackageReadmeFile` | `README.md`, packed from the repository root |
| `IncludeSymbols`, `SymbolPackageFormat` | `true`, `snupkg` |

The README is packed by one `<None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" .../>` item in
`Directory.Build.props`, so every package's nuget.org page shows the same front page. A `.snupkg` beside each
`.nupkg` lets a debugger step into the package; it costs one file and no configuration.

**A package's dependencies are pinned to the same version as the package itself.** A host that references
`Projectional.Avalonia` 1.2.0 gets `Projectional.Projection` 1.2.0, not a floating range - which is what
makes "the version I compiled against" mean one thing.

## Where the version comes from

**GitVersion reads the repository.** `GitVersion.yml` asks for `ContinuousDelivery` with a floor of `0.1.0`:

```yaml
mode: ContinuousDelivery
next-version: 0.1.0
```

- A `v1.2.3` tag is version `1.2.3`.
- Every other commit is a prerelease of the next patch, labelled by its branch - an untagged `main` here
  produces `0.1.0-760`, where 760 is the commit count.

Nothing types a version by hand. `<Version>0.1.0</Version>` in `Directory.Build.props` is the fallback for a
checkout with no git history to read (a source drop, a CI checkout without tags), and it is the only version
literal in the repository.

Three things about the configuration are load-bearing and easy to get wrong:

- **`tag-prefix` is deliberately absent.** GitVersion's default already accepts the `v` convention. Setting
  `tag-prefix: v` explicitly breaks version 6.8.2 when `next-version` is present: it tries to parse the
  prefixed string as a strict SemVer and fails with "Failed to parse ... into a Semantic Version". Do not
  "clarify" the configuration by adding it back.
- **A custom `branches:` key needs a `regex` in v6.** A bare `fix:` key throws a `ConfigurationException`
  rather than falling back to the default. The file currently declares no branches at all, which is why the
  defaults apply.
- **A cached `obj/gitversion.json` outlives a tag change.** The target treats that file as its output and skips
  re-running when it looks up to date, so a build made while a tag existed keeps claiming that tag's version
  after the tag is gone. If a version looks wrong, delete the `obj` folders (or just the `gitversion.json`
  files) and build again before believing it.

**CI needs `fetch-depth: 0` wherever it builds.** A shallow clone has neither the tags nor the commit count
GitVersion reads, so a shallow checkout produces a wrong version rather than an error. Every job in
`.github/workflows/ci.yml` checks out with full history for that reason.

## Building and packing locally

```powershell
dotnet build                                 # GitVersion runs; the version is in the output
dotnet pack Projectional.slnx -o artifacts   # 30 .nupkg + 30 .snupkg
```

`artifacts/` is gitignored. Packing needs no tag and no credentials: a local pack produces prerelease
versions and that is the correct answer, because nothing was released.

## Publishing

**Only a tag publishes.** The `publish-nuget` job is guarded by

```yaml
if: startsWith(github.ref, 'refs/tags/v')
```

so a merge or a pull request can never ship. The flow is:

```powershell
git tag v1.2.3
git push origin v1.2.3
```

That pushes the tag, which triggers `ci`; the `pack` job produces the 35 packages and uploads them as an
artifact, and `publish-nuget` downloads that artifact and pushes both `.nupkg` and `.snupkg` to nuget.org.

Two properties of the job are deliberate:

- **`environment: nuget`.** The secret lives in a GitHub environment rather than in the repository's global
  secrets, so a reviewer can be required for a publish and the secret is only exposed to the job that needs
  it.
- **`--skip-duplicate`.** A partly failed publish is re-run rather than repaired by hand, and a re-run must
  not fail because some packages are already there. A tag pushed by mistake cannot be unpublished, only
  unlisted, so a second push is the ordinary outcome.

### Setting up the secret

1. Create the `nuget` environment in the repository's settings, with a required reviewer if a publish should
   be confirmed.
2. Add a nuget.org API key scoped to `Projectional.*` as `NUGET_API_KEY` in that environment.
3. The job fails with a named error ("see docs/architecture/packaging.md") when the secret is missing,
   rather than pushing nothing and reporting success.

**Trusted Publishing (OIDC) is the better long-term answer** and is the one planned replacement: no
long-lived secret, and the publishing identity is the workflow. It needs a nuget.org account configured for
it, so switching is a change to one job rather than a change to this document's structure.

## What CI does with packages

`.github/workflows/ci.yml` has four jobs relevant here:

| Job | What it proves |
| --- | --- |
| `build-and-test` | Builds and tests on Windows and Ubuntu with full history. |
| `byte-stability` | The saved form of the corpus is still byte-identical (ADR 0005). |
| `pack` | `dotnet pack` succeeds and produces exactly 35 packages; uploads them as an artifact. |
| `publish-nuget` | Tag-gated; pushes the artifact to nuget.org. |

`single-file` publishes the standalone app and asserts the single-file shape; it is unrelated to packaging. `docs`
publishes the generated documentation site to GitHub Pages, which is also unrelated to packaging.

The `single-file` job is a build of every product, so it fails on anything the single-file analyzer objects to. That is
not decoration: `Projectional.LanguageExport` answers where its reference assemblies live, and `Assembly.Location` is
empty inside a bundle - IL3000, an error here because warnings are errors - so the compiler asks the runtime's trusted
platform assemblies instead (`AssemblyCompiler.PathOf`). A product assembly that must survive that publish may not read
`Assembly.Location`.

## Known caveats

- **`Projectional.Workbench` ships as a package.** It is an application, so the package is only useful as a
  tool or as a library. If that turns out to be wrong, it is one `IsPackable=false` in its `.csproj` and one
  count in the CI assertion.
- **The repository URL says `NodeLens.Avalonia` while the packages say `Projectional`.** The GitHub
  repository was renamed; the package prefix was not. `PackageProjectUrl` and `RepositoryUrl` point at the
  real repository so a consumer can find the source; renaming the packages is a separate decision.
- **The count assertion is manual.** A project added without updating it fails CI, which is intended, but
  the message does not say which project is missing - the job prints the list it produced, so the diff is
  visible in the log.
