using Projectional.Language;

namespace Projectional.Editing;

/// <summary>
/// The representations one concept of an open document can be drawn in, with the one currently asked
/// for.
/// </summary>
/// <remarks>
/// A host needs exactly this to offer the choice: which concepts of <em>this</em> document declare more
/// than one notation, and which of them the reader has picked. The variants of the super concepts are
/// included, because a language that draws its abstract base as a table draws its refinements as a
/// table too - a menu that only listed the concept's own notations would hide a choice the projection
/// engine honours.
/// </remarks>
public sealed class ConceptVariants
{
    internal ConceptVariants(string conceptKey, IReadOnlyList<string> variants, string selected)
    {
        ConceptKey = conceptKey;
        Variants = variants;
        Selected = selected;
    }

    /// <summary>The concept whose representation is being chosen.</summary>
    public string ConceptKey { get; }

    /// <summary>The variants that apply to it, the concept's own first, in declaration order.</summary>
    public IReadOnlyList<string> Variants { get; }

    /// <summary>
    /// The variant the reader asked for, which is <see cref="Notation.DefaultVariant"/> while nobody has
    /// asked for one.
    /// </summary>
    /// <remarks>
    /// It is what a menu shows as checked rather than what a re-projection would resolve to, because the
    /// two differ when a concept declares no default at all: then the first declared variant is drawn
    /// while the reader has still expressed no opinion, and pretending otherwise would make the menu
    /// claim a choice that was never made.
    /// </remarks>
    public string Selected { get; }

    public override string ToString() => $"{ConceptKey} as {Selected}";
}
