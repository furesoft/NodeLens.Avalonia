using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A fix is a value per finding: an identity, text for the user, and the function that builds the edit.
/// </summary>
/// <remarks>
/// The interesting assertions are about what a menu would show, because that is what the type is for: a
/// fix that names the result, carries a sentence, and can say why it cannot run.
/// </remarks>
public sealed class DiagnosticFixTests
{
    [Fact]
    public void AFixWithoutASentenceExplainsItselfWithTheFinding()
    {
        // The fallback is the reason the fix carries its diagnostic at all: a disabled entry with no
        // description of its own still has a sentence to show, and it is the problem being repaired.
        var diagnostic = Finding("A rule needs a name.");
        var fix = new DiagnosticFix(diagnostic, "rules.name.rename", "Rename", _ => null);

        Assert.Equal("A rule needs a name.", fix.Tip);
        Assert.Null(fix.Description);
    }

    [Fact]
    public void AFixWithASentenceUsesIt()
    {
        var fix = new DiagnosticFix(
            Finding("A rule needs a name."),
            "rules.name.rename",
            "Rename to \"Rule2\"",
            _ => null,
            "Gives the rule the next free name of its concept.");

        Assert.Equal("Gives the rule the next free name of its concept.", fix.Tip);
    }

    [Fact]
    public void AFixAppliesUnlessItSaysOtherwise()
    {
        var fix = new DiagnosticFix(Finding("bad"), "rules.name.rename", "Rename", _ => null);

        Assert.False(fix.IsDisabled);
        Assert.Equal(FixAvailability.Applies, fix.Availability);
    }

    [Fact]
    public void ADisabledFixKeepsItsReason()
    {
        var fix = new DiagnosticFix(
            Finding("bad"),
            "rules.name.rename",
            "Rename",
            _ => null,
            availability: FixAvailability.Disabled("the model is read-only"));

        Assert.True(fix.IsDisabled);
        Assert.Equal("the model is read-only", fix.Availability.Reason);
    }

    [Fact]
    public void AFixIsPlainUnlessItSaysOtherwise()
    {
        // The default has to be the safe direction: a category that forgot to classify itself runs only
        // from a list the user picked it from, never from the single keystroke that asked what is here.
        var fix = new DiagnosticFix(Finding("bad"), "rules.name.rename", "Rename", _ => null);

        Assert.Equal(EditSafety.Plain, fix.Safety);
        Assert.False(fix.IsDestructive);
    }

    [Fact]
    public void ADestructiveFixSaysSo()
    {
        var fix = new DiagnosticFix(
            Finding("bad"),
            "rules.name.delete",
            "Delete the rule",
            _ => null,
            safety: EditSafety.Destructive);

        Assert.Equal(EditSafety.Destructive, fix.Safety);
        Assert.True(fix.IsDestructive);
    }

    [Fact]
    public void TheIdIsNotTheLabel()
    {
        // The id survives translation and a host binding a gesture to it; the label is what the user
        // reads and may name the result of applying the fix.
        var fix = new DiagnosticFix(
            Finding("bad"),
            "rules.name.rename",
            "Rename to \"Rule2\"",
            _ => null);

        Assert.Equal("rules.name.rename", fix.Id);
        Assert.Equal("Rename to \"Rule2\"", fix.Label);
    }

    [Fact]
    public void AFixWithoutAnIdLabelOrCreatorIsRejected()
    {
        var diagnostic = Finding("bad");

        Assert.Throws<ArgumentNullException>(() => new DiagnosticFix(null!, "id", "label", _ => null));
        Assert.Throws<ArgumentNullException>(() => new DiagnosticFix(diagnostic, "id", "label", null!));
        Assert.Throws<ArgumentException>(() => new DiagnosticFix(diagnostic, string.Empty, "label", _ => null));
        Assert.Throws<ArgumentException>(() => new DiagnosticFix(diagnostic, "id", string.Empty, _ => null));
    }

    private static Diagnostic Finding(string message) =>
        new(DiagnosticSeverity.Warning, TestEditor.BareRule().Id, message);
}
