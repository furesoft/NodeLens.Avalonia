namespace Projectional.Core;

/// <summary>
/// One recorded step of a transaction: what a subscriber should hear about, and how to undo the
/// step if the transaction never commits.
/// </summary>
/// <param name="Change">
/// The published description, or <see langword="null"/> for a step that is an implementation detail
/// — creating a detached node, for instance, which is only worth reporting once it is placed.
/// </param>
/// <param name="Rollback">Restores the model to the state before this step.</param>
internal readonly record struct JournalEntry(IModelChange? Change, Action Rollback);
