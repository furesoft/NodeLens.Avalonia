namespace Projectional.Editing;

/// <summary>
/// An edit cannot be carried out because the session, the tree and the model no longer agree.
/// </summary>
/// <remarks>
/// This is the failure an editing layer has instead of a crash: the tree is rebuilt from the model and
/// the layout from the tree, so anything the caller still holds - a caret, a cell - is a claim about a
/// past state. A claim that can be repaired is repaired (the caret is coerced onto the nearest place);
/// a claim that cannot be repaired, such as a document whose notation produces nothing to stand in, is
/// an error rather than a silent no-op, because it can only come from a broken notation.
/// </remarks>
public sealed class EditingException : Exception
{
    /// <summary>Creates an exception with the given message.</summary>
    public EditingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    public EditingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception with a default message.</summary>
    public EditingException()
        : base("The edit could not be carried out.")
    {
    }
}
