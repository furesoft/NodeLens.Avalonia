using System.Globalization;

namespace Projectional.Language;

/// <summary>The variables a run is given, read from the one line a caller offers.</summary>
/// <remarks>
/// <para>
/// A name in an expression is a free variable, so one decision table is runnable against many inputs and the values
/// have to come from outside. One line of <c>name=value</c> pairs is the smallest thing that answers that - a
/// richer input would need a type per variable and the document does not declare one.
/// </para>
/// <para>
/// A value is read as an <see cref="int"/>, then a <see cref="double"/>, then a <see cref="bool"/>, then a
/// <c>yyyy-MM-dd</c> date, and otherwise as text. The order is the only one that keeps <c>1500</c> a whole number,
/// <c>1.5</c> a fraction and <c>EUR</c> text, and it is why there is no
/// quoting syntax: a user who means the word <c>true</c> cannot say so, which is the honest limit of a line rather
/// than a rule to work around.
/// </para>
/// <para>
/// It lives beside <see cref="Value"/> rather than in the layer that shows the line, because "what does
/// <c>1500</c> mean" is a fact about the language's values and not about a panel. The editor's input box and a
/// declared test's <c>Evaluates</c> assertion read the same pairs through this one parser, so a run in the editor
/// and a run in a test cannot disagree about what a value is.
/// </para>
/// </remarks>
public static class VariableInputs
{
    /// <summary>Reads <paramref name="text"/> as the run's variables.</summary>
    /// <param name="text">The line a caller typed, such as <c>amount=1500, currency=EUR</c>.</param>
    /// <param name="variables">The values read, empty when <paramref name="text"/> names nothing.</param>
    /// <param name="error">Why the line could not be read, or <see langword="null"/> when it could.</param>
    /// <returns>Whether the line could be read. On <see langword="false"/> <paramref name="variables"/> is empty.</returns>
    /// <remarks>
    /// A malformed line answers no variables at all rather than the pairs before the mistake: a run against half the
    /// inputs the caller typed would be a run whose answer nobody can diagnose from the document.
    /// </remarks>
    public static bool TryParse(string? text, out Dictionary<string, Value> variables, out string? error)
    {
        variables = new Dictionary<string, Value>(StringComparer.Ordinal);
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        foreach (var pair in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            // The blanks around a pair and around its '=' are the line's, not the name's or the value's: a caller
            // types them for readability, and a name with a trailing space would be a variable no document reads.
            var name = separator < 0 ? string.Empty : pair[..separator].Trim();
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..].Trim();

            if (name.Length == 0 || value.Length == 0)
            {
                error = $"'{pair}' is not a name=value pair.";

                variables.Clear();

                return false;
            }

            if (!variables.TryAdd(name, ParseValue(value)))
            {
                error = $"'{name}' is given more than once.";

                variables.Clear();

                return false;
            }
        }

        return true;
    }

    /// <summary>What one written value means: a whole number, a fraction, a flag, a date, or the text itself.</summary>
    /// <remarks>
    /// <c>int</c> before <c>float</c>, because a whole number written as <c>1500</c> is a whole number and reading it
    /// as a fraction would make <c>amount=1500</c> a different value than the model's own <c>int</c> slot holds.
    /// </remarks>
    private static Value ParseValue(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? new IntValue(number)
        : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && double.IsFinite(fraction) ? new FloatValue(fraction)
        : bool.TryParse(text, out var flag) ? new BoolValue(flag)
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? new DateValue(date)
        : new StringValue(text);
}
