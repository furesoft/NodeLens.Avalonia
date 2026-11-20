using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Samples.SqlSchema.Checks;

/// <summary>A foreign key whose two columns could never be matched up.</summary>
/// <remarks>
/// <para>
/// A key says "a value of this column also appears in that one", so the two columns have to hold the same kind of
/// value. Nothing in the model says so - both slots accept any column of any table - so the rule belongs to the
/// language, and it is the reason the schema has a type of its own at all.
/// </para>
/// <para>
/// A constraint rather than a check: there is no repair to offer, because which of the two columns is wrong is the
/// author's decision and either answer is a schema change.
/// </para>
/// <para>
/// The question is **directed**: the local column's type has to be usable where the target's is required, not the
/// other way round. A whole number may be looked up in a fractional column and a fractional value may not be looked
/// up in an integer one, which is what widening means; asking whether the two are interchangeable would refuse the
/// first and accept the second. It is asked of the target type first - <see cref="Projectional.Language.IType.Accepts"/>
/// is the framework's own answer to "may this value stand here" - and then of the edges the language declared, because
/// a primitive that does not know about an edge would otherwise answer for the language.
/// </para>
/// <para>
/// A key with an unresolved or unwritten side stays silent. The reference layer already reports what it cannot
/// resolve, and a second finding about the same missing column would be one mistake reported twice.
/// </para>
/// </remarks>
public sealed class KeyTypesConstraint : IConstraint
{
    /// <summary>The identifier a host puts on the finding so a user can look it up.</summary>
    public const string Code = "SQL010";

    /// <inheritdoc />
    public void Check(ConstraintContext context)
    {
        if (context.Node.GetReferenceTarget(SqlSchemaLanguage.LocalSlot) is not { } local
            || context.Node.GetReferenceTarget(SqlSchemaLanguage.TargetSlot) is not { } target)
        {
            return;
        }

        var localType = context.TypeOf(local);
        var targetType = context.TypeOf(target);

        if (targetType.Accepts(localType)
            || context.Types.Subtypes.IsSubtype(localType, targetType))
        {
            return;
        }

        context.Error(
            $"the key's column is {localType.Name}, but \"{target.GetOrNull("name")}\" holds {targetType.Name}, so no value of one could be looked up with the other.",
            role: SqlSchemaLanguage.LocalSlot,
            code: Code);
    }
}
