using Projectional.Language;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The import contract: one transaction, one undo step, and nothing left behind on any failure.</summary>
/// <remarks>
/// These tests use <see cref="ProbeImporter"/> rather than a real language on purpose. They are the rules every
/// importer has to keep (docs/architecture/import.md), and a rule that is only proven through one format's grammar
/// stops being proven as soon as that grammar grows a construct the test did not think of.
/// </remarks>
public sealed class TextImportTests
{
    [Fact]
    public void ACommittedImportIsOneUndoStepAndOneChangeEvent()
    {
        var model = TestLanguage.NewModel();
        using var history = new UndoHistory(model);
        var changes = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => changes.Add(e);

        var result = new ProbeImporter(nodes: 3).Import(model, "rule rule rule");

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Roots.Count);
        Assert.Equal(1, history.UndoDepth);
        Assert.Single(changes);

        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void ARefusedImportLeavesTheModelExactlyAsItWas()
    {
        var model = TestLanguage.NewModel();
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        var result = new ProbeImporter(nodes: 3, refusal: new ImportDiagnostic("probe.bad", "Nothing here.", 1, 1))
            .Import(model, "rule rule rule");

        Assert.False(result.Succeeded);
        Assert.Empty(result.Roots);
        Assert.Empty(model.Roots);
        Assert.Equal(version, model.Version);
        Assert.Equal(before, ModelXml.Serialize(model));
    }

    [Fact]
    public void AnImportThatThrowsHalfWayLeavesTheModelExactlyAsItWas()
    {
        // The transaction is the whole failure path: an importer that dies on the third node needs no cleanup code,
        // and a model that kept two of three roots would be a document nobody wrote.
        var model = TestLanguage.NewModel();
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        Assert.Throws<InvalidOperationException>(
            () => new ProbeImporter(nodes: 3, throws: true).Import(model, "rule rule rule"));

        Assert.Empty(model.Roots);
        Assert.Equal(version, model.Version);
        Assert.Equal(before, ModelXml.Serialize(model));
    }

    [Fact]
    public void ARefusalCarriesItsReasonAndNoRoots()
    {
        var refusal = new ImportDiagnostic("probe.bad", "Nothing here.", 3, 14, Length: 5);

        var result = ImportResult.Refused(refusal);

        Assert.False(result.Succeeded);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Roots);
        Assert.Same(refusal, Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void AWarningTravelsBesideASuccessfulImport()
    {
        // "It worked, and this construct is only raw text" is a state a host has to report without calling the
        // import a failure, which is why the severity is on the diagnostic rather than on the result.
        var model = TestLanguage.NewModel();
        var imported = new ProbeImporter(nodes: 1).Import(model, "rule");
        var warning = new ImportDiagnostic("probe.raw", "Kept as text.", 1, 1, Severity: ImportSeverity.Warning);

        var result = ImportResult.Imported(imported.Roots, [warning]);

        Assert.True(result.Succeeded);
        Assert.True(result.HasWarnings);
        Assert.Same(imported.Roots[0], Assert.Single(result.Roots));
    }
}
