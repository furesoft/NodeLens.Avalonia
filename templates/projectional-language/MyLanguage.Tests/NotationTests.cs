using Projectional.Core;
using Projectional.Persistence;
using Projectional.Toolkit;
using Xunit;

namespace MyLanguage.Tests;

/// <summary>The two tests every notation is worth: what it draws, and that a stored model comes back unchanged.</summary>
/// <remarks>
/// <para>
/// A notation is data, so it cannot be read in a debugger; it is read as a drawing. <see cref="LanguageSnapshot"/> is
/// how a language looks at its own projection without a window, which is why the toolkit exposes it.
/// </para>
/// <para>
/// The round trip is the second half because the writer is byte-stable by contract: if the second serialize differs,
/// the change is a wire-format change and belongs in a review, not in a bug report.
/// </para>
/// </remarks>
public sealed class NotationTests
{
    [Fact]
    public void TheSampleDrawsAsTheNotationSays()
    {
        var model = MyLanguageSample.NewSampleModel();

        var drawing = LanguageSnapshot.Render(model, MyLanguageNotations.Registry, width: 40);

        Assert.Equal(
            string.Join(
                '\n',
                "board Shopping",
                "  - milk",
                "  - bread"),
            drawing);
    }

    [Fact]
    public void AStoredModelComesBackByteForByte()
    {
        var model = MyLanguageSample.NewSampleModel();

        var first = ModelXml.Serialize(model);
        var reloaded = ModelXml.Deserialize(MyLanguageConcepts.NewProject(), first);

        Assert.Equal(first, ModelXml.Serialize(reloaded));
    }
}