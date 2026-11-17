namespace Projectional.Editing;

/// <summary>Which way a search walks the document.</summary>
public enum FindDirection
{
    /// <summary>Towards the end of the document, wrapping around at the end.</summary>
    Forward,

    /// <summary>Towards the start of the document, wrapping around at the start.</summary>
    Backward,
}