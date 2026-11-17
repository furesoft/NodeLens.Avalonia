namespace Projectional.Semantics;

/// <summary>
/// The set of things a name can mean at one place in a document.
/// </summary>
/// <remarks>
/// <para>
/// A scope is asked two questions and they are the same question: <see cref="Entries"/> is "what may I
/// reference here", which is what a completion list shows, and <see cref="Resolve"/> is "what does this
/// name mean", which is what a typed name asks. One implementation, two consumers - a second lookup
/// that answered differently would let the list offer something the name cannot mean.
/// </para>
/// <para>
/// Entries are in the order the scope wants them shown, which is also the order a completion list
/// ranks them in. Nothing here is cached: a scope describes a place in a document, and the document
/// changes.
/// </para>
/// </remarks>
public interface IScope
{
    /// <summary>Everything visible here, in the order it should be offered.</summary>
    IReadOnlyList<ScopeEntry> Entries { get; }

    /// <summary>What <paramref name="name"/> means here.</summary>
    ScopeResolution Resolve(string name);
}
