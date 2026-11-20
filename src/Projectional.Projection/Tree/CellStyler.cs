using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// The built-in classification: keywords and punctuation from the notation, value kinds from the
/// property type, and the three states a slot can be in.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is read off the cell, never off the model. That keeps a frame consistent with the
/// layout it was built from: a style that reached back into the node could reflect a value the layout
/// never measured.
/// </para>
/// <para>
/// It is not the last word. A language whose operators are enumeration symbols gets keyword styling by
/// default, which is usually what a reader expects; a host that disagrees installs its own
/// <see cref="ICellStyler"/>.
/// </para>
/// </remarks>
public sealed class CellStyler : ICellStyler
{
    private CellStyler()
    {
    }

    /// <summary>The shared instance, since the classification holds no state.</summary>
    public static CellStyler Default { get; } = new();

    /// <inheritdoc />
    public TextStyleKind StyleOf(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        return cell switch
        {
            ConstantCell constant => OfConstant(constant.Kind),
            PropertyCell property => OfValue(property),
            ReferenceCell reference => OfReference(reference.State),
            ChildCell { IsEmpty: true } => TextStyleKind.Placeholder,
            CollectionCell { IsEmpty: true } => TextStyleKind.Placeholder,

            // The affordance row is a statement that the slot is empty, which is what the placeholder
            // kind means - the same reason an empty child slot is styled that way.
            AddRowCell => TextStyleKind.Placeholder,
            MissingNotationCell => TextStyleKind.Missing,

            // A summary is not a value, it is a statement that something is missing from view - which is
            // exactly what the placeholder kind means, and every palette already has a brush for it.
            CollapsedCell => TextStyleKind.Placeholder,
            _ => TextStyleKind.Default,
        };
    }

    private static TextStyleKind OfConstant(ConstantKind kind) => kind switch
    {
        ConstantKind.Keyword => TextStyleKind.Keyword,
        ConstantKind.Punctuation => TextStyleKind.Punctuation,
        _ => TextStyleKind.Default,
    };

    private static TextStyleKind OfReference(ReferenceState state) => state switch
    {
        ReferenceState.Resolved => TextStyleKind.Reference,
        ReferenceState.Unresolved => TextStyleKind.Unresolved,
        _ => TextStyleKind.Placeholder,
    };

    /// <summary>
    /// Reads the property type by its canonical name, because the concrete types behind
    /// <see cref="Projectional.Core.PropertyType"/> are internal and the name is the only public
    /// discriminator.
    /// </summary>
    /// <remarks>
    /// A cell that shows a placeholder instead of a value is not a value of its type: it is a statement that
    /// something belongs there, which is what the placeholder kind means - the same reading an empty child slot
    /// gets.
    /// </remarks>
    private static TextStyleKind OfValue(PropertyCell cell) =>
        cell.Text.Length == 0 && cell.EmptyText.Length > 0
            ? TextStyleKind.Placeholder
            : cell.Type.Name switch
            {
                "int" or "float" or "date" => TextStyleKind.Number,
                "string" => TextStyleKind.Text,
                // A bool and every enumeration hold one of a closed set of words the language chose.
                _ => TextStyleKind.Keyword,
            };
}
