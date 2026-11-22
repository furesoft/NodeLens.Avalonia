namespace Projectional.Language;

/// <summary>Several values, each under the name a record gives it.</summary>
/// <remarks>
/// <para>
/// A record is the value a node with several properties answers with - a quantity is an amount and a unit, not one number -
/// and it exists because the alternative was a body per property and a rule that could not hand the pair on as one answer.
/// </para>
/// <para>
/// Equality is by content and by <em>name</em>, never by order: a record is a mapping, so two records that name the same
/// values are one value even when a rule listed them the other way round. The rule itself lives in <see cref="RecordFields"/>,
/// because this record's generated equality compares its fields and a bare list of pairs would compare by identity.
/// </para>
/// </remarks>
public sealed record RecordValue : Value
{
    private readonly RecordFields _fields;

    /// <summary>A record of <paramref name="fields"/>, in the order they were written.</summary>
    /// <param name="fields">The named values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> is <see langword="null"/>.</exception>
    public RecordValue(IEnumerable<KeyValuePair<string, Value>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        _fields = new RecordFields(fields);
    }

    /// <summary>The named values, in the order the rule wrote them.</summary>
    public IReadOnlyList<KeyValuePair<string, Value>> Fields => _fields.Items;

    /// <summary>The value under <paramref name="name"/>, or <see langword="null"/> while the record holds no such field.</summary>
    /// <param name="name">The field to read.</param>
    public Value? Field(string name) => _fields.Value(name);

    /// <inheritdoc />
    public override string Display =>
        "{" + string.Join(", ", _fields.Items.Select(named => $"{named.Key} = {named.Value.Display}")) + "}";
}
