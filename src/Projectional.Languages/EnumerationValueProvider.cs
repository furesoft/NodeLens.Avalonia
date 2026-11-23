using Projectional.Core;
using Projectional.CSharp;
using Projectional.Editing;

namespace Projectional.Languages;

/// <summary>The values a behaviour written in C# has for the enumerations of the language it belongs to.</summary>
/// <remarks>
/// <para>
/// The one slot in the C# base language whose values no declared type can answer. A behaviour names an enumeration of
/// the language *around it* - <c>enum/WeekDays/.Thu</c> - and the C# concept holds that name as text, because the
/// enumeration lives in a <c>structure</c> document loaded in a project of its own. So the list cannot come from the
/// registry the document was opened with; it comes from the language project, which is why the host installs this and
/// not the language package.
/// </para>
/// <para>
/// Two slots, two answers: the enumeration's own key, and then the symbols of whichever enumeration the sibling slot
/// names. A half-written value - <c>enum/</c> with no enumeration yet - offers nothing for the member, which is the
/// truth: there is no set to choose from until there is an enumeration.
/// </para>
/// </remarks>
internal sealed class EnumerationValueProvider(IReadOnlyDictionary<string, IReadOnlyList<string>> enumerations) : IValueProvider
{
    /// <inheritdoc/>
    public IReadOnlyList<string>? ValuesOf(Node owner, string slot)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!owner.Concept.IsKindOf(CSharpConceptKeys.EnumValue))
        {
            return null;
        }

        if (string.Equals(slot, CSharpLanguage.EnumSlot, StringComparison.Ordinal))
        {
            return [.. enumerations.Keys];
        }

        return string.Equals(slot, CSharpLanguage.MemberSlot, StringComparison.Ordinal)
            && owner.GetOrNull(CSharpLanguage.EnumSlot) is { Length: > 0 } key
            && enumerations.TryGetValue(key, out var symbols)
                ? symbols
                : null;
    }
}
