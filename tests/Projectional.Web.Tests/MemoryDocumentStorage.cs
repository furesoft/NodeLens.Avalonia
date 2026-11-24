namespace Projectional.Web.Tests;

/// <summary>A storage that keeps its values in a dictionary, so the workspace is testable without a browser.</summary>
internal sealed class MemoryDocumentStorage : IDocumentStorage
{
    internal Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string? Read(string key) => Values.GetValueOrDefault(key);

    public void Write(string key, string value) => Values[key] = value;
}
