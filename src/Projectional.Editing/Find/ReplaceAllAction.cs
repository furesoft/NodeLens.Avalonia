using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Replaces every occurrence of a query, as one undo step.</summary>
/// <remarks>
/// <para>
/// One transaction, because "replace all" is one decision: a user who dislikes the result presses undo once, not once
/// per occurrence. That is also why it is not a program of <see cref="ReplaceMatchAction"/>s - a program would be
/// correct and would still produce one undo step per row.
/// </para>
/// <para>
/// Each cell is rewritten <em>once</em>, with its replacements applied right to left: offsets counted against the
/// original text stay valid that way, and a cell holding three occurrences costs one property write rather than three.
/// </para>
/// <para>
/// Values the property type refuses are skipped rather than aborting the run, and the count of what happened is part
/// of the answer's description. Aborting would mean a half-replaced document rolled back because of one cell the user
/// cannot see the problem in.
/// </para>
/// </remarks>
public sealed class ReplaceAllAction : EditAction
{
    private readonly FindQuery _query;
    private readonly string _replacement;

    /// <summary>Replaces every occurrence of <paramref name="query"/> with <paramref name="replacement"/>.</summary>
    /// <param name="query">What to look for.</param>
    /// <param name="replacement">What to put in its place; empty deletes the occurrences.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> or <paramref name="replacement"/> is <see langword="null"/>.
    /// </exception>
    public ReplaceAllAction(FindQuery query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(replacement);

        _query = query;
        _replacement = replacement;
    }

    /// <summary>What is looked for.</summary>
    public FindQuery Query => _query;

    /// <summary>What is put in its place.</summary>
    public string Replacement => _replacement;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;
        var matches = Finder.Scan(context.Tree, _query);

        if (matches.Count == 0)
        {
            return EditResult.Unchanged(selection);
        }

        var replaced = 0;
        var refused = 0;

        using var transaction = context.Document.Model.BeginTransaction();

        foreach (var (cell, found) in Cells(matches))
        {
            var text = cell.Text;

            // Right to left, so every offset still counts against the original text.
            foreach (var match in found.OrderByDescending(match => match.Offset))
            {
                var candidate = string.Concat(text[..match.Offset], _replacement, text[(match.Offset + match.Length)..]);

                if (!cell.Type.IsValid(candidate))
                {
                    refused++;
                    continue;
                }

                text = candidate;
                replaced++;
            }

            if (replaced > 0 && !context.SetProperty(cell, text))
            {
                refused += found.Count;
            }
        }

        if (replaced == 0)
        {
            // Nothing was written, so there is nothing to keep: rolling back keeps "replace all" out of the
            // history when it could not do anything.
            transaction.Dispose();

            return EditResult.Rejected(
                selection,
                refused == 0 ? "Nothing to replace." : $"None of the {refused} occurrences accept \"{_replacement}\".");
        }

        transaction.Complete();

        return EditResult.Applied(selection);
    }
    public override string ToString() => $"replace all \"{_query.Text}\" with \"{_replacement}\"";

    /// <summary>The cells a run of matches covers, grouped so each is written once.</summary>
    private static List<(PropertyCell Cell, List<TextMatch> Matches)> Cells(IReadOnlyList<TextMatch> matches)
    {
        List<(PropertyCell Cell, List<TextMatch> Matches)> cells = [];
        var byAddress = new Dictionary<CellAddress, int>();

        foreach (var match in matches)
        {
            if (match.Place.Cell is not PropertyCell cell)
            {
                continue;
            }

            if (!byAddress.TryGetValue(match.Place.Address, out var index))
            {
                index = cells.Count;
                byAddress[match.Place.Address] = index;
                cells.Add((cell, []));
            }

            cells[index].Matches.Add(match);
        }

        return cells;
    }
}