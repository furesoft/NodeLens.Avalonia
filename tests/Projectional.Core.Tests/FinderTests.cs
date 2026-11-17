using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Finding text: what is searched, in which order, and how case is treated.</summary>
/// <remarks>
/// The two rules that matter are pinned here. Only <em>editable</em> text is searched - a keyword is drawn from the
/// notation, so a match in one would be an offer to replace something no edit can touch. And the order is the
/// document's, because "next" is a position in a page, not an index into a sorted list.
/// </remarks>
public sealed class FinderTests
{
    [Fact]
    public void EveryOccurrenceIsFoundInDocumentOrder()
    {
        var (body, steps) = TestEditor.BodyWithSteps("milk", "bread", "milk");

        using var session = TestEditor.Open(body.Parent!);

        var matches = Finder.Find(session, new FindQuery("milk"));

        Assert.Equal(2, matches.Count);
        Assert.Equal(CellAddress.OfSlot(steps[0].Id, "text"), matches[0].Address);
        Assert.Equal(CellAddress.OfSlot(steps[2].Id, "text"), matches[1].Address);
    }

    [Fact]
    public void CaseIsIgnoredUnlessTheQueryAsksForIt()
    {
        var (body, _) = TestEditor.BodyWithSteps("Milk");

        using var session = TestEditor.Open(body.Parent!);

        Assert.Single(Finder.Find(session, new FindQuery("milk")));
        Assert.Empty(Finder.Find(session, new FindQuery("milk", MatchCase: true)));
    }

    [Fact]
    public void OnlyEditableTextIsSearched()
    {
        var (body, _) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        // "rule" is a keyword the notation draws; nothing can replace it, so it is not a match.
        Assert.Empty(Finder.Find(session, new FindQuery("rule")));
    }

    [Fact]
    public void AnEmptyQueryFindsNothing()
    {
        var (body, _) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        Assert.Empty(Finder.Find(session, new FindQuery(string.Empty)));
    }

    [Fact]
    public void TwoOccurrencesInOneCellAreBothFound()
    {
        var (body, steps) = TestEditor.BodyWithSteps("aa");

        using var session = TestEditor.Open(body.Parent!);

        var cell = CellAddress.OfSlot(steps[0].Id, "text");
        var matches = Finder.Find(session, new FindQuery("a")).Where(match => match.Address.Equals(cell)).ToList();

        // Two inside this cell; the rule's placeholder name holds another, which the test is not about.
        Assert.Equal([0, 1], [.. matches.Select(match => match.Offset)]);
    }

    [Fact]
    public void AMatchKnowsWhereItEnds()
    {
        var (body, _) = TestEditor.BodyWithSteps("milk");

        using var session = TestEditor.Open(body.Parent!);

        var match = Assert.Single(Finder.Find(session, new FindQuery("ilk")));

        Assert.Equal(1, match.Offset);
        Assert.Equal(3, match.Length);
        Assert.Equal(4, match.End);
    }
}