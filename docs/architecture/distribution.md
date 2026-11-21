# Distribution and updates: Velopack

The app ships as a self-contained single file per runtime identifier today, and CI proves it. What is missing is
everything after the download. This document is the plan for that half; the decision is
[ADR 0024](../adr/0024-distribution-and-updates-are-velopack.md).

**Nothing here is implemented yet.** It is what M7 will do, written down while the shape is fresh, with the parts
that must be checked first named as checks rather than assumed.

## What the user gets

| Step | Today | After |
| --- | --- | --- |
| Install | unpack a zip | run `Setup.exe`; the app lands where the OS expects, with a shortcut |
| Find out about a new version | nothing | the shell asks on start (and from `Help ▸ Check for updates`) |
| Update | download again | `UpdateManager` downloads a delta and applies it on restart |
| Go back | nothing | a channel, not a downgrade: `preview` beside `stable` |

## The shape

1. **`Main` starts with the hook.**
   ```csharp
   VelopackApp.Build()
       .OnFirstRun(_ => FirstRunMarker.Write())
       .Run();
   ```
   First, before anything reads `args`: the hook consumes `--veloapp-*` and the first-run flags, and it must not
   swallow `--self-check` - the single-file CI job runs that on a machine with no display, and a hook that claimed
   every argument would turn a green job into a mystery.
2. **A packaging step in the release job.** `vpk pack` over the *publish output the `single-file` job already
   produces*, one run per RID, with `--packId Projectional.Workbench`, `--packVersion` from GitVersion, and
   `--channel` from the tag (`v1.2.0` → `stable`, `v1.2.0-preview.3` → `preview`).
3. **`releases.json` and the assets are published with the packages**, by the same tag-gated job, so ADR 0022's
   rule holds: a `v*` tag is the only thing that ships anything.
4. **The window owns one `UpdateManager`.** It is created lazily (a user who never asks pays one HTTP request less),
   asks once at startup off the UI thread, and offers the answer as a status line + `Help ▸ Check for updates…`,
   never as a modal on start.

## What must be verified before it is written

| Question | Why it decides something |
| --- | --- |
| Which Velopack version is current, and does it still support `net10.0` and `win-x64`/`linux-x64`? | The plan names 1.2.x from the feed; the packaging API has moved before. Verify against the feed rather than a blog post. |
| Does `VelopackApp.Build().Run()` leave a non-`--veloapp-*` argument list untouched? | `--self-check` is the one command CI depends on. If it eats arguments, the hook goes behind a guard. |
| Is the `-r win-x64` publish output a folder Velopack accepts as-is? | The single-file bundle must stay the unit ([ADR 0024](../adr/0024-distribution-and-updates-are-velopack.md)); if `vpk` insists on a *framework-dependent* folder, that is a second build and a decision, not a detail. |
| Does `vpk` sign? | Windows SmartScreen makes an unsigned installer a bad first impression. Signing needs a certificate in CI; that is a secret and a policy, not a code change. |
| Linux: `.AppImage` only, or a `.deb` too? | An AppImage cannot self-update in place the way Windows does; Velopack's answer for Linux is per-format. Decide per channel. |

## What this plan does not include

- **File association** (`.mps` opens the app). It is M7's other half and belongs with the installer, but it is a
  separate change: the association must survive an update, and on Linux it is a `.desktop` entry and
  `xdg-mime`, not a registry key.
- **A store presence.** Nothing about Microsoft Store, Flathub or Homebrew is decided here.
- **Automatic updates without asking.** The user is told and chooses; an editor that restarts itself under a
  document would be the wrong default for this app.

## Open questions

- **Where does the update UI live?** A status-bar line is the cheapest and the least intrusive; a dialog is what a
  user expects when they click `Check for updates`. Both, probably: the line reports, the dialog acts.
- **What happens to a running terminal?** The terminal tool owns a child process; an update that restarts the app
  kills it. The update flow has to refuse while one runs, or say that it will.
- **Which channel does a build from `main` belong to?** Only tags ship, so the answer today is "none" - which is
  also the answer for anyone who wants the newest bits without a tag.
