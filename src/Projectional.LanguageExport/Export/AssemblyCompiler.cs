using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Projectional.Core;
using Projectional.Language;

namespace Projectional.LanguageExport;

/// <summary>Compiles the source an export wrote into the assembly a language package ships.</summary>
/// <remarks>
/// <para>
/// The compilation happens in-process so the export needs no SDK, no temporary project and no shell: what a language
/// becomes is a byte array, like the source beside it. That is also what makes it testable - the test loads the very
/// assembly the exporter produced and asks it for its declarations.
/// </para>
/// <para>
/// The reference set is the framework's trusted assemblies plus the two assemblies the generated source can name:
/// <c>Projectional.Core</c>, for the declarations, and <c>Projectional.Language</c>, for the notation registry. That
/// is the executable form of ADR 0019's promise - a language package that references <c>Projectional.Toolkit</c> has
/// everything an exported language asks for, so the emitted assembly references nothing it does not bring.
/// </para>
/// <para>
/// Emission is deterministic, so exporting the same language twice produces the same bytes: the artefact can be
/// compared, checked in and cached the way any other build output of this repository can.
/// </para>
/// </remarks>
internal static class AssemblyCompiler
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(CollectReferences);

    /// <summary>Compiles <paramref name="source"/> into an assembly named <paramref name="assemblyName"/>.</summary>
    /// <param name="assemblyName">The assembly's simple name; the emitted file is <c>{assemblyName}.dll</c>.</param>
    /// <param name="source">The generated C#, exactly as the export wrote it.</param>
    /// <exception cref="ExportCompilationException">The source does not compile.</exception>
    public static GeneratedAssembly Compile(string assemblyName, string source) => Compile(assemblyName, [source]);

    /// <summary>Compiles several generated files into one assembly.</summary>
    /// <param name="assemblyName">The assembly's simple name; the emitted file is <c>{assemblyName}.dll</c>.</param>
    /// <param name="sources">The generated C# files, one per language, exactly as the export wrote them.</param>
    /// <remarks>
    /// The trees are compiled together rather than concatenated: each file is a unit with its own using directives and
    /// its own namespace, so joining the text would put one file's usings after another file's class.
    /// </remarks>
    /// <exception cref="ExportCompilationException">The sources do not compile.</exception>
    public static GeneratedAssembly Compile(string assemblyName, IReadOnlyList<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [.. sources.Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)))],
            References.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,

                // The repository's own rule, applied to what it writes: a warning in a generated package is a build
                // failure for whoever references it, so it is one here, where the export can still be fixed.
                generalDiagnosticOption: ReportDiagnostic.Error,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));

        using var image = new MemoryStream();

        var result = compilation.Emit(image);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString())
                .ToList();

            throw new ExportCompilationException(
                $"The generated source of '{assemblyName}' does not compile: {string.Join(" ", errors)}",
                errors);
        }

        return new GeneratedAssembly($"{assemblyName}.dll", image.ToArray());
    }

    private static List<MetadataReference> CollectReferences()
    {
        var references = new List<MetadataReference>();

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            references.AddRange(
                trusted
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => MetadataReference.CreateFromFile(path)));
        }

        references.Add(MetadataReference.CreateFromFile(PathOf(typeof(ConceptDeclaration))));
        references.Add(MetadataReference.CreateFromFile(PathOf(typeof(NotationRegistry))));

        return references;
    }

    /// <summary>The file an assembly can be read from, asked of the runtime rather than of the assembly.</summary>
    /// <remarks>
    /// <c>Assembly.Location</c> is the obvious way to answer this and the wrong one: it is empty for an assembly a
    /// single-file bundle kept inside itself, and the single-file analyzer rejects it at build time (IL3000) - which
    /// would make the one product that must survive that publish fail to build. The trusted platform assemblies are
    /// asked instead, and they are already the source of every other reference here: their entries are files on disk,
    /// and an assembly the bundle did not keep beside the application is simply not among them.
    /// </remarks>
    private static string PathOf(Type type)
    {
        var fileName = $"{type.Assembly.GetName().Name}.dll";

        var location = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted
            ? trusted
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
            : null;

        // The file has to exist, not merely be listed: a bundle's own assemblies are described by paths that name a
        // place inside it, and handing one of those to the compiler would replace this sentence with a message about a
        // file the compiler could not read.
        if (location is null || !File.Exists(location))
        {
            throw new ExportCompilationException(
                $"The assembly '{type.Assembly.GetName().Name}' has no file on disk, so the export cannot compile "
                + "against it. Compiling an export needs the framework's own assemblies beside the application, "
                + "which a single-file publish does not keep.");
        }

        return location;
    }
}
