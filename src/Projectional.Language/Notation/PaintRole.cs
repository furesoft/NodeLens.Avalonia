namespace Projectional.Language;

/// <summary>
/// What a decoration means, rather than what colour it is.
/// </summary>
/// <remarks>
/// The same division <see cref="ConstantKind"/> makes for text: a notation says "this is a table's header" and a
/// palette says what a header looks like. A role is therefore not a colour and cannot be one - a host that paints
/// its own palette, or the same document on a dark background, must be able to change every decoration without the
/// language knowing. The set is deliberately small: each role is a decision a notation author makes about what a
/// region is, and a vocabulary of one role per shade would be a palette in the notation.
/// </remarks>
public enum PaintRole
{
    /// <summary>An ordinary region: a table's body, a boxed group.</summary>
    Neutral,

    /// <summary>A region the notation wants quieter than the document around it, such as a table's header row.</summary>
    Muted,

    /// <summary>A region the language points at, such as the row a rule selected.</summary>
    Accent,

    /// <summary>A region that asks the reader to look before continuing.</summary>
    Warning,

    /// <summary>A region that states a defect.</summary>
    Danger,
}
