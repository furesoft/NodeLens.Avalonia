using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.CSharp.Checks;

/// <summary>The rules the base language states about itself.</summary>
/// <remarks>
/// <para>
/// Three rules, one per kind of thing a language can say without knowing anything else: a slot that must be filled,
/// a value that is still the default it started with, and text that carries whitespace nobody meant. They are
/// constraints rather than checks because a constraint is the language's own sentence about a node - the catalogue in
/// <c>Projectional.Checks</c> is what a *host* says about every language.
/// </para>
/// <para>
/// They are reported against the concept they are about, so a document that holds no C# is unaffected by them, which
/// is what makes shipping them in the shell's own set harmless.
/// </para>
/// </remarks>
public static class CSharpConstraints
{
    /// <summary>The identifier the missing-body rule reports under.</summary>
    public const string MissingBodyCode = "csharp.method.body";

    /// <summary>The identifier the placeholder-name rule reports under.</summary>
    public const string PlaceholderNameCode = "csharp.method.name";

    /// <summary>The identifier the padded-name rule reports under.</summary>
    public const string PaddedNameCode = "csharp.field.name";

    /// <summary>Every rule the base language states.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(CSharpConceptKeys.Method, new MethodNeedsABody())
        .Add(CSharpConceptKeys.Method, new MethodIsStillCalledTheDefault())
        .Add(CSharpConceptKeys.Field, new FieldNameCarriesWhitespace());

    /// <summary>A method with no body generates as a signature only, which is almost never what was meant.</summary>
    private sealed class MethodNeedsABody : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.GetChildren(CSharpConceptKeys.BodySlot).Count == 0)
            {
                context.Warning(
                    "a method has no body, so it generates as a signature only.",
                    role: CSharpConceptKeys.BodySlot,
                    code: MissingBodyCode);
            }
        }
    }

    /// <summary>
    /// A method whose name is still the concept's default, which is the one rule a language can state about a value
    /// it chose itself.
    /// </summary>
    /// <remarks>
    /// The default is read from the declaration rather than copied, so changing it changes the rule with it: a rule
    /// that compared against a literal would start reporting the wrong name the day the declaration moved.
    /// </remarks>
    private sealed class MethodIsStillCalledTheDefault : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.Concept.FindProperty(CSharpLanguage.NameSlot)?.DefaultValue is not { Length: > 0 } placeholder
                || !string.Equals(context.Node.GetOrNull(CSharpLanguage.NameSlot), placeholder, StringComparison.Ordinal))
            {
                return;
            }

            context.Hint(
                $"a method is still called \"{placeholder}\"; give it a name.",
                role: CSharpLanguage.NameSlot,
                code: PlaceholderNameCode);
        }
    }

    /// <summary>
    /// A name that starts or ends with whitespace. It is invisible in the drawing and part of the generated
    /// identifier, so nothing but this rule can point at it.
    /// </summary>
    private sealed class FieldNameCarriesWhitespace : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.GetOrNull(CSharpLanguage.NameSlot) is not { Length: > 0 } name
                || (name.Length == name.Trim().Length))
            {
                return;
            }

            context.Warning(
                "the field name starts or ends with whitespace, which reads as part of it.",
                role: CSharpLanguage.NameSlot,
                code: PaddedNameCode);
        }
    }
}
