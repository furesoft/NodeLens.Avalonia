namespace Projectional.Core;

/// <summary>
/// A slot holding owned child nodes. Children are deleted with their parent and keep their
/// order, because a projection shows them in that order and document order is content.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Cardinality"/> says how the slot is <em>stored</em> - at most one entry, or an ordered list - and
/// <see cref="Minimum"/> says how many it <em>needs</em>. The two are separate on purpose. "None or one" and
/// "exactly one" are both a single child (<c>0..1</c> and <c>1</c>), and "zero or more" and "one or more" are
/// both a list (<c>0..n</c> and <c>1..n</c>), so folding the minimum into the cardinality would either lose a
/// distinction the author writes down or invent a third storage shape for a validity statement.
/// </para>
/// <para>
/// The minimum is <em>reported</em>, not enforced. A document may be written below it - deleting the last
/// element of a <c>1..n</c> list is an ordinary edit - and the check catalogue says so with the repair that
/// fills the slot (<c>STD010</c> for a single child, <c>STD011</c> for a list). Enforcing it instead would make
/// a model that cannot be brought into an invalid state, which is also a model that cannot be edited back out
/// of one.
/// </para>
/// </remarks>
public sealed record ChildSlot : SlotDeclaration
{
    /// <summary>Declares a child slot.</summary>
    /// <param name="name">The slot's name, unique within its concept.</param>
    /// <param name="childConceptKey">The concept every child has to be or extend.</param>
    /// <param name="cardinality">Whether the slot holds at most one child or an ordered list.</param>
    /// <param name="minimum">
    /// How many children the slot needs, or <see langword="null"/> for the cardinality's own answer - one for a
    /// single child, none for a list - which is what every slot declared before ranges existed meant.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="childConceptKey"/> is not a concept key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The minimum is negative, or a single-child slot is asked to require more than one child.
    /// </exception>
    public ChildSlot(
        string name,
        string childConceptKey,
        Cardinality cardinality = Cardinality.Many,
        int? minimum = null)
        : base(name)
    {
        if (!NameRules.IsValidConceptKey(childConceptKey))
        {
            throw new ArgumentException($"'{childConceptKey}' is not a valid concept key.", nameof(childConceptKey));
        }

        var required = minimum ?? (cardinality == Cardinality.One ? 1 : 0);

        if (required < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                required,
                "A child slot cannot require a negative number of children.");
        }

        if (cardinality == Cardinality.One && required > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                required,
                $"Slot '{name}' holds one child, so it cannot require {required} - declare it as a list for that.");
        }

        ChildConceptKey = childConceptKey;
        Cardinality = cardinality;
        Minimum = required;
    }

    /// <summary>Every child has to be of this concept or extend it.</summary>
    public string ChildConceptKey { get; }

    /// <summary>Whether the slot holds at most one child or an ordered list.</summary>
    public Cardinality Cardinality { get; }

    /// <summary>How many children the slot needs, which a check reports when it holds fewer.</summary>
    public int Minimum { get; }

    /// <summary>
    /// The most children the slot can hold, or <see langword="null"/> when the list is unbounded - the upper half
    /// of the range an author writes as <c>[0..n]</c>, which is the cardinality's answer rather than a declaration.
    /// </summary>
    public int? Maximum => Cardinality == Cardinality.One ? 1 : null;
}

