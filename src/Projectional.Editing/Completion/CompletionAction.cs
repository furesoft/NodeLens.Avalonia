namespace Projectional.Editing;

/// <summary>
/// An action that keeps the completion list open rather than closing it.
/// </summary>
/// <remarks>
/// <para>
/// The marker exists for one rule in <see cref="EditorSession"/>: every action that is not a completion
/// action closes the list, because a keystroke that means something else - a move, a deletion, an undo -
/// describes a caret the proposals no longer belong to. Marking the exceptions keeps that rule a single
/// line instead of a list of types that grows with every new completion gesture.
/// </para>
/// <para>
/// A completion action may still close the list deliberately - accepting and dismissing both do - so the
/// marker means "may leave it open", not "must".
/// </para>
/// </remarks>
public abstract class CompletionAction : EditAction
{
}
