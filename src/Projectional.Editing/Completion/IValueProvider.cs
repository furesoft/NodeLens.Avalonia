using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>What an open list offers for a property slot that no declared type can answer.</summary>
/// <remarks>
/// <para>
/// A property slot is answered from its <see cref="PropertyType"/> - an enumeration property offers its symbols, and
/// that is the whole of what the model can say. Some slots mean something the declaring concept cannot express: a
/// behaviour written in C# names an enumeration of the language *around it*, so the values live in another layer
/// entirely and no <see cref="PropertyType"/> could hold them.
/// </para>
/// <para>
/// The host installs this, not the language, for the same reason it installs
/// <see cref="IScopeProvider"/>: a language package cannot reference the editing layer, and the answer needs the
/// language project the host has open rather than the registry the document was loaded with.
/// </para>
/// </remarks>
public interface IValueProvider
{
    /// <summary>What <paramref name="slot"/> of <paramref name="owner"/> may hold, or <see langword="null"/> for "ask the type".</summary>
    /// <param name="owner">The node the property belongs to.</param>
    /// <param name="slot">The property slot.</param>
    /// <returns>
    /// The values, or <see langword="null"/> when this provider says nothing about that slot - which is not the same as
    /// an empty list: an empty list closes the list, <see langword="null"/> lets the declared type answer.
    /// </returns>
    IReadOnlyList<string>? ValuesOf(Node owner, string slot);
}
