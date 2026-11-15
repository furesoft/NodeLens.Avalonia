namespace Projectional.Workbench;

/// <summary>What the user decided to do about a document that has changes not on disk.</summary>
internal enum UnsavedChoice
{
    /// <summary>Write the document, then carry on with what asked.</summary>
    Save,

    /// <summary>Throw the changes away and carry on.</summary>
    Discard,

    /// <summary>Do nothing at all, which is also what dismissing the prompt means.</summary>
    Cancel,
}
