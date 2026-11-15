using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// Emphasises every node of one concept, whatever the node says.
/// </summary>
/// <remarks>
/// The decoration tests need to know which node an emphasis came from, and a rule of the sample
/// language would tie them to that sample. Answering for a whole concept instead keeps the rule of
/// the test in the test: <c>ConceptEmphasis.On(TestLanguage.Body, TextEmphasis.Strikethrough)</c>
/// says exactly what it means without any model condition behind it.
/// </remarks>
internal sealed class ConceptEmphasis : ICellEmphasis
{
    private readonly string _conceptKey;
    private readonly TextEmphasis _emphasis;

    private ConceptEmphasis(string conceptKey, TextEmphasis emphasis)
    {
        _conceptKey = conceptKey;
        _emphasis = emphasis;
    }

    /// <summary>Emphasises every node of <paramref name="conceptKey"/>.</summary>
    public static ConceptEmphasis On(string conceptKey, TextEmphasis emphasis) => new(conceptKey, emphasis);

    /// <inheritdoc />
    public TextEmphasis EmphasisOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.Key == _conceptKey ? _emphasis : TextEmphasis.None;
    }

    public override string ToString() => $"'{_conceptKey}' as {_emphasis}";
}
