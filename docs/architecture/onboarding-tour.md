# The onboarding tour: explaining the IDE inside the IDE

`Help ▸ Tour` and the first run of a new installation put an overlay over the window: the page dims, one area stays
lit, and a card says what it is and why it is interesting. The decision is
[ADR 0025](../adr/0025-the-tour-is-a-guide-overlay.md); the shape below is the plan.

**Nothing here is implemented yet.**

## Why a tour and not better labels

Every gesture of a projectional editor is a sentence a user has to be told once:

- a document is **projected**, not typed - clicking builds nothing until `Enter` fills a slot;
- `Alt+Enter` is where repairs *and* a language's own intentions appear, and a new user will not guess that the bulb
  in the margin is the same list;
- the outline is a tree of the model, not of files; the project explorer is what the *languages* are made of;
- `View ▸ Panels` is how a closed tool comes back, which is the first thing anybody needs after closing one.

None of that is discoverable by looking, and the README is for people who already decided to care.

## The shape

```csharp
internal sealed record TourStep(string Text, TourTarget Target);

internal abstract record TourTarget
{
    internal sealed record Control(string Name) : TourTarget;   // a name the shell declares
    internal sealed record Menu(string Path) : TourTarget;      // "View > Panels > Terminal"
    internal sealed record None : TourTarget;                   // a centred card
}
```

- **`Tour`** is the data: an ordered list of steps plus a title, so a step list is reviewable as text and testable
  without a window.
- **`TourPresenter`** draws: the dim, the hole around the target's bounds in window coordinates, the card, and the
  two buttons. It is the only part that knows about pixels.
- **Anchors are declared, not found.** A control that participates says so by name (`x:Name` already exists); the
  tour resolves names against the window's visual tree. A step whose name no longer exists is a **failed test**, not
  a silently centred card - that is the whole reason to prefer names over selectors.
- **"Already seen" is a setting**, in the same store as the window's other chrome state, and it is written when the
  last step is dismissed rather than when the tour starts: an abandoned tour shows again.

## The steps that ship first

| Step | Points at | Says, in one sentence |
| --- | --- | --- |
| 1 | the document area | this is a model drawn as text; the text is a projection, not a buffer |
| 2 | the same document, `Enter` | a slot filled by `Enter` is how a node gets made - and the completion list is behind `Ctrl+Space` |
| 3 | the gutter bulb | `Alt+Enter` offers the repairs a finding carries and the intentions the language declares |
| 4 | the outline tool | the left column is the model's tree, not files |
| 5 | the project tool | opening a language shows what it is made of - its concepts, its editors, its types |
| 6 | `View ▸ Panels` | a tool that was closed comes back here |
| 7 | the terminal tool | any program can be run here, with its output and its input |

Seven steps is already long for a first run. The plan is to ship these seven, then measure: a tour nobody finishes is
worse than none, and the honest instrument is the "already seen" store.

## What must be decided before it is written

| Question | Why it matters |
| --- | --- |
| Does the overlay take the pointer, or pass it through? | A tour that blocks clicking is a slideshow; one that passes clicks through loses the "click here" affordance. Recommendation: the overlay takes the pointer except on the lit target, so the step can be *tried* while it is explained. |
| Is the card positioned by the step or by the space? | A step that says "above" breaks on a short window; positioning from the available space is more code and never points off screen. |
| What happens when the target is not on screen (a closed panel)? | Either the tour opens it (a tour that changes the window is honest) or the step is skipped. Recommendation: the step declares `Reveal` - show the tool, then point - because "click View ▸ Panels" while the panel is hidden teaches nothing. |
| Keyboard: does `Esc` end the tour? | Yes, and it must also work mid-step, or the tour becomes a trap. |

## What this plan does not include

- **A tour of a language.** The steps above are the shell's. A language that wanted to introduce itself would need a
  language-side declaration, which is a different feature (and the concept description already answers "what is
  this" on hover).
- **Localisation.** The texts are resources in one file, so translating later is a file, not a rewrite.
- **A tour of the terminal or of running a document** beyond one sentence each: those are feature demonstrations,
  and the gallery in the README already is one.
