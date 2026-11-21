using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.CSharp.Intentions;

/// <summary>The edits a reader of the base language would otherwise have to make by hand.</summary>
/// <remarks>
/// <para>
/// Three set a property the concept already has, and two create a child - and the two are not a convenience. The
/// notation draws a parameter or an argument list only while it holds something, so <c>Greet()</c> reads as C#; the
/// intention is the way the first entry gets in. That pair is the bargain a hidden slot makes: the cell is invisible
/// so the drawing is the language, and something else has to be the way back in.
/// </para>
/// <para>
/// A provider answers for one caret and answers <see cref="IntentionAvailability.Absent"/> everywhere else, so
/// registering it for every document costs nothing: a document with no C# in it gets an empty list from this source.
/// </para>
/// </remarks>
public sealed class CSharpIntentions : IIntentionProvider
{
    /// <summary>The stable id of "make the class public".</summary>
    public const string MakeClassPublicId = "csharp.class.public";

    /// <summary>The stable id of "make the class static".</summary>
    public const string MakeClassStaticId = "csharp.class.static";

    /// <summary>The stable id of "make the method static".</summary>
    public const string MakeMethodStaticId = "csharp.method.static";

    /// <summary>The stable id of "add a parameter".</summary>
    public const string AddParameterId = "csharp.method.parameter";

    /// <summary>The stable id of "add an argument".</summary>
    public const string AddArgumentId = "csharp.call.argument";

    private static readonly Intention ClassPublic = Set(
        MakeClassPublicId,
        "Make the class public",
        "Writes the visibility the class withholds.",
        CSharpConceptKeys.Class,
        CSharpLanguage.VisibilitySlot,
        CSharpVisibility.Public);

    private static readonly Intention ClassStatic = Set(
        MakeClassStaticId,
        "Make the class static",
        "A static class is a home for static members.",
        CSharpConceptKeys.Class,
        CSharpLanguage.IsStaticSlot,
        "true");

    private static readonly Intention MethodStatic = Set(
        MakeMethodStaticId,
        "Make the method static",
        "A static method belongs to the type, not to an instance.",
        CSharpConceptKeys.Method,
        CSharpLanguage.IsStaticSlot,
        "true");

    private static readonly Intention Parameter = Insert(
        AddParameterId,
        "Add a parameter",
        "Puts an empty parameter at the end of the signature.",
        CSharpConceptKeys.Method,
        CSharpConceptKeys.ParametersSlot);

    private static readonly Intention Argument = Insert(
        AddArgumentId,
        "Add an argument",
        "Puts a placeholder argument at the end of the call.",
        CSharpConceptKeys.Call,
        CSharpConceptKeys.ArgumentsSlot);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static CSharpIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Node is not { } node)
        {
            return [];
        }

        return
        [
            On(node, CSharpConceptKeys.Class, ClassPublic),
            On(node, CSharpConceptKeys.Class, ClassStatic),
            On(node, CSharpConceptKeys.Method, MethodStatic),
            On(node, CSharpConceptKeys.Method, Parameter),
            On(node, CSharpConceptKeys.Call, Argument),
        ];
    }

    /// <summary>One entry, absent unless the caret stands on a node of <paramref name="conceptKey"/>.</summary>
    /// <remarks>
    /// The whole list is handed back with the inapplicable entries marked absent rather than filtered out, because a
    /// declared catalogue works the same way and one shape is easier to reason about than two.
    /// </remarks>
    private static Intention On(Node node, string conceptKey, Intention intention) =>
        intention.With(node.Concept.IsKindOf(conceptKey) ? IntentionAvailability.Applies : IntentionAvailability.Absent);

    /// <summary>An intention that writes one property of the node under the caret.</summary>
    private static Intention Set(string id, string label, string description, string conceptKey, string slot, string value) =>
        new(
            id,
            label,
            description,
            context => context.Node is { } node && node.Concept.IsKindOf(conceptKey)
                ? new SetPropertyAction(CellAddress.OfSlot(node.Id, slot), value)
                : null);

    /// <summary>An intention that appends a child to a list slot of the node under the caret.</summary>
    private static Intention Insert(string id, string label, string description, string conceptKey, string slot) =>
        new(
            id,
            label,
            description,
            context => context.Node is { } node && node.Concept.IsKindOf(conceptKey)
                ? new InsertChildAction(node.Id, slot)
                : null);
}
