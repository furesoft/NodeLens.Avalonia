namespace Projectional.Language;

/// <summary>What a node evaluates to, as a value the interpreter can carry.</summary>
/// <remarks>
/// <para>
/// A value is deliberately not an <see cref="object"/>: a language author writes rules, and a rule that could
/// return anything would put the type checks of a whole language into a cast. The set is open (it is an
/// abstract record, and the subclass list grows with the first language that evaluates something new), but
/// every value that exists is one this framework can compare, print and trace.
/// </para>
/// <para>
/// A value carries no node and no place: it is the <em>answer</em>, and the trace is what remembers where the
/// answer came from. Two values of one kind are equal by content, which is what makes an interpreter test an
/// assertion rather than a walk.
/// </para>
/// </remarks>
public abstract record Value
{
    /// <summary>How the value reads in a trace, a message or a test failure.</summary>
    /// <remarks>
    /// A rendering, not a second identity: nothing compares displays. It exists because a trace is read by the
    /// user, and <c>IntValue { Content = 5 }</c> is the record's printer rather than a value.
    /// </remarks>
    public abstract string Display { get; }
}
