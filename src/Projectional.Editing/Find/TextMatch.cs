namespace Projectional.Editing;

/// <summary>One occurrence, as the place the caret would stand in and the offsets inside it.</summary>
/// <param name="Place">The caret place whose cell holds the text.</param>
/// <param name="Offset">Where in that cell the occurrence starts.</param>
/// <param name="Length">How many characters it covers.</param>
/// <remarks>
/// Internal, unlike <see cref="FindMatch"/>: the public shape names a cell address so a host can act on it, while this
/// one keeps the <see cref="CaretPlace"/> the search walked, which is what lets the next-occurrence rule compare
/// positions in document order rather than by address.
/// </remarks>
internal readonly record struct TextMatch(CaretPlace Place, int Offset, int Length);