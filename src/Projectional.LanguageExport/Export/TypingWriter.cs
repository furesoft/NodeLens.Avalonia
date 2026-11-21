using Projectional.TypingRules;

namespace Projectional.LanguageExport;

/// <summary>Writes a language's declared typing rules as the C# a package fills its registry with.</summary>
/// <remarks>
/// <para>
/// The typing aspect is the one the export can spell completely: <see cref="Projectional.Language.ITypingRules"/>
/// and <see cref="Projectional.Language.TypingRuleRegistry"/> live in <c>Projectional.Language</c>, which is inside
/// the toolkit a generated package references, and the four claims the declared language offers are exactly the
/// predicates the hand-written languages write. So a rule is written out rather than named as omitted - dropping it
/// would produce a language that reads and draws and refuses to be typed into.
/// </para>
/// <para>
/// The predicate is the C# twin, character for character: <c>char.IsAsciiDigit</c>, the name shape
/// <c>ExpressionsLanguage</c> writes, <c>StartsWith</c> with <see cref="StringComparison.Ordinal"/> and an equality
/// against each word. A generated language that claimed different text than the folder it came from would be a
/// divergence the parity harness cannot see, because it compares declarations and drawings, not keystrokes.
/// </para>
/// </remarks>
internal static class TypingWriter
{
    /// <summary>The members the rules need imported, or nothing when the language declares no rule.</summary>
    /// <param name="rules">The rules to write, in declaration order.</param>
    /// <returns>Whether the predicates read characters - which needs <c>System.Linq</c>'s <c>All</c>.</returns>
    public static bool NeedsLinq(IReadOnlyList<DeclaredTypingRule> rules) =>
        rules.Any(rule => rule.Claims is TypingClaims.Digits or TypingClaims.Name);

    /// <summary>Whether the predicates compare a prefix - which needs <c>System</c>'s <c>StringComparison</c>.</summary>
    /// <param name="rules">The rules to write, in declaration order.</param>
    public static bool NeedsSystem(IReadOnlyList<DeclaredTypingRule> rules) =>
        rules.Any(rule => rule.Claims == TypingClaims.Prefix);

    /// <summary>Writes the method that builds the registry.</summary>
    /// <param name="source">The file being written.</param>
    /// <param name="rules">The rules to write, in declaration order.</param>
    public static void Write(SourceWriter source, IReadOnlyList<DeclaredTypingRule> rules)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rules);

        source.Line("/// <summary>Which node a burst of typing into an empty slot creates.</summary>");
        source.Line("public static ITypingRules CreateTypingRules()");
        source.Line("{");

        using (source.Indent())
        {
            source.Line("return TypingRuleRegistry.Empty");

            using (source.Indent())
            {
                for (var index = 0; index < rules.Count; index++)
                {
                    source.Line($".Add({Call(rules[index])}){(index == rules.Count - 1 ? ";" : string.Empty)}");
                }
            }
        }

        source.Line("}");
    }

    /// <summary>The arguments of one <c>Add</c> call: the concept, its predicate and, when it has one, the property.</summary>
    private static string Call(DeclaredTypingRule rule)
    {
        var property = rule.PropertySlot is { } slot ? $", {SourceWriter.Literal(slot)}" : string.Empty;

        return $"{SourceWriter.Literal(rule.ConceptKey)}, {Predicate(rule)}{property}";
    }

    /// <summary>The lambda a claim stands for, in the spelling the hand-written languages use.</summary>
    private static string Predicate(DeclaredTypingRule rule) => rule.Claims switch
    {
        TypingClaims.Digits => "text => text.All(char.IsAsciiDigit)",
        TypingClaims.Name =>
            "text => text.Length > 0 && (char.IsAsciiLetter(text[0]) || text[0] == '_') && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')",
        TypingClaims.Prefix =>
            $"text => text.StartsWith({SourceWriter.Literal(rule.Text)}, StringComparison.Ordinal)",
        TypingClaims.AnyOf => $"text => text is {Words(rule.Text)}",

        // A rule the language accepted but this writer does not know cannot be written as a predicate, and writing a
        // predicate that claims less than the rule would be silently wrong - so the export refuses instead.
        var other => throw new InvalidOperationException(
            $"The claim '{other}' has no C# spelling; the export and the typing language have drifted apart."),
    };

    /// <summary>The words of an <c>any of</c> rule as one pattern: <c>"true" or "false"</c>.</summary>
    private static string Words(string? text) =>
        string.Join(
            " or ",
            (text ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(SourceWriter.Literal));
}
