using Projectional.Core;

namespace Projectional.LanguageExport;

/// <summary>Writes a language's concepts as the <see cref="ConceptDeclaration"/> calls a hand-written language makes.</summary>
/// <remarks>
/// <para>
/// Only what the declared path can actually express is emitted, which is why no <c>applicability</c> and no
/// <c>triggers</c> appear: the structure language has no spelling for them yet, so a declaration lowered from a
/// model never carries one. Emitting them as defaults would look like a decision the author made.
/// </para>
/// <para>
/// Every body line is built with its trailing comma and the last one loses it, because an argument list has no
/// trailing comma in C# - only an array initializer does. That is the one place the emitted text cannot just be
/// "the same thing with commas everywhere".
/// </para>
/// </remarks>
internal static class DeclarationWriter
{
    /// <summary>Writes the <c>ConceptDeclarations</c> property into <paramref name="source"/>.</summary>
    public static void Write(SourceWriter source, IReadOnlyList<ConceptDeclaration> declarations)
    {
        source.Line("public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =");
        source.Line("[");

        using (source.Indent())
        {
            foreach (var declaration in declarations)
            {
                WriteConcept(source, declaration);
            }
        }

        source.Line("];");
    }

    private static void WriteConcept(SourceWriter source, ConceptDeclaration declaration)
    {
        var body = new List<string> { SourceWriter.Literal(declaration.Key) + "," };

        body.AddRange(SlotLines(declaration.Slots));
        body.AddRange(ArgumentLines(declaration));

        body[^1] = body[^1].TrimEnd(',');

        source.Line("new ConceptDeclaration(");

        using (source.Indent())
        {
            foreach (var line in body)
            {
                foreach (var physical in line.Split('\n'))
                {
                    source.Line(physical);
                }
            }
        }

        source.Line("),");
    }

    private static List<string> SlotLines(IReadOnlyList<SlotDeclaration> slots)
    {
        var lines = new List<string>();

        if (slots.Count == 0)
        {
            return lines;
        }

        lines.Add("[");

        foreach (var slot in slots)
        {
            lines.Add($"    {SlotText(slot)},");
        }

        lines.Add("],");

        return lines;
    }

    private static List<string> ArgumentLines(ConceptDeclaration declaration)
    {
        var lines = new List<string>();

        if (declaration.SuperConcepts.Count > 0)
        {
            lines.Add($"superConcepts: [{Literals(declaration.SuperConcepts)}],");
        }

        if (declaration.Interfaces.Count > 0)
        {
            lines.Add($"interfaces: [{Literals(declaration.Interfaces)}],");
        }

        if (declaration.NameSlot is not null)
        {
            lines.Add($"nameSlot: {SourceWriter.Literal(declaration.NameSlot)},");
        }

        if (declaration.CollapseSlot is not null)
        {
            lines.Add($"collapseSlot: {SourceWriter.Literal(declaration.CollapseSlot)},");
        }

        if (declaration.Description is not null)
        {
            lines.Add($"description: {SourceWriter.Literal(declaration.Description)},");
        }

        if (declaration.IsAbstract)
        {
            lines.Add("isAbstract: true,");
        }

        if (declaration.IsInterface)
        {
            lines.Add("isInterface: true,");
        }

        if (!declaration.IsOfferedInCompletion)
        {
            lines.Add("isOfferedInCompletion: false,");
        }

        return lines;
    }

    private static string SlotText(SlotDeclaration slot) => slot switch
    {
        PropertySlot property => PropertyText(property),
        ChildSlot child => ChildText(child),
        ReferenceSlot reference => ReferenceText(reference),
        _ => throw new NotSupportedException(
            $"The slot '{slot.Name}' is a '{slot.GetType().Name}', which the export does not know how to write."),
    };

    private static string PropertyText(PropertySlot slot)
    {
        var arguments = new List<string> { SourceWriter.Literal(slot.Name), TypeText(slot.Type) };

        if (slot.DefaultValue is not null || slot.Description is not null || slot.ShowDescriptionWhenFilled)
        {
            arguments.Add(SourceWriter.Literal(slot.DefaultValue));
        }

        if (slot.Description is not null || slot.ShowDescriptionWhenFilled)
        {
            arguments.Add(SourceWriter.Literal(slot.Description));
        }

        if (slot.ShowDescriptionWhenFilled)
        {
            arguments.Add("showDescriptionWhenFilled: true");
        }

        return $"new PropertySlot({string.Join(", ", arguments)})";
    }

    private static string ChildText(ChildSlot slot)
    {
        var text = $"new ChildSlot({SourceWriter.Literal(slot.Name)}, {SourceWriter.Literal(slot.ChildConceptKey)}, Cardinality.{slot.Cardinality}";

        // The minimum is the half of a range the cardinality cannot say: `1..n` and `0..n` are both `Many`.
        return slot.Minimum == (slot.Cardinality == Cardinality.One ? 1 : 0)
            ? $"{text})"
            : $"{text}, minimum: {slot.Minimum})";
    }

    private static string ReferenceText(ReferenceSlot slot) =>
        slot.AllowOwnSubtree
            ? $"new ReferenceSlot({SourceWriter.Literal(slot.Name)}, {SourceWriter.Literal(slot.TargetConceptKey)}, allowOwnSubtree: true)"
            : $"new ReferenceSlot({SourceWriter.Literal(slot.Name)}, {SourceWriter.Literal(slot.TargetConceptKey)})";

    private static string TypeText(PropertyType type) => type switch
    {
        _ when type.Symbols.Count > 0 => $"PropertyType.Enum({Literals(type.Symbols)})",
        _ when ReferenceEquals(type, PropertyType.String) => "PropertyType.String",
        _ when ReferenceEquals(type, PropertyType.Integer) => "PropertyType.Integer",
        _ when ReferenceEquals(type, PropertyType.Boolean) => "PropertyType.Boolean",
        _ when ReferenceEquals(type, PropertyType.Date) => "PropertyType.Date",
        _ => throw new NotSupportedException(
            $"The property type '{type.Name}' has no export spelling."),
    };

    private static string Literals(IEnumerable<string> values) =>
        string.Join(", ", values.Select(SourceWriter.Literal));
}
