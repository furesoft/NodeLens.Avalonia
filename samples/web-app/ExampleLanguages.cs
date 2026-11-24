using System.Reflection;

namespace Projectional.Browser;

/// <summary>Writes the declared samples the page embeds into the runtime's file system, where the shell looks for them.</summary>
/// <remarks>
/// The desktop build copies <c>samples/declared-*</c> beside the executable and <c>ExampleProjects</c> enumerates that
/// folder. A browser has no such folder, so the same files travel as embedded resources and are written to
/// <c>AppContext.BaseDirectory/Examples</c> once at startup - the in-memory system the .NET runtime provides. The shell
/// then opens them exactly as it does on the desktop, including the dependency order a language project declares.
/// </remarks>
internal static class ExampleLanguages
{
    private const string Prefix = "Examples/";

    /// <summary>Writes every embedded example into <c>Examples/</c> under the application base.</summary>
    internal static void Extract()
    {
        var assembly = typeof(ExampleLanguages).Assembly;
        var root = Path.Combine(AppContext.BaseDirectory, "Examples");

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal)
                || Resource(assembly, name) is not { } input)
            {
                continue;
            }

            var target = Path.Combine(
                root,
                name[Prefix.Length..].Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using (input)
            using (var output = File.Create(target))
            {
                input.CopyTo(output);
            }
        }
    }

    private static Stream? Resource(Assembly assembly, string name) => assembly.GetManifestResourceStream(name);
}
