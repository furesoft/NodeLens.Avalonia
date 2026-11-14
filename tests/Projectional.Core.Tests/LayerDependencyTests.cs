using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Guards the dependency rule of the architecture overview: references point downward only,
/// so the core must not know about any higher layer.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void CoreReferencesNoOtherProjectionalAssembly()
    {
        var referenced = ReadAssemblyReferences("Projectional.Core.dll");

        // Guards against the reader silently returning nothing, which would make the
        // assertion below pass for the wrong reason.
        Assert.NotEmpty(referenced);

        var violations = referenced
            .Where(name => name.StartsWith("Projectional.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(violations);
    }

    private static string[] ReadAssemblyReferences(string assemblyFileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
        Assert.True(File.Exists(path), $"Expected {assemblyFileName} next to the test assembly.");

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToArray();
    }
}