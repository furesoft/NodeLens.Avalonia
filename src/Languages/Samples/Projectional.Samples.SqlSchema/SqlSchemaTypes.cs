using Projectional.Core;
using Projectional.Language;

namespace Projectional.Samples.SqlSchema;

/// <summary>What a column means as a type: the type its `type` property names.</summary>
/// <remarks>
/// <para>
/// The schema is the first shipped language with a type of its own, and it needs one for a reason a reader of the
/// DDL already accepts: a column is declared <c>name type</c>, so "a value of this column may be used to look up
/// that column" is a question about two declared types rather than about two nodes.
/// </para>
/// <para>
/// Each column type is a <see cref="NamedType"/>, so it has an identity of its own - the property's value names it,
/// and one instance per value is what makes two columns of one type the same type. The expansion is the primitive
/// underneath, which is what a comparison falls back to.
/// </para>
/// <para>
/// <c>decimal</c> expands to <c>float</c>: a decimal holds a fractional part, so the framework's fractional primitive
/// is the honest expansion even though SQL's <c>DECIMAL</c> is exact and a <see cref="double"/> is not. That is a
/// boundary this sample records rather than invents - an exact decimal is a primitive the framework does not have -
/// and it is why the two numeric column types are related by a declared edge instead of by their expansions:
/// <c>int</c> may stand where <c>decimal</c> is required, and a <c>decimal</c> may not stand where an <c>int</c> is,
/// which is what widening means and what a key check needs to know.
/// </para>
/// </remarks>
internal static class SqlSchemaTypes
{
    /// <summary>A whole number.</summary>
    internal static NamedType Int { get; } = new("int", PrimitiveType.Of(PropertyType.Integer));

    /// <summary>Free text.</summary>
    internal static NamedType Text { get; } = new("text", PrimitiveType.Of(PropertyType.String));

    /// <summary>A truth value.</summary>
    internal static NamedType Bool { get; } = new("bool", PrimitiveType.Of(PropertyType.Boolean));

    /// <summary>A calendar date.</summary>
    internal static NamedType Date { get; } = new("date", PrimitiveType.Of(PropertyType.Date));

    /// <summary>A number with a fractional part, held in the framework's fractional primitive.</summary>
    internal static NamedType Decimal { get; } = new("decimal", PrimitiveType.Of(PropertyType.Float));

    /// <summary>The one edge the schema declares: a whole number may stand where a fractional one is required.</summary>
    /// <remarks>
    /// Declared rather than implied, because the two expand to different primitives and an expansion cannot say what
    /// a widening is. The other direction is deliberately absent: a fractional value is not an integer, and a key
    /// between the two columns in that order is a real mistake.
    /// </remarks>
    internal static SubtypeRelation Subtypes { get; } = SubtypeRelation.Primitives.Add(Int, Decimal);

    /// <summary>The registry <see cref="SqlSchemaLanguage.Types"/> answers with.</summary>
    internal static TypeRegistry Registry { get; } =
        TypeRegistry.Empty.Add(SqlConceptKeys.Column, new ColumnType()).WithSubtypes(Subtypes);

    /// <summary>A column is the type its `type` property names, and nothing else.</summary>
    private sealed class ColumnType : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.Node.GetOrNull("type") switch
        {
            "int" => Int,
            "text" => Text,
            "bool" => Bool,
            "date" => Date,
            "decimal" => Decimal,

            // Unreachable: the model refuses a value outside the enumeration, so the property always names one of
            // the five. The branch is here because a type rule answers rather than throws - the alternative is a
            // cast that would fail where a caller least expects it.
            _ => UnknownType.Instance,
        };
    }
}
