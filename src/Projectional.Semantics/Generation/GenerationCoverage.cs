using Projectional.Core;
using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>Which concepts of a document the chosen generator cannot print, as findings a host can show.</summary>
/// <remarks>
/// <para>
/// Generation stops at the first concept no template answers for, and that is the right behaviour - half a file is
/// worse than none. But a stop is a poor way to <em>find out</em>: by the time a user presses Generate they have
/// already written the document, and the failure names one node out of however many are affected. This is the same
/// information as a list, before the button, so the editor can mark the document instead of surprising its author.
/// </para>
/// <para>
/// A template is answered by the concept itself or by its nearest super concept, exactly as the run asks
/// (<c>TemplateRegistry.TemplateFor</c>), so this reports what a run would do rather than a second opinion about the
/// language.
/// </para>
/// <para>
/// <b>One finding per concept, at its first node in document order.</b> The defect belongs to the language - a missing
/// mapping - and not to each occurrence: a document with five hundred nodes of one unmapped concept has one thing wrong
/// with it, and repeating the sentence five hundred times would bury every other finding in the panel. The node is what
/// makes it a place, so the user can be taken to it.
/// </para>
/// <para>
/// These are diagnostics rather than fixes: a repair would have to write a template, and no edit action can decide what
/// a language author meant to emit. A finding with no cure reaches the squiggles and the problems panel and stays out of
/// the quick-fix list, which is exactly what this is.
/// </para>
/// </remarks>
public static class GenerationCoverage
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string MissingTemplateCode = "GEN001";

    /// <summary>What <paramref name="generator"/> cannot print in <paramref name="model"/>, in document order.</summary>
    /// <param name="model">The document to inspect, whole subtrees included.</param>
    /// <param name="generator">The generator that would be run.</param>
    /// <returns>One finding per concept it cannot print; empty when the document is fully covered.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<Diagnostic> Of(Model model, TextGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(generator);

        List<Diagnostic> findings = [];
        HashSet<string> reported = new(StringComparer.Ordinal);

        foreach (var node in model.Nodes)
        {
            if (generator.Templates.TemplateFor(node.Concept) is not null
                || !reported.Add(node.Concept.Key))
            {
                continue;
            }

            findings.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                node.Id,
                $"'{node.Concept.Key}' has no template in generator '{generator.Name}', so generating this document "
                    + "would stop here. Add a mapping for it, or say that it emits nothing.",
                code: MissingTemplateCode));
        }

        return findings;
    }

    /// <summary>What none of <paramref name="generators"/> can print in <paramref name="model"/>.</summary>
    /// <param name="model">The document to inspect.</param>
    /// <param name="generators">The generators the language offers; empty answers nothing.</param>
    /// <remarks>
    /// One finding per concept <em>per generator</em>, because each generator is a separate target and a language can
    /// be complete for one and not the other. A document whose language declares no generator therefore reports
    /// nothing, which is right: there is nothing to print it with, and "cannot generate" is not a defect of the
    /// document.
    /// </remarks>
    public static IReadOnlyList<Diagnostic> Of(Model model, IEnumerable<TextGenerator> generators)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(generators);

        List<Diagnostic> findings = [];

        foreach (var generator in generators)
        {
            findings.AddRange(Of(model, generator));
        }

        return findings;
    }
}
