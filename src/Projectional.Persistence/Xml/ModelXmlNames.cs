namespace Projectional.Persistence;

/// <summary>
/// The names the stored form is built from, in one place, so that the reader and the writer
/// cannot drift apart.
/// </summary>
internal static class ModelXmlNames
{
    /// <summary>The document element of a model file.</summary>
    internal const string Model = "model";

    /// <summary>Wrapper around the root nodes of a model.</summary>
    internal const string Nodes = "nodes";

    /// <summary>One node, with its identity in attributes and its slots in child elements.</summary>
    internal const string Node = "node";

    /// <summary>Wrapper around the filled properties of a node.</summary>
    internal const string Properties = "properties";

    /// <summary>A single reference slot.</summary>
    internal const string References = "references";

    /// <summary>A single child slot and the nodes it holds.</summary>
    internal const string Children = "children";

    internal const string FormatVersion = "formatVersion";

    /// <summary>
    /// The format this build writes. A reader that meets a higher number stops instead of
    /// guessing, because a newer writer may have meant something else by the same element.
    /// </summary>
    internal const int SupportedFormatVersion = 1;

    internal const string Id = "id";

    internal const string Concept = "concept";

    internal const string Name = "name";

    internal const string Role = "role";

    internal const string Target = "target";

    internal const string ResolveInfo = "resolveInfo";
}
