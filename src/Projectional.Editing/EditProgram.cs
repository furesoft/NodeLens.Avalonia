using System.Collections.ObjectModel;

namespace Projectional.Editing;

/// <summary>Several edits that are applied as one: one undo step, and nothing at all if one of them fails.</summary>
/// <remarks>
/// <para>
/// A repair is usually one property write, but not always - renaming a node and then updating what it
/// said, or filling a slot and putting the caret in what was created. A program is how such a repair
/// stays one action from the caller's point of view: the user sees one undo step, and a step that cannot
/// be carried out leaves the model exactly as it was.
/// </para>
/// <para>
/// The steps are applied in order against a model that has already changed, so the tree the next step
/// addresses is not the one the previous step saw. That is why a program is not applied like an ordinary
/// action: only <see cref="EditorSession"/> can run one, because only the session rebuilds the tree and the
/// layout between steps. <see cref="Apply"/> therefore throws rather than half-working, and the session
/// recognises a program before it calls it.
/// </para>
/// <para>
/// Programs do not nest. A program inside a program is a modelling mistake - the sequence is flat by
/// construction - and flattening it silently would hide the mistake instead of reporting it.
/// </para>
/// </remarks>
public sealed class EditProgram : EditAction
{
    private readonly ReadOnlyCollection<EditAction> _steps;

    private EditProgram(List<EditAction> steps) => _steps = steps.AsReadOnly();

    /// <summary>A program that applies <paramref name="steps"/> in order.</summary>
    /// <param name="steps">The edits to apply; none of them may be a program itself.</param>
    /// <exception cref="ArgumentNullException">One of <paramref name="steps"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">One of <paramref name="steps"/> is a program.</exception>
    public static EditProgram Of(params ReadOnlySpan<EditAction> steps)
    {
        var all = new List<EditAction>(steps.Length);

        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step, nameof(steps));

            if (step is EditProgram)
            {
                throw new ArgumentException(
                    "A program cannot contain another program; list its steps instead.",
                    nameof(steps));
            }

            all.Add(step);
        }

        return new EditProgram(all);
    }

    /// <summary>The edits, in the order they are applied.</summary>
    public IReadOnlyList<EditAction> Steps => _steps;

    /// <summary>Always throws: a program runs only through <see cref="EditorSession"/>.</summary>
    /// <param name="context">Unused.</param>
    /// <remarks>
    /// Each step after the first addresses a tree the previous step has invalidated, so running a program
    /// against one context would apply later steps to stale addresses. The session is the type that can
    /// rebuild between steps, and it does so before calling the step.
    /// </remarks>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException(
            "A program is applied by the session, which rebuilds the tree between its steps.");

    public override string ToString() => $"EditProgram({string.Join(", ", _steps)})";
}
