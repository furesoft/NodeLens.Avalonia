namespace Projectional.Core;

/// <summary>
/// One recorded step of a transaction: what a subscriber should hear about, how to undo the step,
/// and how to do it again.
/// </summary>
/// <param name="Change">
/// The published description, or <see langword="null"/> for a step that is an implementation detail
/// — creating a detached node, for instance, which is only worth reporting once it is placed.
/// </param>
/// <param name="Undo">Restores the model to the state before this step.</param>
/// <param name="Redo">
/// Runs the step again, or <see langword="null"/> when the step has no forward direction of its own:
/// a detached creation is nothing but the undo of an unregistration, because the placement that
/// follows it is a separate step with its own redo.
/// </param>
/// <remarks>
/// Redo is a closure over the original operation, not a replay of <paramref name="Change"/>. A move
/// is published as <see cref="NodeRemoved"/> plus <see cref="NodeAdded"/>; replaying those records
/// would have to delete the subtree in between and register it again, whereas the original operation
/// simply re-linked the node. Re-running the operation keeps redo as exact as undo.
/// </remarks>
internal readonly record struct JournalEntry(IModelChange? Change, Action Undo, Action? Redo);
