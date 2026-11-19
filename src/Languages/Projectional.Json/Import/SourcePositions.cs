using System.Text;

namespace Projectional.Json;

/// <summary>The one translation between the tokenizer's byte offsets and the place a reader sees.</summary>
/// <remarks>
/// <para>
/// <see cref="System.Text.Json"/> reports positions in UTF-8 bytes, and an editor shows a line and a character column.
/// Keeping the mapping in one place means a diagnostic this importer writes and the error the tokenizer raised are
/// measured the same way - which is what lets both be underlined in the same panel.
/// </para>
/// <para>
/// The text is kept as the bytes the reader walked plus the byte offset of every line start, so neither question needs
/// the string decoded again.
/// </para>
/// </remarks>
internal sealed class SourcePositions
{
    private readonly byte[] _bytes;
    private readonly int[] _lineStarts;

    internal SourcePositions(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _bytes = Encoding.UTF8.GetBytes(text);

        var starts = new List<int> { 0 };
        for (var index = 0; index < _bytes.Length; index++)
        {
            if (_bytes[index] == (byte)'\n')
            {
                starts.Add(index + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>The text as the reader expects it.</summary>
    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>The place of <paramref name="byteOffset"/> in the text.</summary>
    /// <param name="byteOffset">An offset the tokenizer reported; one past the end is clamped to it.</param>
    internal SourcePlace PlaceAt(long byteOffset)
    {
        var offset = (int)Math.Clamp(byteOffset, 0, _bytes.Length);

        var line = 0;
        while (line + 1 < _lineStarts.Length && _lineStarts[line + 1] <= offset)
        {
            line++;
        }

        var column = 1 + Encoding.UTF8.GetCharCount(_bytes, _lineStarts[line], offset - _lineStarts[line]);
        return new SourcePlace(line + 1, column);
    }

    /// <summary>The byte offset of a place the tokenizer reported as a zero-based line and byte column.</summary>
    /// <param name="line">The 0-based line.</param>
    /// <param name="byteColumn">The 0-based byte column within that line.</param>
    internal long OffsetOf(int line, int byteColumn) =>
        (line >= 0 && line < _lineStarts.Length ? _lineStarts[line] : _bytes.Length) + byteColumn;
}
