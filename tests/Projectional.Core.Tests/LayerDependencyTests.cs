using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Projectional.Expressions.Intentions;
using Projectional.Hocon.Checks;
using Projectional.Json.Checks;
using Projectional.Samples.Checks;
using Projectional.Samples.FeatureModel.Checks;
using Projectional.Samples.SqlSchema.Checks;
using Projectional.Samples.StateMachine.Checks;
using Projectional.Structures.Intentions;
using Projectional.Toml.Checks;
using Projectional.Xml.Checks;
using Projectional.Yaml.Checks;

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
    public void TheTestingPackageReachesDownwardOnly()
    {
        // The runner reads a language project's tests folder and the persistence layer, so it sits in the
        // behaviour band beside Checks: it may name the layers below it and never the control or the app.
        var referenced = ReadAssemblyReferences("Projectional.Testing.dll");

        Assert.NotEmpty(referenced);

        var violations = referenced
            .Where(name => name is "Projectional.Avalonia" or "Projectional.Workbench")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void TheLanguagePackagesNeverReachUpward()
    {
        // A shipped language carries its own checks and intentions (ADR 0021), so it names the behaviour band
        // below it - and never the control or the app above it. The assertion is on emitted references, which
        // is why it is the negative half only: Roslyn drops a reference the assembly does not use, so "it
        // references Editing" is not something a binary can promise.
        foreach (var assembly in LanguageAssemblies)
        {
            var referenced = ReadAssemblyReferences(assembly);

            Assert.DoesNotContain("Projectional.Avalonia", referenced);
            Assert.DoesNotContain("Projectional.Workbench", referenced);
        }
    }

    [Fact]
    public void ALanguagesBehaviourIsCompiledIntoTheLanguage()
    {
        // The half of ADR 0021 a binary can promise: the checks and intentions of a language are types *in*
        // that language's assembly, not a package beside it. Each pair is one language.
        Assert.Equal("Projectional.Json", typeof(JsonChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Hocon", typeof(HoconChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Toml", typeof(TomlChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Xml", typeof(XmlChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Yaml", typeof(YamlChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Expressions", typeof(ExpressionsIntentions).Assembly.GetName().Name);
        Assert.Equal("Projectional.Samples", typeof(SampleConstraints).Assembly.GetName().Name);
        Assert.Equal("Projectional.Samples.FeatureModel", typeof(FeatureModelChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Samples.StateMachine", typeof(StateMachineConstraints).Assembly.GetName().Name);
        Assert.Equal("Projectional.Samples.SqlSchema", typeof(SqlSchemaChecks).Assembly.GetName().Name);
        Assert.Equal("Projectional.Structures", typeof(StructureIntentions).Assembly.GetName().Name);
    }

    /// <summary>Every shipped language, by assembly name.</summary>
    private static readonly string[] LanguageAssemblies =
    [
        "Projectional.Json.dll",
        "Projectional.Hocon.dll",
        "Projectional.Toml.dll",
        "Projectional.Xml.dll",
        "Projectional.Yaml.dll",
        "Projectional.Expressions.dll",
        "Projectional.Samples.dll",
        "Projectional.Samples.FeatureModel.dll",
        "Projectional.Samples.StateMachine.dll",
        "Projectional.Samples.SqlSchema.dll",
        "Projectional.Structures.dll",
    ];

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