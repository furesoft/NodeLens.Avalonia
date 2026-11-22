using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;

namespace Projectional.Workbench;

/// <summary>The three-way merge as a command, for a git merge driver to call.</summary>
/// <remarks>
/// <para>
/// It is a switch for the same reason the self check and the documentation export are: it runs before Avalonia starts,
/// so it needs no display and can run on a build agent or inside a hook. What a merge driver needs is exactly that - a
/// process that reads three files, writes one and says what happened with its exit code.
/// </para>
/// <para>
/// <b>The language is an argument, because the file cannot say which one it is.</b> A document names its concepts, but
/// the slots that make a property comparable and a child a child live in the language's declarations - so the folder a
/// <see cref="LanguageProject"/> opens is what the three files are read with, and the merge is asked the questions the
/// language answers.
/// </para>
/// <para>
/// The exit codes are the contract: <c>0</c> merged and written, <c>2</c> conflicts and **nothing** written, <c>1</c> a
/// file or folder that could not be read. Git treats any non-zero answer as "this needs a person", which is what both
/// failures mean, so the two are distinguished here only so the message can say which it was.
/// </para>
/// </remarks>
internal static class MergeExport
{
    /// <summary>The switch that asks for the merge.</summary>
    internal const string Switch = "--merge";

    /// <summary>The option naming the file the merged document is written to.</summary>
    internal const string Output = "-o";

    /// <summary>Whether <paramref name="args"/> asks for a merge.</summary>
    internal static bool IsRequested(string[] args) =>
        Array.Exists(args, argument => string.Equals(argument, Switch, StringComparison.Ordinal));

    /// <summary>Runs the merge and answers the process exit code.</summary>
    /// <param name="args">The command line: the language folder, the three files, and <c>-o</c> with the output.</param>
    /// <returns>Zero when the result was written, two on a conflict, one when something could not be read.</returns>
    internal static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var index = Array.IndexOf(args, Switch);

        if (index < 0 || args.Length < index + 5)
        {
            Usage();

            return 1;
        }

        var folder = args[index + 1];
        var basePath = args[index + 2];
        var oursPath = args[index + 3];
        var theirsPath = args[index + 4];

        if (Value(args, Output) is not { } output)
        {
            Usage();

            return 1;
        }

        try
        {
            var language = LanguageProject.Open(folder, out var error);

            if (language is null)
            {
                Console.Error.WriteLine($"merge failed: {error}");

                return 1;
            }

            // Three projects, because a project is a set of ids and the three files share them: the reader refuses a
            // second node of one id, which is exactly the duplicate these three files are to each other.
            var origin = ModelFile.Load(language.NewProject(), basePath);
            var ours = ModelFile.Load(language.NewProject(), oursPath);
            var theirs = ModelFile.Load(language.NewProject(), theirsPath);

            var result = ThreeWayMerge.Merge(origin, ours, theirs);

            if (!result.Succeeded)
            {
                foreach (var conflict in result.Conflicts)
                {
                    Console.Error.WriteLine(conflict.Describe());
                }

                Console.Error.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{result.Conflicts.Count} conflict(s): {oursPath} left as it was."));

                return 2;
            }

            ModelFile.Save(result.Model!, output);

            Console.WriteLine($"merged {basePath} + {oursPath} + {theirsPath} into {output}");

            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or XmlException
            or InvalidOperationException)
        {
            Console.Error.WriteLine($"merge failed: {exception.Message}");

            return 1;
        }
    }

    /// <summary>The value that follows an option, or <see langword="null"/> when it is not there.</summary>
    private static string? Value(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);

        return index >= 0 && index < args.Length - 1 ? args[index + 1] : null;
    }

    private static void Usage()
    {
        Console.Error.WriteLine($"usage: {Switch} <language folder> <base> <ours> <theirs> {Output} <merged>");
        Console.Error.WriteLine("  exits 0 when the merge was written, 2 on conflicts (nothing is written), 1 on a bad file");
    }
}
