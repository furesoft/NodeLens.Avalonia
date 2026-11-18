namespace Projectional.Core;

/// <summary>How many children or references a slot may hold, which is the storage shape a slot is declared with.</summary>
/// <remarks>
/// It says at most one entry or an ordered list, not how many an author requires: the lower end of the range an
/// author writes (<c>[1..n]</c>) is <see cref="ChildSlot.Minimum"/>, because "at least one" is a statement a check
/// reports rather than a second way to store a child.
/// </remarks>
public enum Cardinality
{
    /// <summary>The slot holds at most one entry.</summary>
    One,

    /// <summary>The slot holds an ordered list of entries.</summary>
    Many,
}
