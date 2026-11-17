namespace Projectional.Language;

/// <summary>Any text.</summary>
/// <param name="Content">The characters, without the quotes.</param>
public sealed record StringValue(string Content) : Value
{
    /// <inheritdoc />
    public override string Display => Content;
}
