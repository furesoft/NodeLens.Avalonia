namespace Projectional.Avalonia;

/// <summary>What the user decided to do about a document that has changes not on disk.</summary>
/// <remarks>
/// Public and in the library because <see cref="IShellDialogs"/> answers it: a host that supplies its own dialog has
/// to be able to say what the user chose. <see cref="Cancel"/> is the value a dismissal produces, because a question
/// that loses data when it is ignored is worse than no question at all.
/// </remarks>
public enum UnsavedChoice
{
    /// <summary>Write the document, then carry on with what asked.</summary>
    Save,

    /// <summary>Throw the changes away and carry on.</summary>
    Discard,

    /// <summary>Do nothing at all, which is also what dismissing the prompt means.</summary>
    Cancel,
}
