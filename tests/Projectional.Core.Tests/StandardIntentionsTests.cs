using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Intentions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The entries the framework ships for every language: rename, wrap in something, reset to the default.
/// </summary>
/// <remarks>
/// <para>
/// What these pin is that the catalogue is derived from the <em>shape</em> of the caret's place rather than from
/// knowledge of a language: a node with a name slot, a property holding something other than its declared default,
/// and a node some concept could surround. The wrap search is the one worth the most attention, because it is the
/// entry the catalogue exists for - "surround with <c>if</c>" is not a refactoring here, it is what the language's
/// own declarations answer.
/// </para>
/// <para>
/// The two absence rules are pinned as well: an entry that would change nothing is not offered (a name already
/// selected, a property already holding its default), which is what keeps a bulb from lighting for a no-op.
/// </para>
/// </remarks>
public sealed class StandardIntentionsTests
{
    [Fact]
    public void RenameIsOfferedOnANodeWithAName()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        Assert.Contains(StandardIntentions.RenameId, Ids(session));

        var result = session.Apply(Offer(session, StandardIntentions.RenameId));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);
        Assert.True(session.Selection.HasText);
        Assert.Equal(rule.GetOrNull("name")!.Length, session.Selection.End);
    }

    [Fact]
    public void RenameIsNotOfferedOnANodeWithoutAName()
    {
        var step = TestEditor.BareStep("walk");
        using var session = Session(step);

        Assert.DoesNotContain(StandardIntentions.RenameId, Ids(session));
    }

    [Fact]
    public void RenameIsNotOfferedOnANameThatIsAlreadySelected()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));
        session.Apply(new SelectValueAction());

        Assert.DoesNotContain(StandardIntentions.RenameId, Ids(session));
    }

    [Fact]
    public void ResetWritesTheDeclaredDefaultOverWhatIsThere()
    {
        var rule = TestEditor.BareRule();

        rule.Set("name", "Approved");

        using var session = Session(rule);
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        var offer = Offer(session, StandardIntentions.ResetId);

        Assert.Equal("Reset to \"unnamed\"", offer.Label);

        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void ResetIsNotOfferedOnAPropertyHoldingItsDefault()
    {
        var rule = TestEditor.BareRule();
        using var session = Session(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        Assert.DoesNotContain(StandardIntentions.ResetId, Ids(session));
    }

    [Fact]
    public void EveryConceptThatCouldSurroundTheNodeOffersAWrap()
    {
        var (session, _, condition) = Document();

        using (session)
        {
            Caret(session, condition);

            var wrap = Wraps(session);

            // The expressions language's own groupings, found from the declarations and not from a list.
            Assert.Contains(StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Parenthesized, wrap);
            Assert.Contains(StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Unary, wrap);

            // One id per concept: a list keyed by id would otherwise hold a single wrap entry.
            Assert.Equal(wrap.Count, wrap.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void AWrapIsNotOfferedForARoot()
    {
        // A wrapper has to take the node's place, and a root is not in one.
        var model = PayrollSample.NewSampleModel();
        using var session = Session(model.Roots[0]);

        Assert.Empty(Wraps(session));
    }

    [Fact]
    public void ApplyingAWrapEntrySurroundsTheNode()
    {
        var (session, rule, condition) = Document();

        using (session)
        {
            Caret(session, condition);

            Assert.Equal(
                EditOutcome.Applied,
                session.Apply(Offer(session, StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Parenthesized))
                    .Outcome);

            var wrapper = Assert.Single(rule.GetChildren("condition"));

            Assert.Equal(ExpressionsConceptKeys.Parenthesized, wrapper.Concept.Key);
            Assert.Equal(condition.Id, wrapper.GetChild("inner")!.Id);

            Assert.True(session.Undo());
            Assert.Equal(condition.Id, Assert.Single(rule.GetChildren("condition")).Id);
        }
    }

    [Fact]
    public void WrapSelectionIsOfferedForASelectionOfSeveralNodes()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second");
        using var session = GroupSession(group);

        GroupEditor.SelectRun(session, items[0], items[1]);

        var offer = Offer(session, StandardIntentions.WrapSelectionPrefix + GroupLanguage.Group);

        Assert.Equal("Wrap selection in \"Group\"", offer.Label);
        Assert.Equal(IntentionAvailability.Applies, offer.Availability);
    }

    [Fact]
    public void WrapSelectionIsAbsentForOneNode()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second");
        using var session = GroupSession(group);

        GroupEditor.SelectRun(session, items[0], items[0]);

        Assert.DoesNotContain(
            StandardIntentions.WrapSelectionPrefix + GroupLanguage.Group,
            Ids(session));

        // One node is the per-node entry's business, and it is a different edit: it wraps what the caret is
        // in, while this one would wrap what the user selected.
        Assert.Contains(StandardIntentions.WrapPrefix + GroupLanguage.Group, Ids(session));
    }

    [Fact]
    public void AcceptingTheWrapSelectionEntrySurroundsTheRunInOneStep()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second");
        using var session = GroupSession(group);

        GroupEditor.SelectRun(session, items[0], items[1]);

        var result = session.Apply(Offer(session, StandardIntentions.WrapSelectionPrefix + GroupLanguage.Group));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var wrapper = Assert.Single(group.GetChildren("items"));

        Assert.Equal(GroupLanguage.Group, wrapper.Concept.Key);
        Assert.Equal([items[0], items[1]], wrapper.GetChildren("items"));

        Assert.True(session.Undo());
        Assert.Equal(items, group.GetChildren("items"));
    }

    [Fact]
    public void DuplicateIsOfferedOnAChildOfAListAndCopiesItInPlace()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        Assert.Contains(StandardIntentions.DuplicateId, Ids(session));

        Assert.Equal(
            EditOutcome.Applied,
            session.Apply(Offer(session, StandardIntentions.DuplicateId)).Outcome);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal("a", after[1].GetOrNull("text"));
        Assert.NotEqual(steps[0].Id, after[1].Id);
    }

    [Fact]
    public void DuplicateIsNotOfferedOnARoot()
    {
        // A root has no place, so it has no next sibling to be copied to.
        using var session = Session(TestEditor.BareRule());

        Assert.DoesNotContain(StandardIntentions.DuplicateId, Ids(session));
    }

    [Fact]
    public void DuplicateIsNotOfferedInASingleValuedSlot()
    {
        var outer = TestEditor.BareRule();
        var inner = outer.Model.NewChild(outer, "extends", TestLanguage.Rule);

        using var session = Session(outer);
        Caret(session, inner);

        Assert.DoesNotContain(StandardIntentions.DuplicateId, Ids(session));
    }

    [Fact]
    public void TheStructuralEntriesAreOfferedOnAChildOfAList()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b", "c");

        using var session = Session(body.Parent!);
        Caret(session, steps[1]);

        var ids = Ids(session);

        Assert.Contains(StandardIntentions.DuplicateId, ids);
        Assert.Contains(StandardIntentions.DeleteId, ids);
        Assert.Contains(StandardIntentions.MoveUpId, ids);
        Assert.Contains(StandardIntentions.MoveDownId, ids);
        Assert.Contains(StandardIntentions.MoveToFrontId, ids);
        Assert.Contains(StandardIntentions.MoveToBackId, ids);
        Assert.Contains(StandardIntentions.InsertBeforeId, ids);
        Assert.Contains(StandardIntentions.InsertAfterId, ids);
    }

    [Fact]
    public void TheEndsOfAListDoNotOfferTheMovesThatWouldChangeNothing()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = Session(body.Parent!);

        Caret(session, steps[0]);

        var first = Ids(session);

        Assert.DoesNotContain(StandardIntentions.MoveUpId, first);
        Assert.DoesNotContain(StandardIntentions.MoveToFrontId, first);
        Assert.Contains(StandardIntentions.MoveDownId, first);
        Assert.Contains(StandardIntentions.MoveToBackId, first);

        Caret(session, steps[1]);

        var last = Ids(session);

        Assert.Contains(StandardIntentions.MoveUpId, last);
        Assert.DoesNotContain(StandardIntentions.MoveDownId, last);
        Assert.DoesNotContain(StandardIntentions.MoveToBackId, last);
    }

    [Fact]
    public void DeleteIsOfferedAsADestructiveEntry()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        var offer = Offer(session, StandardIntentions.DeleteId);

        Assert.Equal(EditSafety.Destructive, offer.Safety);

        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Equal("b", Assert.Single(body.GetChildren("steps")).GetOrNull("text"));
    }

    [Fact]
    public void InsertBeforePutsAnEmptySiblingInFront()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = Session(body.Parent!);
        Caret(session, steps[1]);

        Assert.Equal(
            EditOutcome.Applied,
            session.Apply(Offer(session, StandardIntentions.InsertBeforeId)).Outcome);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal("a", after[0].GetOrNull("text"));
        Assert.True(string.IsNullOrEmpty(after[1].GetOrNull("text")));
        Assert.Equal("b", after[2].GetOrNull("text"));

        // A new node is something to name, so the caret lands inside it - which cell the notation draws first is
        // the notation's business, the node is the answer.
        Assert.Equal(after[1].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void MoveToFrontReordersThroughTheEntry()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b", "c");

        using var session = Session(body.Parent!);
        Caret(session, steps[2]);

        session.Apply(Offer(session, StandardIntentions.MoveToFrontId));

        Assert.Equal(["c", "a", "b"], [.. body.GetChildren("steps").Select(step => step.GetOrNull("text"))]);
    }

    [Fact]
    public void EveryConceptThatCouldStandThereOffersAReplace()
    {
        var (session, _, condition) = Document();

        using (session)
        {
            Caret(session, condition.GetChild("left")!);

            var replace = Replaces(session);

            // The slot is declared over `Expression`, so everything else that is an expression stands there.
            Assert.Contains(StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.IntLiteral, replace);
            Assert.Contains(StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.Parenthesized, replace);

            // The concept the node already is, and the abstract one the slot is declared over, are not offered.
            Assert.DoesNotContain(StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.Name, replace);
            Assert.DoesNotContain(StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.Expression, replace);

            Assert.Equal(replace.Count, replace.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void AReplaceIsNotOfferedOnARoot()
    {
        // The document *is* a root, so replacing it would take the node the editor is drawn from.
        using var session = Session(TestEditor.BareRule());

        Assert.Empty(Replaces(session));
    }

    [Fact]
    public void AReplaceIsNotOfferedWhereOnlyItsOwnConceptCouldStand()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        // A step stands in a slot declared over steps: swapping it for another step would only empty it.
        Assert.Empty(Replaces(session));
    }

    [Fact]
    public void ApplyingAReplaceEntrySwapsTheNodeAndCanBeUndone()
    {
        var (session, _, condition) = Document();

        using (session)
        {
            Caret(session, condition.GetChild("left")!);

            Assert.Equal(
                EditOutcome.Applied,
                session.Apply(Offer(session, StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.IntLiteral))
                    .Outcome);

            Assert.Equal(ExpressionsConceptKeys.IntLiteral, condition.GetChild("left")!.Concept.Key);

            Assert.True(session.Undo());

            var restored = condition.GetChild("left")!;

            Assert.Equal(ExpressionsConceptKeys.Name, restored.Concept.Key);
            Assert.Equal("amount", restored.GetOrNull("name"));
        }
    }

    [Fact]
    public void AReplaceEntryIsMarkedDestructive()
    {
        var (session, _, condition) = Document();

        using (session)
        {
            Caret(session, condition.GetChild("left")!);

            var offer = Offer(session, StandardIntentions.ReplacePrefix + ExpressionsConceptKeys.IntLiteral);

            Assert.Equal(EditSafety.Destructive, offer.Safety);
        }
    }

    [Fact]
    public void SortIsOfferedOnAListThatIsOutOfOrder()
    {
        var (session, ruleset, rules) = Ruleset("gamma", "alpha");

        using (session)
        {
            Caret(session, rules[0]);

            Assert.Contains(StandardIntentions.SortByNameId, Ids(session));

            Assert.Equal(
                EditOutcome.Applied,
                session.Apply(Offer(session, StandardIntentions.SortByNameId)).Outcome);

            Assert.Equal(["alpha", "gamma"], [.. ruleset.GetChildren(RulesLanguage.RulesSlot)
                .Select(rule => rule.GetOrNull("name"))]);
        }
    }

    [Fact]
    public void SortIsNotOfferedOnAListThatIsAlreadyInOrder()
    {
        var (session, _, rules) = Ruleset("alpha", "beta");

        using (session)
        {
            Caret(session, rules[0]);

            Assert.DoesNotContain(StandardIntentions.SortByNameId, Ids(session));
        }
    }

    [Fact]
    public void SortIsNotOfferedOnAListWhoseElementsHaveNoName()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a", "b");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        Assert.DoesNotContain(StandardIntentions.SortByNameId, Ids(session));
    }

    [Fact]
    public void SortIsNotOfferedOnARoot()
    {
        // A root is in no list, so there is nothing for it to be an element of.
        using var session = Session(TestEditor.BareRule());

        Assert.DoesNotContain(StandardIntentions.SortByNameId, Ids(session));
    }

    /// <summary>Opens <paramref name="document"/> with the standard catalogue registered.</summary>
    private static EditorSession Session(Node document)
    {
        var session = new EditorSession(document, TestNotations.Registry);

        session.AddIntentions(StandardIntentions.Provider);

        return session;
    }

    /// <summary>A group document opened with the standard catalogue registered.</summary>
    private static EditorSession GroupSession(Node document)
    {
        var session = GroupEditor.Open(document);

        session.AddIntentions(StandardIntentions.Provider);

        return session;
    }

    /// <summary>The payroll sample's condition, opened with the standard catalogue registered.</summary>
    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(model.Concepts));
        session.AddIntentions(StandardIntentions.Provider);

        return (session, rule, rule.GetChild("condition")!);
    }

    [Fact]
    public void BothSelectionEntriesAreOfferedOnAChildOfAList()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        var ids = Ids(session);

        Assert.Contains(StandardIntentions.SelectNodeId, ids);
        Assert.Contains(StandardIntentions.SelectParentId, ids);
    }

    [Fact]
    public void SelectParentSelectsTheNodeThatHoldsThisOne()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = Session(body.Parent!);
        Caret(session, steps[0]);

        Assert.Equal(
            EditOutcome.Unchanged,
            session.Apply(Offer(session, StandardIntentions.SelectParentId)).Outcome);

        var entry = session.Selection;

        Assert.False(entry.IsSingleCell);

        // What the entry did is what the body's own action does - which is the point of the row: it names the parent,
        // not the node the caret happens to be in.
        session.Apply(new SelectNodeAction(body.Id));

        Assert.Equal(entry.Anchor.Address, session.Selection.Anchor.Address);
        Assert.Equal(entry.Anchor.Offset, session.Selection.Anchor.Offset);
        Assert.Equal(entry.Caret.Address, session.Selection.Caret.Address);
        Assert.Equal(entry.Caret.Offset, session.Selection.Caret.Offset);
    }

    [Fact]
    public void SelectParentIsNotOfferedOnARoot()
    {
        // A root is in nothing, so there is no node that holds it - but the root itself can still be selected.
        using var session = Session(TestEditor.BareRule());

        Assert.DoesNotContain(StandardIntentions.SelectParentId, Ids(session));
        Assert.Contains(StandardIntentions.SelectNodeId, Ids(session));
    }

    [Fact]
    public void TheProviderSetTheAppRegistersListsEachOperationOnce()
    {
        // Exactly what the workbench does at open: the catalogue as the host's, then the language's package. The
        // expression package used to ship its own "wrap in parentheses" beside the catalogue's derived wrap, and one
        // operation appearing twice is what this pins.
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        using var session = new EditorSession(rule, RulesLanguage.CreateNotations(model.Concepts));

        session.AddIntentions(StandardIntentions.Provider);
        session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);

        Caret(session, rule.GetChild("condition")!);

        var ids = Ids(session);

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        Assert.Single(
            ids,
            id => string.Equals(
                id,
                StandardIntentions.WrapPrefix + ExpressionsConceptKeys.Parenthesized,
                StringComparison.Ordinal));
    }

    /// <summary>A ruleset holding one named rule per entry, opened with the standard catalogue registered.</summary>
    private static (EditorSession Session, Node Ruleset, IReadOnlyList<Node> Rules) Ruleset(params string[] names)
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        model.AddRoot(ruleset);

        List<Node> rules = [];

        foreach (var name in names)
        {
            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", name);
            rules.Add(rule);
        }

        var session = new EditorSession(ruleset, RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(StandardIntentions.Provider);

        return (session, ruleset, rules);
    }

    /// <summary>Puts the caret on the first cell <paramref name="node"/> owns.</summary>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    /// <summary>The ids of everything offered at the caret, in list order.</summary>
    private static IReadOnlyList<string> Ids(EditorSession session) =>
        [.. IntentionList.For(session).Select(entry => entry.Id)];

    /// <summary>The wrap ids offered at the caret.</summary>
    private static IReadOnlyList<string> Wraps(EditorSession session) =>
        [.. Ids(session).Where(id => id.StartsWith(StandardIntentions.WrapPrefix, StringComparison.Ordinal))];

    /// <summary>The replace ids offered at the caret.</summary>
    private static IReadOnlyList<string> Replaces(EditorSession session) =>
        [.. Ids(session).Where(id => id.StartsWith(StandardIntentions.ReplacePrefix, StringComparison.Ordinal))];

    /// <summary>The one entry with <paramref name="id"/>.</summary>
    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
}
