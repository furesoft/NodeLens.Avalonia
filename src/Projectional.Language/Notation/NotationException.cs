namespace Projectional.Language;

/// <summary>A notation does not describe a concept that could ever be drawn.</summary>
public sealed class NotationException : Exception
{
    public NotationException(string message)
        : base(message)
    {
    }

    public NotationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotationException()
    {
    }
}
