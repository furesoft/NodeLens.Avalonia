# ADR 0024 — Distribution and updates are Velopack's job

- Status: Accepted
- Date: 2026-11-21
- Related: [distribution.md](../architecture/distribution.md), [packaging.md](../architecture/packaging.md),
  [standalone.md](../architecture/standalone.md), [ADR 0022](0022-every-project-is-a-package.md),
  [ADR 0003](0003-runtime-and-license.md)

## Context

The app publishes as a self-contained single file per runtime identifier, and CI proves it: a `win-x64` and a
`linux-x64` bundle, exactly one file in `publish/`, and `--self-check` run before Avalonia starts. What that
leaves unsolved is everything after the download: a user who has the app has no way to find out that there is a
newer one, no installer that puts it where an operating system expects, no shortcut, and no way back if the new
one is worse.

`roadmap.md` puts installers and file association in M7 and calls it "release hardening". Doing that by hand
means per-OS installer tooling (MSIX or Inno or WiX, `.dmg`, `.AppImage`), an update check of our own, and a
delta format - three projects' worth of work that is nobody's idea of the product.

Velopack is the .NET answer to exactly that: it wraps an app in an installer, publishes `releases.json` and
delta packages, and ships an `UpdateManager` that downloads and applies them.

## Decision

**Distribution and updates are Velopack's.** The app calls `VelopackApp.Build().Run()` as the first statement of
`Main`, CI produces the release with the `vpk` CLI, and the window owns an `UpdateManager` whose job is to say
"there is a newer version" and to apply it on the user's word.

Three constraints the decision carries with it:

1. **The single file stays the unit.** Velopack packages a *directory*, so what it is given is the publish output
   the `single-file` job already proves is one file. A packaging step that rebuilt the app its own way would be a
   second build to keep true.
2. **`--self-check` keeps working.** It runs before Avalonia starts, and `VelopackApp.Build().Run()` must not
   claim it: the hook parses its own arguments (`--veloapp-*`), and a hook that swallowed everything would break
   the one command CI uses on a machine with no display.
3. **A release is still a tag.** ADR 0022 makes a `v*` tag the only thing that publishes. Velopack's `releases.json`
   and its assets are published by the same tag-gated job, so "which commit is this build" has one answer for
   packages and for installers alike.

## Alternatives rejected

| Alternative | Why not |
| --- | --- |
| An installer per OS, hand-made | Three toolchains, three signing stories and an updater written from scratch - for a part of the product that nobody chose this repository for. |
| MSIX with the Microsoft Store | The store is a distribution channel, not an update mechanism, and it forbids the self-update the user asked for. |
| ClickOnce | Windows only, and its model is an application URL rather than a version the app can reason about. |
| No updates; "download the new zip" | Honest, and what the project has today: it is the status this ADR replaces. |
