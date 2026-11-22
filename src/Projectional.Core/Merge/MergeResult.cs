namespace Projectional.Core;

/// <summary>What a three-way merge produced: one model, or the conflicts that stopped it.</summary>
/// <remarks>
/// <para>
/// Never both. A model that is written beside the conflicts it has would be a document nobody can trust - it looks
/// merged, and the report is somewhere else - which is why the two outcomes are a union rather than a pair of nullable
/// members a caller has to combine. <see cref="Succeeded"/> is the question a command asks to choose its exit code.
/// </para>
/// <para>
/// The merged model belongs to a project of its own, made beside the three the merge read, so nothing a caller did with
/// the inputs can be observed in the result and the result can be saved without touching a file the user still has open.
/// </para>
/// </remarks>
public sealed class MergeResult
{
    private MergeResult(Model? model, IReadOnlyList<MergeConflict> conflicts)
    {
        Model = model;
        Conflicts = conflicts;
    }

    /// <summary>The merged model, or <see langword="null"/> when there were conflicts.</summary>
    public Model? Model { get; }

    /// <summary>Everything the merge could not decide, in the order the questions were asked.</summary>
    public IReadOnlyList<MergeConflict> Conflicts { get; }

    /// <summary>Whether the merge produced a model.</summary>
    public bool Succeeded => Model is not null;

    /// <summary>A merge that came out whole.</summary>
    /// <param name="model">The merged model.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public static MergeResult Merged(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new MergeResult(model, []);
    }

    /// <summary>A merge that stopped, with everything it could not decide.</summary>
    /// <param name="conflicts">The conflicts, which must not be empty - a result with neither a model nor a conflict is a bug.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="conflicts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="conflicts"/> is empty.</exception>
    public static MergeResult Conflicted(IReadOnlyList<MergeConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);

        if (conflicts.Count == 0)
        {
            throw new ArgumentException(
                "A merge that produced no model has to say what stopped it.",
                nameof(conflicts));
        }

        return new MergeResult(null, conflicts);
    }

    public override string ToString() =>
        Model is { } model ? $"merged {model.Name}" : $"{Conflicts.Count} conflict(s)";
}
