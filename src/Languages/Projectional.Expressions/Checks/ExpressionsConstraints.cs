using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Expressions.Checks;

/// <summary>The rules <see cref="ExpressionsLanguage"/> states about its own concepts.</summary>
/// <remarks>
/// <para>
/// A vocabulary that resolves names when something runs the model pushes its mistakes to the run: two functions of one
/// name, a call to nothing, a call with the wrong number of arguments and a parameter declared twice all reach the
/// interpreter as a message. A message at run time is late - the author has already written the rest of the document -
/// so these rules say the same things in the editor, and the interpreter keeps saying them because a document being
/// edited can always be run before it is finished.
/// </para>
/// <para>
/// None of the three is an error the framework owns: they are facts about *this* vocabulary, which is why they live
/// beside it rather than in <c>Projectional.Checks</c>. A host composes them the way it composes the language.
/// </para>
/// </remarks>
public static class ExpressionsConstraints
{
    /// <summary>The identifier a finding about a duplicated function name carries.</summary>
    public const string DuplicateFunctionCode = "EXPR001";

    /// <summary>The identifier a finding about a call that names no function carries.</summary>
    public const string UnknownFunctionCode = "EXPR002";

    /// <summary>The identifier a finding about an argument count that does not match carries.</summary>
    public const string ArityCode = "EXPR003";

    /// <summary>The identifier a finding about a parameter declared twice carries.</summary>
    public const string DuplicateParameterCode = "EXPR004";

    /// <summary>Every rule this language states.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(ExpressionsConceptKeys.Function, new FunctionNameIsUnique())
        .Add(ExpressionsConceptKeys.Function, new ParametersAreDistinct())
        .Add(ExpressionsConceptKeys.Call, new CallMatchesItsFunction());

    /// <summary>The function <paramref name="call"/> points at, or <see langword="null"/> when its reference is unresolved.</summary>
    /// <remarks>
    /// Read from the reference rather than from a name, so the rule and the run agree by construction: both answer with
    /// the node the author chose, and neither can quietly pick a different function of the same name.
    /// </remarks>
    private static Node? FunctionOf(Node call) =>
        call.GetReference("callee") is { } target && target.IsResolvedIn(call.Model.Project)
            ? call.Model.Roots.FirstOrDefault(root => root.Id.Equals(target.Target))
            : null;

    /// <summary>Two functions of one name: only the later one reports, so a pair is one finding.</summary>
    private sealed class FunctionNameIsUnique : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.GetOrNull("name") is not { Length: > 0 } name)
            {
                return;
            }

            var earlier = context.Node.Model.Roots
                .TakeWhile(root => !ReferenceEquals(root, context.Node))
                .Where(root => root.Concept.IsKindOf(ExpressionsConceptKeys.Function))
                .Any(root => string.Equals(root.GetOrNull("name"), name, StringComparison.Ordinal));

            if (earlier)
            {
                context.Error(
                    $"a function called \"{name}\" is already declared, and a call resolves to the first one.",
                    role: "name",
                    code: DuplicateFunctionCode);
            }
        }
    }

    /// <summary>One function declaring the same parameter twice, which the body cannot tell apart.</summary>
    private sealed class ParametersAreDistinct : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (var parameter in context.Node.GetChildren(ExpressionsLanguage.ParametersSlot))
            {
                if (parameter.GetOrNull("name") is { Length: > 0 } name && !seen.Add(name))
                {
                    context.Error(
                        $"the parameter \"{name}\" is declared twice, so the body cannot tell them apart.",
                        role: ExpressionsLanguage.ParametersSlot,
                        code: DuplicateParameterCode);
                }
            }
        }
    }

    /// <summary>A call that names no function of the model, or brings the wrong number of arguments.</summary>
    private sealed class CallMatchesItsFunction : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            var callee = context.Node.GetReference("callee")?.ResolveInfo;

            if (FunctionOf(context.Node) is not { } function)
            {
                context.Error(
                    callee is { Length: > 0 }
                        ? $"no function of this model is called \"{callee}\"."
                        : "this call names no function.",
                    role: "callee",
                    code: UnknownFunctionCode);

                return;
            }

            var expected = function.GetChildren(ExpressionsLanguage.ParametersSlot).Count;
            var given = context.Node.GetChildren(ExpressionsLanguage.ArgumentsSlot).Count;

            if (expected != given)
            {
                context.Error(
                    $"\"{callee}\" takes {expected} argument(s), but {given} were given.",
                    role: ExpressionsLanguage.ArgumentsSlot,
                    code: ArityCode);
            }
        }
    }
}
