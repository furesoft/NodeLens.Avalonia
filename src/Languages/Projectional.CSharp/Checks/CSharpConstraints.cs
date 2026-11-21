using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;

namespace Projectional.CSharp.Checks;

/// <summary>The rules the base language states about itself.</summary>
/// <remarks>
/// <para>
/// Two kinds of rule live here. The correctness rules say what a node cannot mean: a slot that must be filled, a
/// value that is still the default it started with, text that carries whitespace nobody meant, a condition that is
/// not a truth value. The style rules say what the .NET conventions ask for even though the code would still mean
/// the same thing: imports in order, members in the conventional order, an interface named with its <c>I</c>. A
/// style rule warns and a correctness rule may error, because the order of two imports changes nothing.
/// </para>
/// <para>
/// They are constraints rather than checks because a constraint is the language's own sentence about a node - the
/// catalogue in <c>Projectional.Checks</c> is what a *host* says about every language.
/// </para>
/// <para>
/// They are reported against the concept they are about, so a document that holds no C# is unaffected by them, which
/// is what makes shipping them in the shell's own set harmless.
/// </para>
/// <para>
/// A style rule cannot see a blank line, because the model holds no whitespace: the projection draws it. What a rule
/// can see is the arrangement that makes a blank line land in the right place, so "a blank line after a block of
/// fields" is checked here as "the fields are contiguous", and the projection is what puts the line where the block
/// ends.
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

    /// <summary>The identifier the non-boolean-condition rule reports under.</summary>
    public const string ConditionNotBooleanCode = "csharp.if.condition.not-boolean";

    /// <summary>The identifier the import-order rule reports under.</summary>
    public const string UsingOrderCode = "csharp.using.order";

    /// <summary>The identifier the member-order rule reports under.</summary>
    public const string MemberOrderCode = "csharp.member.order";

    /// <summary>The identifier the interface-name rule reports under.</summary>
    public const string InterfaceNameCode = "csharp.interface.name";

    /// <summary>Every rule the base language states.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(CSharpConceptKeys.Method, new MethodNeedsABody())
        .Add(CSharpConceptKeys.Method, new MethodIsStillCalledTheDefault())
        .Add(CSharpConceptKeys.Field, new FieldNameCarriesWhitespace())
        .Add(CSharpConceptKeys.If, new ConditionMustBeBoolean())
        .Add(CSharpConceptKeys.File, new UsingsAreSorted())
        .Add(CSharpConceptKeys.Class, new MembersAreGroupedByKind())
        .Add(CSharpConceptKeys.Interface, new InterfaceNameStartsWithI());

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
    /// An <c>if</c> whose condition is not a truth value, which is the first rule the language could not state before
    /// it had types.
    /// </summary>
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted, and that is the whole discipline of a rule that reads types: a
    /// condition this language cannot type - a call, a member of an assembly it does not model - is not a mistake, and
    /// reporting it would put a squiggle under every real program. Only a type it <em>knows</em> and knows to be wrong
    /// is reported.
    /// </remarks>
    private sealed class ConditionMustBeBoolean : IConstraint
    {
        private static readonly PrimitiveType Boolean = PrimitiveType.Of(PropertyType.Boolean);

        public void Check(ConstraintContext context)
        {
            if (context.Node.GetChild(CSharpConceptKeys.ConditionSlot) is not { } condition)
            {
                return;
            }

            var type = context.TypeOf(condition);

            if (type is UnknownType || Boolean.Accepts(type))
            {
                return;
            }

            context.Error(
                $"an if condition has to be true or false, but this one is {type.Name}.",
                role: CSharpConceptKeys.ConditionSlot,
                code: ConditionNotBooleanCode);
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

    /// <summary>
    /// The imports a file lists, in the order the .NET style guide gives them: <c>System</c> first, then everything
    /// else alphabetically.
    /// </summary>
    /// <remarks>
    /// It is a style rule and not a correctness one, which is why it warns rather than errors: the order changes
    /// nothing about what the code means, and a warning is what a convention gets. The comparison is ordinal, because
    /// a namespace name is an identifier and an ordinal comparison is the one a compiler would agree with. Only the
    /// first import out of place is reported, because a list that is wrong from one point on is wrong for one reason.
    /// </remarks>
    private sealed class UsingsAreSorted : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            Node? previous = null;
            var previousKey = string.Empty;

            foreach (var import in context.Node.GetChildren(CSharpConceptKeys.UsingsSlot))
            {
                var key = SortKeyOf(import);

                if (previous is not null && string.CompareOrdinal(previousKey, key) > 0)
                {
                    context.Report(
                        DiagnosticSeverity.Warning,
                        import,
                        $"the import \"{NameOf(import)}\" comes after \"{NameOf(previous)}\"; System goes first, then the rest alphabetically.",
                        role: CSharpLanguage.NameSlot,
                        code: UsingOrderCode);

                    return;
                }

                previous = import;
                previousKey = key;
            }
        }

        /// <summary>The key the style guide sorts by: <c>System</c> first, then the name itself.</summary>
        /// <remarks>
        /// The prefix is a sort key rather than a special case in the loop, so "System goes first" is one comparison
        /// like every other and the loop stays a single ordered walk. A namespace counts as System's when it is
        /// <c>System</c> or sits beneath it, which is the whole of "the System namespaces".
        /// </remarks>
        private static string SortKeyOf(Node import)
        {
            var name = NameOf(import);

            return name.StartsWith("System", StringComparison.Ordinal)
                && (name.Length == "System".Length || name["System".Length] == '.')
                ? "0" + name
                : "1" + name;
        }
    }

    /// <summary>
    /// A class's members, in the order the .NET style guide gives them: fields, then constructors, then properties and
    /// indexers, then methods.
    /// </summary>
    /// <remarks>
    /// This is the model's half of the "blank line after a block of fields" convention. The model holds no
    /// whitespace, so a rule cannot see a blank line; what it can see is whether the fields are contiguous. When they
    /// are, the projection can put the blank line where the block ends - and when they are not, this rule says so.
    /// Only the first member out of place is reported, because the order is a single sequence and the first break is
    /// the one that needs moving.
    /// </remarks>
    private sealed class MembersAreGroupedByKind : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            Node? previous = null;
            var previousRank = -1;

            foreach (var member in context.Node.GetChildren(CSharpConceptKeys.MembersSlot))
            {
                var rank = RankOf(member);

                if (previous is not null && rank < previousRank)
                {
                    context.Report(
                        DiagnosticSeverity.Warning,
                        member,
                        $"a {KindOf(member)} comes after a {KindOf(previous)}; the convention is fields, then constructors, then properties, then methods.",
                        code: MemberOrderCode);

                    return;
                }

                previous = member;
                previousRank = rank;
            }
        }

        /// <summary>Where a member belongs in the convention's order, or among the properties when it is not a kind it names.</summary>
        private static int RankOf(Node member) => member.Concept.Key switch
        {
            CSharpConceptKeys.Field => 0,
            CSharpConceptKeys.Constructor => 1,
            CSharpConceptKeys.Property => 2,
            CSharpConceptKeys.Indexer => 3,
            CSharpConceptKeys.Method => 4,
            _ => 2,
        };

        private static string KindOf(Node member) => member.Concept.Key switch
        {
            CSharpConceptKeys.Field => "field",
            CSharpConceptKeys.Constructor => "constructor",
            CSharpConceptKeys.Property => "property",
            CSharpConceptKeys.Indexer => "indexer",
            CSharpConceptKeys.Method => "method",
            _ => "member",
        };
    }

    /// <summary>
    /// An interface whose name does not start with <c>I</c>, which is the one naming convention the .NET style guide
    /// states for a declaration rather than for a variable.
    /// </summary>
    /// <remarks>
    /// A name the author has not given yet is not a violation: the concept's default is empty, and an empty name is
    /// already what the language's own placeholder rules are for. The rule reads the first character only, because
    /// "starts with I" is the whole of the convention and a longer name is not more correct for being longer.
    /// </remarks>
    private sealed class InterfaceNameStartsWithI : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (NameOf(context.Node) is not { Length: > 0 } name || name[0] == 'I')
            {
                return;
            }

            context.Warning(
                $"the interface \"{name}\" does not start with \"I\", which is how the .NET style guide names one.",
                role: CSharpLanguage.NameSlot,
                code: InterfaceNameCode);
        }
    }

    /// <summary>The name a node carries, or the empty string when it carries none.</summary>
    private static string NameOf(Node node) => node.GetOrNull(CSharpLanguage.NameSlot) ?? string.Empty;
}
