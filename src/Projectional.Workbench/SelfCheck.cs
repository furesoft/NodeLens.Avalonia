using System;
using System.Globalization;
using System.Linq;
using Projectional.Editing;
using Projectional.Samples;

namespace Projectional.Workbench;

/// <summary>
/// The check the single-file build runs on itself, so a broken bundle fails in CI instead of on a
/// user's machine.
/// </summary>
/// <remarks>
/// <para>
/// A self-contained single file is only useful if everything it needs is inside it. The failure mode
/// that matters is a missing assembly or a missing language package, and neither shows up until
/// something asks for it - which for a GUI application means the first time a user opens a document.
/// This asks for it deliberately: it builds the shell's language registry, which composes the sample
/// language with both definition languages, and projects a document through it.
/// </para>
/// <para>
/// It runs before Avalonia starts, so it needs no display and can run on a build agent. That is the
/// whole reason it is a command-line switch rather than a menu entry.
/// </para>
/// </remarks>
internal static class SelfCheck
{
    /// <summary>The switch that asks for the check.</summary>
    internal const string Switch = "--self-check";

    /// <summary>Whether <paramref name="args"/> asks for the check.</summary>
    internal static bool IsRequested(string[] args) =>
        Array.Exists(args, argument => string.Equals(argument, Switch, StringComparison.Ordinal));

    /// <summary>
    /// Runs the check and answers the process exit code: zero when the bundle is whole, one when it is
    /// not.
    /// </summary>
    internal static int Run()
    {
        try
        {
            var language = new WorkbenchLanguage();
            var model = language.NewProject().NewModel("self-check");
            var document = model.NewNode(RulesConceptKeys.Ruleset);

            using var session = new EditorSession(document, language.Notations);

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"self-check ok: {language.Concepts.Concepts.Count} concept(s), {session.Tree.Cells().Count()} cell(s)"));

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"self-check failed: {exception}");
            return 1;
        }
    }
}
