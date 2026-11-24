namespace Projectional.Web;

/// <summary>The workspace's on-disk shape: one index line per document, and one entry per document's text.</summary>
/// <remarks>
/// <para>
/// A line-based format rather than JSON, because the browser sample has no serializer to configure and no
/// trimming surprise to find: three escaped fields separated by a tab, and the document's own XML under its own
/// key. It is read and written by the same two methods, and a line that does not parse is skipped rather than
/// failing the restore - a workspace that refused to open because one index line was corrupt would hide the
/// documents beside it.
/// </para>
/// <para>
/// Fields are escaped with <see cref="Uri.EscapeDataString(string)"/> so a name may contain a tab, a newline or
/// anything else a user typed. The document text is not escaped: it is XML, stored under its own key.
/// </para>
/// </remarks>
public static class DocumentStore
{
    /// <summary>The key the index lives under.</summary>
    public const string IndexKey = "Projectional.Web.Documents";

    /// <summary>The key the active document's id lives under, so a reload opens the tab that was in front.</summary>
    public const string ActiveKey = "Projectional.Web.Active";

    /// <summary>The prefix every document's text is stored under, followed by its id.</summary>
    public const string DocumentPrefix = "Projectional.Web.Document.";

    /// <summary>The key one document's text lives under.</summary>
    /// <param name="id">The document's identity.</param>
    public static string DocumentKey(string id) => DocumentPrefix + id;

    /// <summary>The index as one line per document, or an empty string when there are none.</summary>
    /// <param name="documents">The documents to list, in tab order.</param>
    public static string EncodeIndex(IEnumerable<SavedDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var lines = documents.Select(document =>
            string.Join(
                '\t',
                Uri.EscapeDataString(document.Id),
                Uri.EscapeDataString(document.Name),
                Uri.EscapeDataString(document.Language)));

        return string.Join('\n', lines);
    }

    /// <summary>The documents an index line list names, skipping lines that do not parse.</summary>
    /// <param name="text">What <see cref="EncodeIndex"/> wrote, or <see langword="null"/>.</param>
    public static IReadOnlyList<SavedDocument> DecodeIndex(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        List<SavedDocument> documents = [];

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('\t');

            if (fields.Length != 3)
            {
                continue;
            }

            var id = Uri.UnescapeDataString(fields[0]);
            var name = Uri.UnescapeDataString(fields[1]);
            var language = Uri.UnescapeDataString(fields[2]);

            if (id.Length == 0 || name.Length == 0 || language.Length == 0)
            {
                continue;
            }

            documents.Add(new SavedDocument(id, name, language));
        }

        return documents;
    }
}
