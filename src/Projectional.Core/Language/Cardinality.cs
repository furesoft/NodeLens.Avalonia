namespace Projectional.Core;

/// <summary>How many children or references a slot may hold.</summary>
public enum Cardinality
{
    /// <summary>The slot holds at most one entry.</summary>
    One,

    /// <summary>The slot holds an ordered list of entries.</summary>
    Many,
}
