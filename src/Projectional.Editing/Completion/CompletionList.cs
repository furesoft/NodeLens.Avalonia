namespace Projectional.Editing;

/// <summary>
/// The proposals open for one empty slot, together with the filter the user has typed into them.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: every gesture - a keystroke that narrows, a backspace that widens, a move of the
/// selection - answers with a new list, so a stale reference cannot be mutated into disagreeing with
/// the caret. <see cref="Prefix"/> is held here rather than in a cell because an empty slot has no text
/// to filter with, and putting characters into the model to filter a list the user may dismiss would be
/// an edit nobody asked for.
/// </para>
/// <para>
/// The list is view state on the session: never persisted, never its own undo step. Reopening a
/// document shows no list, and dismissing one leaves the model exactly as it was.
/// </para>
/// </remarks>
public sealed class CompletionList
{
    private readonly IReadOnlyList<CompletionItem> _items;

    /// <summary>Opens a list over <paramref name="items"/> with nothing typed yet.</summary>
    /// <param name="items">Every proposal the slot has, in the order the language declares its concepts.</param>
    /// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
    public CompletionList(IReadOnlyList<CompletionItem> items)
        : this(items, string.Empty, 0)
    {
    }

    private CompletionList(IReadOnlyList<CompletionItem> items, string prefix, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("An open list has at least one proposal.", nameof(items));
        }

        _items = items;
        Prefix = prefix;
        SelectedIndex = selectedIndex;
    }

    /// <summary>Every proposal the slot has, in the order the language declares its concepts.</summary>
    /// <remarks>
    /// Declaration order is the language author's answer to "what does the user most likely want here",
    /// which is the same order <c>InsertNodeAction</c> resolves a default from - so the proposal the
    /// Enter key would create without a list is the first one the list offers.
    /// </remarks>
    public IReadOnlyList<CompletionItem> Items => _items;

    /// <summary>The characters typed while the list was open, which filter it.</summary>
    public string Prefix { get; }

    /// <summary>The proposals that match <see cref="Prefix"/>, in the same order as <see cref="Items"/>.</summary>
    /// <remarks>
    /// Matched case-insensitively against the item's label, because the label is what the user is
    /// reading and typing back; an empty prefix matches everything, and a prefix that matches nothing
    /// leaves an empty list rather than closing it - a mistyped character is a backspace away from
    /// being useful, and closing the list would take the proposals with it.
    /// </remarks>
    public IReadOnlyList<CompletionItem> Matches => field ??= Filter();

    /// <summary>How many proposals match the prefix.</summary>
    public int Count => Matches.Count;

    /// <summary>Whether nothing matches the prefix.</summary>
    public bool IsEmpty => Matches.Count == 0;

    /// <summary>Which match is highlighted, or 0 when nothing matches.</summary>
    public int SelectedIndex { get; }

    /// <summary>The highlighted proposal, or <see langword="null"/> when nothing matches.</summary>
    public CompletionItem? Selected =>
        SelectedIndex >= 0 && SelectedIndex < Matches.Count ? Matches[SelectedIndex] : null;

    /// <summary>The list narrowed by <paramref name="text"/>, with the highlight back at the top.</summary>
    /// <remarks>
    /// The highlight returns to the first match on every keystroke, because the previous position was a
    /// position in a longer list and pointing it at the same index would silently select a different
    /// proposal.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public CompletionList Narrow(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length == 0 ? this : new CompletionList(_items, Prefix + text, 0);
    }

    /// <summary>The list widened by dropping the last character of the filter.</summary>
    public CompletionList Widen() => Prefix.Length == 0 ? this : new CompletionList(_items, Prefix[..^1], 0);

    /// <summary>The same list with the highlight moved by <paramref name="delta"/>, wrapping at both ends.</summary>
    /// <remarks>
    /// Wrapping rather than stopping, because a list is short and a user pressing Down is looking for
    /// something: stopping at the end would look like the key did nothing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delta"/> is zero.</exception>
    public CompletionList Move(int delta)
    {
        ArgumentOutOfRangeException.ThrowIfZero(delta);

        var count = Matches.Count;

        if (count == 0)
        {
            return this;
        }

        var index = ((SelectedIndex + delta) % count + count) % count;

        return new CompletionList(_items, Prefix, index);
    }

    public override string ToString() => $"{Count} of {_items.Count} for '{Prefix}'";

    private IReadOnlyList<CompletionItem> Filter()
    {
        if (Prefix.Length == 0)
        {
            return _items;
        }

        List<CompletionItem> matches = [];

        foreach (var item in _items)
        {
            if (item.Label.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(item);
            }
        }

        return matches;
    }
}
