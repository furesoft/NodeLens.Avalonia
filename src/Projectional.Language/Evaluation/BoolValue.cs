namespace Projectional.Language;

/// <summary>Either true or false.</summary>
/// <param name="Content">The truth value.</param>
public sealed record BoolValue(bool Content) : Value
{
    /// <inheritdoc />
    public override string Display => Content ? "true" : "false";
}
