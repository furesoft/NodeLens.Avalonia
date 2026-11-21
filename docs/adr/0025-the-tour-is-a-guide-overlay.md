# ADR 0025 — The guided tour is Avalonia.GuideControl's overlay

- Status: Accepted
- Date: 2026-11-21
- Related: [onboarding-tour.md](../architecture/onboarding-tour.md),
  [ADR 0023](0023-terminal-is-vtnetcore-behind-our-control.md), [standalone.md](../architecture/standalone.md)

## Context

The workbench is not a text editor, and every gesture in it is a sentence a new user has to be told once: a document
is projected rather than typed, `Alt+Enter` is where repairs and language intentions appear, the outline is not a
file tree, `View ▸ Panels` is how a closed tool comes back. The README explains all of it to somebody who has already
decided to care; nothing explains it inside the app.

The suggested component is [Avalonia.GuideControl](https://github.com/pchuan98/Avalonia.GuideControl) (MIT): an
overlay that dims the window, cuts a hole around one control and shows a card of text with next/previous, which is
exactly the shape of "explain the IDE".

Its state is what makes this an ADR rather than a spike, and it was checked rather than assumed:

- **It is not on any feed.** The flat-container lookup for `Avalonia.GuideControl` answers 404, and the repository
  has three commits, one star and no releases. There is no version to depend on.
- **It is not audited.** Three commits is a project, not a component, and this one would run *inside* our window,
  layered over the editor.
- **The mechanism, not the code, is what is wanted.** A dimmed overlay, a hole around a rectangle and a card with
  two buttons is a few hundred lines against our own controls, and the hard part is not the drawing: it is knowing
  *which rectangle* to point at, and that is ours either way.

## Decision

**The tour is a guide overlay, and it takes Avalonia.GuideControl's model rather than its assembly.** A `Tour` is
data - an ordered list of steps, each with a text and a target (a named control, a menu path, or nothing for a
centred card) - and a `TourPresenter` draws the dim, the hole and the card over the window's content. The panel,
its menu entry, the first-run state and the step list are the shell's.

This is the same call as [ADR 0023](0023-terminal-is-vtnetcore-behind-our-control.md) made for the terminal, for
the same reason and with the same consequence: when the upstream project publishes a package that targets the
Avalonia we use, taking it becomes a swap of the presenter, because the steps are data and do not know who draws
them.

## Consequences

- **The tour cannot rot silently.** A step names a control; a test opens the window, runs the tour and asserts that
  every step resolved its target *and* that the target was on screen - so a renamed tool fails in CI rather than
  pointing at an empty rectangle.
- **The tour is the shell's, not the framework's.** `Projectional.Avalonia` gains nothing; an embedder that wants a
  tour brings one, and its steps are about its own window.
- **Attribution.** If the upstream work is ever vendored rather than depended on, it is MIT and its notice goes with
  it; today nothing of it is copied.
- **Rejected alternatives**: vendoring three commits of someone's first draft (unauditable, and it would still need
  our anchors); a first-run web page (leaves the app to explain the app); a "tip of the day" toast (does not point
  at anything, which is what a tour is for).

## Open questions

Resolved in [onboarding-tour.md](../architecture/onboarding-tour.md): which steps ship, how a target is named so a
rename fails a test, and where "already seen" is remembered.
