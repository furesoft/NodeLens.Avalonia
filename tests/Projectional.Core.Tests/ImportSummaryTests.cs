using Projectional.Language;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The sentence a successful import leaves in the status line, and why the warnings are its point.</summary>
/// <remarks>
/// An importer reports what it kept as text as a warning rather than as a refusal, so the host is the only place the
/// user can learn about it. The three cases are the three answers a host can give: nothing to say, one thing to say,
/// and more than one, where the place of the first is the only one that fits in a sentence.
/// </remarks>
public sealed class ImportSummaryTests
{
    private static readonly ImportDiagnostic Warning = new(
        "json.duplicate-name",
        "The object names \"a\" more than once; both members are kept.",
        3,
        14,
        Length: 3,
        Severity: ImportSeverity.Warning);

    [Fact]
    public void AnImportWithNothingToReportSaysWhatItDid()
    {
        Assert.Equal(
            "Imported probe.json as a document of JSON.",
            ImportSummary.Of("probe.json", "JSON", ImportResult.Imported([])));
    }

    [Fact]
    public void AWarningIsReportedWithItsPlaceAndTheImportersOwnWords()
    {
        // The message is the importer's: it knows what it kept, and "some of your file is only text" would tell the
        // user that something happened without telling them what.
        Assert.Equal(
            "Imported probe.json as a document of JSON, with a warning at 3:14: "
                + "The object names \"a\" more than once; both members are kept.",
            ImportSummary.Of("probe.json", "JSON", ImportResult.Imported([], [Warning])));
    }

    [Fact]
    public void SeveralWarningsAreCountedAndTheFirstIsPlaced()
    {
        // A status line holds one sentence, so the rest are counted rather than listed - and counting them is what
        // tells the user to look again rather than assume the one named warning was the only one.
        Assert.Equal(
            "Imported probe.json as a document of JSON, with 2 warnings, the first at 3:14: "
                + "The object names \"a\" more than once; both members are kept.",
            ImportSummary.Of("probe.json", "JSON", ImportResult.Imported([], [Warning, Warning with { Line = 9, Column = 2 }])));
    }

    [Fact]
    public void ARefusedImportIsNoSummary()
    {
        // A refusal has no document, so it is reported where the text's place can be shown; asking this type for a
        // sentence would produce one about a document that was never opened.
        var refused = ImportResult.Refused(Warning with { Severity = ImportSeverity.Error });

        Assert.Throws<ArgumentException>(() => ImportSummary.Of("probe.json", "JSON", refused));
    }
}
