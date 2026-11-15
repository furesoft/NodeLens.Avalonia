namespace Projectional.Language;

/// <summary>
/// Which variant of a concept's notation a reader wants to see.
/// </summary>
/// <remarks>
/// <para>
/// This is view state, not model state. Two panes over one document can carry different choices and
/// still edit the same model, and nothing here is ever written to a file, which is what keeps a
/// document readable in one and the same form by everybody.
/// </para>
/// <para>
/// The question is asked per concept key rather than per node on purpose: a reader who wants rules as
/// a table wants them as a table wherever they appear, and a choice keyed by node would have to name
/// nodes that do not exist yet. Answering <see langword="null"/> means "no opinion", which is also
/// what an empty choice does for every concept.
/// </para>
/// </remarks>
public interface INotationChoice
{
    /// <summary>
    /// The variant wanted for <paramref name="conceptKey"/>, or <see langword="null"/> to take whatever
    /// the registry would pick on its own.
    /// </summary>
    string? VariantOf(string conceptKey);
}
