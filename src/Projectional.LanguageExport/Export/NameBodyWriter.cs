using Projectional.Language;

namespace Projectional.LanguageExport;

/// <summary>Writes a name body as the C# the same vocabulary is written with by hand.</summary>
/// <remarks>
/// <para>
/// A name is data, not code: <see cref="NameBody"/> is a closed set of records, so a declared body can be written out
/// as the record it lowers to and the exported language keeps the name it had in its folder. This is the one behavioural
/// aspect besides typing that the export can spell, and for the same reason - it lives in
/// <c>Projectional.Language</c>, which the toolkit aggregates, so a generated package compiles against what a package
/// may already reference.
/// </para>
/// <para>
/// Every call is emitted in full, nested parts included, rather than naming a helper: the generated file is the file a
/// language author reads and copies from, and a body they cannot see is a body they cannot change.
/// </para>
/// </remarks>
internal static class NameBodyWriter
{
    /// <summary>The expression that builds <paramref name="body"/>.</summary>
    /// <exception cref="NotSupportedException"><paramref name="body"/> is a part this export cannot write.</exception>
    public static string Text(NameBody body) => body switch
    {
        NameLiteral literal => $"new NameLiteral({SourceWriter.Literal(literal.Text)})",
        NameProperty property => $"new NameProperty({SourceWriter.Literal(property.Slot)})",
        NameChild child => $"new NameChild({SourceWriter.Literal(child.Slot)})",
        NameReference reference => $"new NameReference({SourceWriter.Literal(reference.Slot)})",
        NameResolveInfo resolveInfo => $"new NameResolveInfo({SourceWriter.Literal(resolveInfo.Slot)})",
        NameChildren children => $"new NameChildren({SourceWriter.Literal(children.Slot)}, {SourceWriter.Literal(children.Separator)})",
        NameWhenFilled whenFilled => $"new NameWhenFilled({SourceWriter.Literal(whenFilled.Slot)}, {Text(whenFilled.Part)})",
        NameOfPath ofPath => $"new NameOfPath({PathText(ofPath.Path)})",
        IndexOfPath ofPath => $"new IndexOfPath({PathText(ofPath.Path)})",
        NameJoin join => $"new NameJoin({SourceWriter.Literal(join.Separator)}, {Parts(join.Parts)})",
        NameFallback fallback => $"new NameFallback({Parts(fallback.Parts)})",
        NameChoice choice => $"new NameChoice({SourceWriter.Literal(choice.Slot)}, {SourceWriter.Literal(choice.Value)}, {Text(choice.Then)}, {Text(choice.Otherwise)})",
        _ => throw new NotSupportedException(
            $"The name body '{body}' is a '{body.GetType().Name}', which the export does not know how to write."),
    };

    private static string Parts(IReadOnlyList<NameBody> parts) =>
        string.Join(", ", parts.Select(Text));

    /// <summary>The expression that builds <paramref name="path"/>, one link at a time.</summary>
    private static string PathText(NodePath path)
    {
        var slot = path.Slot is { } named ? SourceWriter.Literal(named) : "null";

        return path.Next is { } rest
            ? $"new NodePath(PathStep.{path.Step}, {slot}, {PathText(rest)})"
            : path.Slot is null
                ? $"new NodePath(PathStep.{path.Step})"
                : $"new NodePath(PathStep.{path.Step}, {slot})";
    }
}
