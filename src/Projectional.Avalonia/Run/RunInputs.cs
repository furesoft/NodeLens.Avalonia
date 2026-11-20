using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>The variables a run is given, read from the one line the output panel offers.</summary>
/// <remarks>
/// <para>
/// A name in an expression is a free variable, so one decision table is runnable against many inputs and the values
/// have to come from the user. One line of <c>name=value</c> pairs is the smallest thing that answers that - a grid
/// with typed columns would need a schema, and the document does not declare one.
/// </para>
/// <para>
/// The rules are <see cref="VariableInputs"/>'s, one layer down: what <c>1500</c> means is a fact about the
/// language's values rather than about a panel, and a declared test's <c>Evaluates</c> assertion reads the same
/// pairs. This type stays as the panel's door, so a run in the editor and a run in a test cannot disagree about what
/// a value is.
/// </para>
/// </remarks>
public static class RunInputs
{
    /// <summary>Reads <paramref name="text"/> as the run's variables.</summary>
    /// <param name="text">The line the user typed, such as <c>amount=1500, currency=EUR</c>.</param>
    /// <param name="variables">The values read, empty when <paramref name="text"/> names nothing.</param>
    /// <param name="error">Why the line could not be read, or <see langword="null"/> when it was.</param>
    /// <returns>Whether the line was read. On <see langword="false"/> <paramref name="variables"/> is empty.</returns>
    /// <remarks>
    /// A malformed line answers no variables at all rather than the pairs before the mistake: a run against half the
    /// inputs the user typed would fail for a reason that is not in the document, which is the one failure a user
    /// cannot diagnose from the editor.
    /// </remarks>
    public static bool TryParse(string? text, out Dictionary<string, Value> variables, out string? error) =>
        VariableInputs.TryParse(text, out variables, out error);
}
