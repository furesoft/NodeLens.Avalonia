using Projectional.Checks;

namespace Projectional.CSharp.Checks;

/// <summary>The framework's standard checks, as this base language takes them.</summary>
/// <remarks>
/// <para>
/// C# states two rules of the name group itself - a method still called the name its declaration gave it, and a field
/// whose name carries whitespace - so <see cref="StandardChecks.Names"/> is not taken: two sources for one finding is
/// the duplication the workbench removed once already, and a rule written in the language says more about a C# name
/// than the generic sentence can. The same rule that keeps a language from restating a check keeps this list short.
/// </para>
/// <para>
/// <see cref="DuplicateNameCheck"/> and <see cref="IdentifierNameCheck"/> are taken from that group on their own. The
/// first is not a style judgement - two methods of one class with one name is code that does not compile - and the
/// second reads the name rules the concepts already declare, so a name written in another convention is reported by
/// the one rule that was written to report it.
/// </para>
/// <para>
/// The structural and reference groups are taken whole, and neither restates anything: an empty operand, a method that
/// is a signature with no body, or a name reference whose target was deleted are defects of the model rather than of
/// C#'s taste, and the checks derive them from the concept declarations a language already has.
/// </para>
/// </remarks>
public static class CSharpChecks
{
    /// <summary>Every standard check this language applies, in the order the findings are offered.</summary>
    public static CheckSet All { get; } =
    new(
    [
        new DuplicateNameCheck(),
        new IdentifierNameCheck(),
        .. StandardChecks.Structure,
        .. StandardChecks.References,
    ]);
}
