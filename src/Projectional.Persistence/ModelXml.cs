using Projectional.Core;

using System.Text;
using System.Xml;

namespace Projectional.Persistence;

/// <summary>
/// The stored form of a model: a model goes in, a document comes out, and the other way round,
/// with every identity, value and reference kept exactly as it was.
/// </summary>
/// <remarks>
/// <para>
/// The format is described in <c>docs/architecture/persistence.md</c>. What matters for callers is
/// the promise: saving a loaded model again produces the same bytes, so a model that nobody edited
/// never shows up in a diff.
/// </para>
/// <para>
/// This type deals in memory only. Reading and writing files — including the atomic replace that
/// keeps a crash from leaving half a model behind — is <see cref="ModelFile"/>.
/// </para>
/// </remarks>
public static class ModelXml
{
    /// <summary>The document a model is stored as, as text.</summary>
    /// <exception cref="InvalidOperationException">The model is in the middle of a transaction.</exception>
    public static string Serialize(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var buffer = new MemoryStream();
        WriteTo(model, buffer);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>Writes a model as UTF-8 without a byte order mark.</summary>
    /// <exception cref="InvalidOperationException">The model is in the middle of a transaction.</exception>
    public static void WriteTo(Model model, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(stream);

        if (model.HasTransaction)
        {
            throw new InvalidOperationException(
                "A model cannot be written while a transaction is open: the tree would be a state that never existed.");
        }

        using var writer = XmlWriter.Create(stream, ModelXmlWriter.CreateSettings());
        ModelXmlWriter.Write(model, writer);
    }

    /// <summary>Builds a model from its document and adds it to <paramref name="project"/>.</summary>
    /// <exception cref="ModelFormatException">The document is not a model this build can read.</exception>
    public static Model Deserialize(Project project, string text)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(text);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return ReadFrom(project, stream);
    }

    /// <summary>Builds a model from its document and adds it to <paramref name="project"/>.</summary>
    /// <exception cref="ModelFormatException">The document is not a model this build can read.</exception>
    public static Model ReadFrom(Project project, Stream stream) => ModelXmlReader.Read(project, stream);
}
