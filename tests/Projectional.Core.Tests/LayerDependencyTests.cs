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

    [Fact]
    public void TheChecksPackageReachesDownwardOnly()
    {
        // A check produces diagnostics and edits, so the package sits in the behaviour band beside Editing:
        // it may name the layers below it, and never the control, the app, a language's samples or another
        // checks package. Adding a reference "just for one icon or one test fixture" is how that drifts.
        var referenced = ReadAssemblyReferences("Projectional.Checks.dll");

        Assert.Contains("Projectional.Editing", referenced);
        Assert.Contains("Projectional.Semantics", referenced);

        var violations = referenced
            .Where(name => name is "Projectional.Avalonia"
                or "Projectional.Workbench"
                or "Projectional.Samples"
                or "Projectional.Samples.Checks"
                or "Projectional.Persistence")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void TheLanguagePackagesReachDownwardOnly()
    {
        // A shipped language carries its own checks and intentions (ADR 0021), so it names the behaviour band
        // below it - and never the control or the app above it. The check is by assembly rather than by rule,
        // because a `ProjectReference` is what a host actually inherits.
        foreach (var assembly in new[] { "Projectional.Json.dll", "Projectional.Expressions.dll" })
        {
            var referenced = ReadAssemblyReferences(assembly);

            Assert.Contains("Projectional.Editing", referenced);
            Assert.DoesNotContain("Projectional.Avalonia", referenced);
            Assert.DoesNotContain("Projectional.Workbench", referenced);
        }
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