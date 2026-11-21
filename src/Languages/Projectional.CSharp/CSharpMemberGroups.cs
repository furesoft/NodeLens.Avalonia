namespace Projectional.CSharp;

/// <summary>The member kinds the .NET conventions draw without a blank line between them.</summary>
/// <remarks>
/// The convention is "a blank line between members, except inside a block of fields": fields are the one kind that
/// sits together, so a field list reads as one block and everything after it starts a new group. The set lives here
/// rather than in the notation or the generator because both read it, and a drawing that grouped members differently
/// from the generated text would be a lie about the code the generator writes.
/// </remarks>
public static class CSharpMemberGroups
{
    /// <summary>The concepts whose neighbouring members share a group.</summary>
    public static IReadOnlySet<string> Tight { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CSharpConceptKeys.Field,
    };
}
