namespace Projectional.Core;

/// <summary>What a merge could not decide, which is the reason it answers with conflicts instead of a model.</summary>
/// <remarks>
/// One kind per question the rule table asks - the same property written twice, a reference retargeted twice, a node one
/// side edited and the other deleted - so a surface can group a report by the question rather than only by the node. A
/// conflict is never resolved by a preference of the tool: what it cannot decide, a person decides.
/// </remarks>
public enum MergeConflictKind
{
    /// <summary>The same property holds two different values.</summary>
    PropertyWrittenDifferently,

    /// <summary>The same reference points at two different nodes.</summary>
    ReferenceRetargeted,

    /// <summary>One side deleted a node the other side changed, or changed something below it.</summary>
    DeletedAndEdited,

    /// <summary>Both sides moved the same node, to different places.</summary>
    MovedDifferently,

    /// <summary>Both sides put the same children of one slot in different orders.</summary>
    ReorderedDifferently,

    /// <summary>The same node has two different concepts.</summary>
    ConceptChanged,

    /// <summary>A reference points at a node of this document that the merge removed.</summary>
    DanglingReference,
}
