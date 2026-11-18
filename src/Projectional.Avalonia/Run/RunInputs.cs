using System.Globalization;

using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>The variables a run is given, read from the one line the output panel offers.</summary>
/// <remarks>
/// <para>
/// A name in an expression is a free variable, so one decision table is runnable against many inputs and the values
/// have to come from the user. One line of <c>name=value</c> pairs is the smallest thing that answers that - a grid
/// with typed columns would need a schema, and the document does not declare one.
/// </para>
/// <para>
/// A value is read as an <see cref="int"/>, then a <see cref="bool"/>, then a <c>yyyy-MM-dd</c> date, and otherwise as
/// text. The order is the only one that keeps <c>1500</c> a number and <c>EUR</c> text, and it is why there is no
/// quoting syntax: a user who means the word <c>true</c> cannot say so, which is the honest limit of a line rather
/// than a rule to work around.
/// </para>
/// </remarks>
public static class RunInputs
{
    /// <summary>Reads <paramref name="text"/> as the run's variables.</summary>
    /// <param name="text">The line the user typed, such as <c>amount=1500, currency=EUR</c>.</param>
    /// <param name="variables">The values read, empty when <paramref name="text"/> names nothing.</param>
    /// <param name="error">Why the line could not be read, or <see langword="null"/> when it was.</param>
    /// <returns>Whether the line was read. On <see langword="false"/> <paramref name="variables"/> is empty.</returns>
    /// <remarks>
    /// A malformed line answers no variables at all rather than the pairs before the mistake: a run against half the
    /// inputs the user typed would fail for a reason that is not in the document, which is the one failure a user
    /// cannot diagnose from the editor.
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

            // The blanks around a pair and around its '=' are the line's, not the name's or the value's: a user types
            // them for readability, and a name with a trailing space would be a variable no document can read.
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

    /// <summary>What one written value means: a number, a flag, a date, or the text itself.</summary>
    private static Value ParseValue(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? new IntValue(number)
        : bool.TryParse(text, out var flag) ? new BoolValue(flag)
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? new DateValue(date)
        : new StringValue(text);
}
