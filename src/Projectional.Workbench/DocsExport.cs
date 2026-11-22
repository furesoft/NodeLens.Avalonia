using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Projectional.LanguageDocs;
using Projectional.Languages;

namespace Projectional.Workbench;

/// <summary>
/// The documentation the repository publishes: one static site over every language the shell ships and every example
/// beside the executable.
/// </summary>
/// <remarks>
/// <para>
/// It is a command-line switch for the same reason the self check is: it runs before Avalonia starts, so it needs no
/// display and can run on a build agent - which is what lets CI publish the pages on every merge instead of a person
/// remembering to. The site is written to the folder that follows the switch.
/// </para>
/// <para>
/// The languages come from the two places the shell itself takes them from, so what is published is what the app
/// offers and cannot drift from it: the shipped set (C#, which is always there rather than opened) and the example
/// projects the build placed beside the executable, each opened with the examples it stands on.
/// </para>
/// </remarks>
internal static class DocsExport
{
    /// <summary>The switch that asks for the export.</summary>
    internal const string Switch = "--docs";

    /// <summary>Whether <paramref name="args"/> asks for the export, which is the switch and the folder after it.</summary>
    internal static bool IsRequested(string[] args) =>
        Array.Exists(args, argument => string.Equals(argument, Switch, StringComparison.Ordinal));

    /// <summary>Runs the export and answers the process exit code: zero when the site was written, one when it was not.</summary>
    /// <param name="args">The command line, which has to name the folder after <see cref="Switch"/>.</param>
    internal static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var index = Array.IndexOf(args, Switch);

        if (index < 0 || index == args.Length - 1)
        {
            Console.Error.WriteLine($"usage: {Switch} <folder>");

            return 1;
        }

        var directory = args[index + 1];

        try
        {
            var language = new WorkbenchLanguage();
            var catalogue = ExampleProjects.Catalogue(Path.Combine(AppContext.BaseDirectory, ExampleProjects.Folder));

            // Base first, because that is the order an example's own dependencies are in and a language can only be
            // opened once the languages it names are in the set.
            var needed = catalogue
                .SelectMany(example => ExampleProjects.Needed(example, catalogue))
                .DistinctBy(example => example.Name, StringComparer.Ordinal)
                .ToArray();

            var opened = ExampleProjects.Load(language, needed, out var refused);

            foreach (var refusal in refused)
            {
                // A refusal is printed and the rest is still published: a site missing one language says so in the log,
                // while a job that stopped at the first broken example would publish nothing at all.
                Console.Error.WriteLine($"skipped {refusal}");
            }

            IReadOnlyList<LanguageProject> documented = [.. WorkbenchLanguage.Shipped, .. opened];

            var files = LanguageDocumentation.Write(documented, directory);

            Console.WriteLine(LanguageDocumentation.Summarise(documented, files));

            foreach (var each in documented)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {each.Name}: {each.Declarations.Count} concept(s)"));
            }

            return refused.Count == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"documentation export failed: {exception.Message}");

            return 1;
        }
    }
}