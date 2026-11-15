using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>
/// Answers the same thing for every cell, so a test can tell a style the frame computed from a style the
/// styler imposed.
/// </summary>
internal sealed class PunctuationOnlyStyler : ICellStyler
{
    public TextStyleKind StyleOf(EditorCell cell) => TextStyleKind.Punctuation;
}
