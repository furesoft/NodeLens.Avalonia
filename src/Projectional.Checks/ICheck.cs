using Projectional.Core;
using Projectional.Editing;

namespace Projectional.Checks;

/// <summary>One rule that turns a model into findings, each carrying the repair that removes it.</summary>
/// <remarks>
/// <para>
/// A check is the model-only half of what the editor calls a diagnostic: it reads the language's
/// declarations and the model's structure and answers with <see cref="DiagnosticFix"/> values. Answering with
/// fixes rather than with bare findings is deliberate - where every finding has a cure, one list is the
/// whole answer and the marker set is derived from it, so a squiggle and the entry beside it can never
/// describe different states of the document.
/// </para>
/// <para>
/// A check never mutates the model and never draws. It may run on a document nobody is editing, which is
/// what lets a host check a model it has just loaded, and it is called again after every change rather than
/// being asked to update what it said before.
/// </para>
/// <para>
/// The interface exists so a language can list the checks it wants - its own and the standard ones - as one
/// ordered set, and so a host can add one without the language knowing. Order is the order of the list:
/// findings are offered to the user in the order they were produced.
/// </para>
/// </remarks>
public interface ICheck
{
    /// <summary>What is wrong in <paramref name="model"/> according to this check.</summary>
    /// <param name="model">The document to inspect, whole subtrees included.</param>
    /// <returns>The findings, in document order, each with at least one repair.</returns>
    IReadOnlyList<DiagnosticFix> Find(Model model);
}
