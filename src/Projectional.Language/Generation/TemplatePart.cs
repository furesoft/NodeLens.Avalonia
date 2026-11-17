namespace Projectional.Language;

/// <summary>One piece of a text template: something a generator emits for a node.</summary>
/// <remarks>
/// <para>
/// A template is data, not code - a list of parts that a C# author writes and that a declared language lowers to,
/// which is the same bargain the notation and the type rules make. The set of parts is closed and small on purpose:
/// a part either emits text, reads one slot, expands a child, or decides between two templates. Anything that would
/// need a variable or a computation is a generator this framework does not have yet, and pretending otherwise with a
/// half-working part would be worse than the gap.
/// </para>
/// <para>
/// There is no part that reads the model's place, its file or the environment: generation is pure, and the same
/// model therefore produces the same bytes on every machine.
/// </para>
/// </remarks>
public abstract record TemplatePart;
