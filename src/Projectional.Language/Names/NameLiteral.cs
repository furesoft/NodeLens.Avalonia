using Projectional.Core;

namespace Projectional.Language;

/// <summary>A fixed word a name always contains, such as the dot between two halves or a closing bracket.</summary>
/// <param name="Text">The word, emitted exactly as written.</param>
public sealed record NameLiteral : NameBody
{
    /// <summary>Creates a part that always answers <paramref name="text"/>.</summary>
    /// <param name="text">The word to answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public NameLiteral(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
    }

    /// <summary>The word this part answers.</summary>
    public string Text { get; }

    private protected override string Evaluate(Node node) => Text;

    public override string ToString() => $"\"{Text}\"";
}
