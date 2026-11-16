using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One open document: the tree, the layout, the caret and the undo history, kept consistent with each
/// other.
/// </summary>
/// <remarks>
/// <para>
/// The session is what an editor control owns per open model. It exists so that the three derived
/// things cannot drift apart: after every applied edit the tree is rebuilt from the model and the
/// layout from the tree, and the caret is resolved against the tree that just appeared. Nothing else
/// in the layer is allowed to hold a tree across a change, which is why the whole cycle is here rather
/// than in the actions.
/// </para>
/// <para>
/// Rebuilding rather than patching is the deliberate choice M1 makes. A change can alter anything - a
/// property may change a cell's text, adding a child may split a line, a substitution may replace the
/// notation - so the honest response to "the model changed" is a new tree. When that becomes too slow,
/// the answer is an invalidation scheme behind this same interface, not incremental mutation of cells.
/// </para>
/// <para>
/// The session also owns the undo boundary policy: it hands the action's coalescing key to the history
/// before applying and restores the previous key afterwards, so the key is only in force for the
/// commits the action itself makes.
/// </para>
/// </remarks>
public sealed class EditorSession : IDisposable
{
    private readonly NotationRegistry _notations;
    private readonly ICellEmphasis? _emphasis;
    private ITextMeasure _measure;
    private CellLayouter _layouter;
    private double? _wrapWidth;

    private EditorTreeBuilder _builder;

    // View state, like Choice: which nodes are folded, keyed by the stable id so the set survives every
    // rebuild and an undo that moves nodes around.
    private CollapsedNodeSet _collapsed = CollapsedNodeSet.Empty;

    // The two sources of intentions, kept apart so the list's order is fixed rather than accidental. A host
    // may register several; a language hands over one provider that composes what its packages declare.
    private readonly List<IIntentionProvider> _hostIntentions = [];
    private readonly List<IIntentionProvider> _languageIntentions = [];

    /// <summary>Opens <paramref name="document"/> for editing.</summary>
    /// <param name="document">The root of the model to edit.</param>
    /// <param name="notations">The notation that projects the document's concepts.</param>
    /// <param name="measure">How text is measured; monospace when omitted, which is what tests use.</param>
    /// <param name="emphasis">
    /// What decorates the model beyond its notation, asked again on every rebuild; nothing when omitted.
    /// </param>
    /// <param name="choice">
    /// Which variant of each concept to draw; the default variant of everything when omitted.
    /// </param>
    /// <param name="clipboard">
    /// Where Copy, Cut and Paste put and take text; the session's own clipboard when omitted, which keeps
    /// copying and pasting working in a host that has no platform clipboard.
    /// </param>
    /// <param name="typing">
    /// What the language says about text typed into an empty slot; nothing when omitted, which leaves
    /// typing there refused.
    /// </param>
    /// <param name="triggers">
    /// What the language says about text typed at the edge of a node that already exists; nothing when
    /// omitted, which leaves such a keystroke doing what it did - nothing.
    /// </param>
    /// <exception cref="EditingException">The notation produces no cell a caret can stand in.</exception>
    public EditorSession(
        Node document,
        NotationRegistry notations,
        ITextMeasure? measure = null,
        ICellEmphasis? emphasis = null,
        INotationChoice? choice = null,
        ITextClipboard? clipboard = null,
        ITypingRules? typing = null,
        ITriggerRules? triggers = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(notations);

        Document = document;
        _measure = measure ?? MonospaceTextMeasure.Instance;
        History = new UndoHistory(document.Model);

        _notations = notations;
        _emphasis = emphasis;
        _layouter = new CellLayouter(_measure);
        Choice = choice ?? NotationChoice.Default;
        Clipboard = clipboard ?? new InMemoryClipboard();
        Typing = typing ?? TypingRuleRegistry.Empty;
        Triggers = triggers ?? TriggerRegistry.Empty;
        _builder = RebuildBuilder();

        Tree = _builder.Build(Document);
        Layout = _layouter.Layout(Tree);

        // Opening a document puts the caret where the user can start typing, not on the keyword the
        // notation happens to render first.
        var start = CaretPlaces.FirstSlot(Tree);
        Selection = new Selection(start, start);
    }

    /// <summary>The node being edited.</summary>
    public Node Document { get; }

    /// <summary>The notation the document is projected with.</summary>
    /// <remarks>
    /// Public because a second projection of the same model has to be built from the same vocabulary - the
    /// preview sandbox copies the document and projects it again, and a sandbox that guessed the notation would
    /// draw a different document from the one being previewed.
    /// </remarks>
    public NotationRegistry Notations => _notations;

    /// <summary>What decorates the model beyond its notation, or <see langword="null"/> for nothing.</summary>
    /// <remarks>
    /// Exposed for the same reason as <see cref="Notations"/>: a second session over a copy of this document
    /// has to decorate it the same way, or the ghost would show a document that reads differently from the
    /// page it replaces.
    /// </remarks>
    public ICellEmphasis? Emphasis => _emphasis;

    /// <summary>How text is measured.</summary>
    /// <remarks>
    /// <para>
    /// Setting a different measure re-lays-out the document, because every position in
    /// <see cref="Layout"/> is expressed in that measure's units. A host uses this to change the font
    /// size: the projection has no notion of points, so "bigger text" reaches it as "the same
    /// character is now worth more units".
    /// </para>
    /// <para>
    /// View state, like <see cref="WrapWidth"/>. Two hosts can show one document at different sizes
    /// and edit the same model, and nothing about the size reaches the model or the undo history.
    /// </para>
    /// </remarks>
    public ITextMeasure Measure
    {
        get => _measure;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_measure, value))
            {
                return;
            }

            _measure = value;
            _layouter = new CellLayouter(value, _wrapWidth);
            Refresh();
        }
    }

    /// <summary>The undo history of the document's model.</summary>
    public UndoHistory History { get; }

    /// <summary>The tree of the current state of the model.</summary>
    public EditorTree Tree { get; private set; }

    /// <summary>The layout of <see cref="Tree"/>.</summary>
    public CellLayout Layout { get; private set; }

    /// <summary>Where the caret is and what is selected.</summary>
    public Selection Selection { get; private set; }

    /// <summary>
    /// Which variant of each concept is drawn, as the tree was built with it. View state, not model
    /// state: two sessions can hold different choices over one document and edit the same model.
    /// </summary>
    public INotationChoice Choice { get; private set; }

    /// <summary>
    /// The completion list open at the caret, or <see langword="null"/> when none is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// View state, like <see cref="Choice"/>: opening a document shows no list, no list is persisted and
    /// no list is an undo step. A host reads this after every <see cref="Apply"/> to draw or hide its
    /// popup, which is why the property is public while the gestures that set it are ordinary actions.
    /// </para>
    /// <para>
    /// It is cleared by anything that is not a completion gesture, by a move of the caret, and by a
    /// rebuild - a list describes the place at a caret, and none of those leave that description true.
    /// </para>
    /// <para>
    /// A list can also appear without a completion gesture: when the caret <em>arrives</em> in a property
    /// whose type names its values, the values are offered, because that is the one place where the
    /// editor knows the whole answer without being asked. See <see cref="OfferValues"/>.
    /// </para>
    /// </remarks>
    public CompletionList? Completion { get; private set; }

    /// <summary>What a check found wrong with the document, or <see cref="MarkerSet.Empty"/> when nothing did.</summary>
    /// <remarks>
    /// <para>
    /// View state, like <see cref="Choice"/> and <see cref="WrapWidth"/>: never part of the model, never
    /// persisted, never an undo step. Two sessions can draw the same document with different findings, and
    /// a host that runs no checks at all keeps the empty set and pays nothing.
    /// </para>
    /// <para>
    /// The set is replaced, never appended to, because a check describes a state of the model: after an
    /// edit the previous findings are about text that is no longer there. Publishing a whole new set is
    /// therefore the only correct way to use this, and it is what makes the property safe to assign from
    /// a background check while the editor is drawing.
    /// </para>
    /// <para>
    /// Assigning it does not rebuild the tree, the layout or the caret: a marker is decoration, so the
    /// control only needs to repaint. That is the whole reason the geometry is computed at paint time from
    /// the current layout instead of being baked into the frame.
    /// </para>
    /// </remarks>
    public MarkerSet Markers { get; set; } = MarkerSet.Empty;

    /// <summary>What repairs are offered for what the checks found, or <see cref="FixSet.Empty"/>.</summary>
    /// <remarks>
    /// <para>
    /// The companion of <see cref="Markers"/> and view state for the same reasons: never part of the model,
    /// never persisted, never an undo step. A host that runs checks publishes both sets - the findings and
    /// the repairs it can offer for them - and one that runs none keeps both empty.
    /// </para>
    /// <para>
    /// They are two properties rather than one because they answer different questions and have different
    /// readers: a marker set is what a renderer and a hit test read, and neither can do anything with a
    /// repair; a fix set is what the menu, the popup and the keyboard read. They are assigned separately
    /// and replaced as a whole, and neither assignment rebuilds the tree, the layout or the caret - both
    /// describe a document rather than change it.
    /// </para>
    /// </remarks>
    public FixSet Fixes { get; set; } = FixSet.Empty;

    /// <summary>
    /// Registers a source of intentions, from the host or from the language the document is written in.
    /// </summary>
    /// <param name="provider">The source, asked afresh for every list.</param>
    /// <param name="source">
    /// Which group it belongs to, which decides where its entries appear in the list; a host's when omitted.
    /// </param>
    /// <remarks>
    /// <para>
    /// Registration rather than a property, because a host adds what only it can know - "deploy", "generate
    /// now", "open the referenced model" - and a language may add what its concepts mean, and the two must
    /// not overwrite each other. The order within a group is the order of registration; the order between
    /// groups is fixed by <see cref="IntentionSource"/> and is not a host's to change.
    /// </para>
    /// <para>
    /// Registering an intention does not rebuild the tree, the layout or the caret, and the list is derived
    /// per call, so registering never has to be followed by a refresh. A host still registers its sources
    /// when it opens the document rather than in the middle of editing one: a control keeps its "is there
    /// anything here" answer between paints, keyed on the caret and the published sets. Registering the same
    /// provider twice adds it twice, because the session does not guess what the caller meant.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is not a defined value.</exception>
    public void AddIntentions(IIntentionProvider provider, IntentionSource source = IntentionSource.Host)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown intention source.");
        }

        (source == IntentionSource.Host ? _hostIntentions : _languageIntentions).Add(provider);
    }

    /// <summary>Every registered source, in the order the list asks them.</summary>
    /// <remarks>
    /// Host before language, each in registration order - see <see cref="IntentionSource"/> for why. The
    /// fixes are not a provider: they are published as a set and appended by
    /// <see cref="IntentionList"/>.
    /// </remarks>
    internal IEnumerable<IIntentionProvider> IntentionProviders
    {
        get
        {
            foreach (var provider in _hostIntentions)
            {
                yield return provider;
            }

            foreach (var provider in _languageIntentions)
            {
                yield return provider;
            }
        }
    }

    /// <summary>The read-only view of the caret that an intention decides against.</summary>
    /// <remarks>
    /// Built from the tree, layout and selection as they are now, so a provider sees one consistent picture.
    /// The place is resolved here rather than passed in, because an intention is offered for the cell the
    /// caret stands in, and only the tree can answer which cell that is.
    /// </remarks>
    internal IntentionContext CreateIntentionContext() =>
        new(Document, Tree, Layout, Measure, Selection, CaretNavigation.PlaceAt(Tree, Selection.Caret));

    /// <summary>The repairs a gesture offered and the user has not chosen between, or <see langword="null"/>.</summary>
    /// <remarks>
    /// <para>
    /// View state like <see cref="Completion"/> and closed by the same rule: any other action closes it,
    /// because a list of repairs for a caret that has moved is advice about a place the user has left. It
    /// is also dropped whenever the tree is rebuilt, since the model moving under a list is the same
    /// situation as the caret moving out of it.
    /// </para>
    /// <para>
    /// Set to a list of one only when the gesture may not run that repair by itself - it is destructive or
    /// it cannot run - so the property answers "the user is choosing between several" or "the user is being
    /// asked about one" and nothing else.
    /// </para>
    /// </remarks>
    public OfferList? Offered { get; private set; }

    /// <summary>Where Copy, Cut and Paste put and take their text.</summary>
    /// <remarks>
    /// <para>
    /// Never <see langword="null"/>: a session built without one gets an <see cref="InMemoryClipboard"/>,
    /// so the clipboard gestures work in a host with no platform clipboard and in a test with no window.
    /// Handing the same adapter to two sessions is what makes copying between two documents work, which is
    /// a wiring decision this layer leaves to the host.
    /// </para>
    /// <para>
    /// Public because a host needs it for two things: to hand the session a platform adapter, and to decide
    /// whether Paste has anything to offer before it shows a menu entry.
    /// </para>
    /// </remarks>
    public ITextClipboard Clipboard { get; }

    /// <summary>What the language says about text typed while the caret is in an empty slot.</summary>
    /// <remarks>
    /// Never <see langword="null"/>: a session built without one gets <see cref="TypingRuleRegistry.Empty"/>,
    /// which claims nothing, so a host that does not care keeps the older behaviour of ignoring the
    /// keystroke. A language declares its rules next to its notation and hands the same registry to every
    /// session it opens.
    /// </remarks>
    public ITypingRules Typing { get; }

    /// <summary>What the language says about text typed at the edge of a node that already exists.</summary>
    /// <remarks>
    /// Never <see langword="null"/>: a session built without one gets <see cref="TriggerRegistry.Empty"/>,
    /// which claims nothing, so a language that declares no triggers behaves exactly as it did before there
    /// were any. Like <see cref="Typing"/> it is a property of the language rather than of the session, so a
    /// host passes it once when it opens a document.
    /// </remarks>
    public ITriggerRules Triggers { get; }


    /// <summary>
    /// How wide the room a text cell may fill is, or <see langword="null"/> when it may run on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text is wrapped at the room left to this edge, measured from the start of its line, so the
    /// width is the host's page and not a column count: a value that starts further right has less room
    /// before it breaks, which is what makes the wrapping follow the indentation instead of ignoring it.
    /// </para>
    /// <para>
    /// View state, like <see cref="Choice"/>: two hosts can show the same document at different widths
    /// and edit the same model. Setting it re-lays-out the document, so a control publishes it from its
    /// measure pass - it is the only way the projection learns how much room there is.
    /// </para>
    /// </remarks>
    public double? WrapWidth
    {
        get => _wrapWidth;

        set
        {
            if (Nullable.Equals(_wrapWidth, value))
            {
                return;
            }

            _wrapWidth = value;
            _layouter = new CellLayouter(Measure, value);
            Refresh();
        }
    }

    /// <summary>
    /// How tall the visible area is, or zero when the host has not said - what a page move travels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// View state like <see cref="WrapWidth"/>, and for the same reason: the layout is viewport
    /// independent, so the reader has to supply the one number the page moves need. Publishing it
    /// therefore has to stay cheap - it rebuilds nothing, re-lays-out nothing and moves no caret, so a
    /// control may write it from every measure pass without the document noticing.
    /// </para>
    /// <para>
    /// The value is not validated because it is a hint rather than a fact: a page move whose page is
    /// unusable becomes a single-line move, which is a visibly smaller step rather than a wrong jump.
    /// </para>
    /// </remarks>
    public double PageHeight { get; set; }

    /// <summary>
    /// Draws the document with <paramref name="choice"/> from now on.
    /// </summary>
    /// <remarks>
    /// Switching a representation is a re-projection like any other, so it goes through the same
    /// rebuild-and-re-resolve path: the model is untouched, the tree and the layout are new, and the
    /// caret lands on the nearest cell that still exists. A cell the other variant showed and this one
    /// hides is not an error - it is the difference between the two representations.
    /// </remarks>
    public void Choose(INotationChoice? choice)
    {
        Choice = choice ?? NotationChoice.Default;
        _builder = RebuildBuilder();
        Refresh();
    }

    /// <summary>The nodes folded right now, which is view state and never an undo step.</summary>
    /// <remarks>
    /// <para>
    /// Assigning replaces the set and re-projects, unlike <see cref="Markers"/>, which only decorates: a
    /// folded node draws a different tree, so the layout and the caret have to follow.
    /// </para>
    /// <para>
    /// Folding is deliberately not undoable. An undo that silently reopened a fold, or a fold that consumed
    /// an undo step, would each be a surprise; two sessions over one document may hold different sets.
    /// </para>
    /// </remarks>
    public CollapsedNodeSet Collapsed
    {
        get => _collapsed;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_collapsed, value))
            {
                return;
            }

            _collapsed = value;
            _builder = RebuildBuilder();
            Refresh();
        }
    }

    /// <summary>Folds <paramref name="node"/> if it is open and unfolds it if it is folded.</summary>
    /// <param name="node">The node to fold or unfold.</param>
    /// <remarks>
    /// The node is not validated. <see cref="Choose"/> throws on a bad argument because a menu entry that
    /// cannot apply is a programming error; here a stale id is harmless - the builder ignores any id it
    /// cannot fold - and a throw would turn a paint-time race into a crash.
    /// </remarks>
    public void ToggleCollapsed(NodeId node) => Collapsed = _collapsed.Toggle(node);

    /// <summary>Builds the projection from both view-state values, so neither can forget the other.</summary>
    /// <remarks>
    /// One construction path on purpose: the builder is built from <see cref="Choice"/> <em>and</em>
    /// <see cref="Collapsed"/> now, and the drift trap is exactly a <see cref="Choose"/> that later forgets
    /// the folds or a fold setter that forgets the variant.
    /// </remarks>
    private EditorTreeBuilder RebuildBuilder() =>
        new(_notations, _emphasis, Choice, _collapsed);

    /// <summary>
    /// The concepts of this document that declare more than one notation, in the order the document
    /// first uses them.
    /// </summary>
    /// <remarks>
    /// Only concepts the document actually holds are listed, because a representation can only be
    /// switched for a node that is there; the variants of a concept's super concepts are included,
    /// because a variant resolves up the chain. A document whose concepts all declare a single notation
    /// returns nothing, which is how a host knows there is no representation to offer.
    /// </remarks>
    public IReadOnlyList<ConceptVariants> Alternatives()
    {
        var alternatives = new List<ConceptVariants>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in SelfAndDescendants(Document))
        {
            if (!seen.Add(node.Concept.Key))
            {
                continue;
            }

            var variants = VariantsOf(node.Concept);

            if (variants.Count > 1)
            {
                alternatives.Add(new ConceptVariants(
                    node.Concept.Key,
                    variants,
                    Choice.VariantOf(node.Concept.Key) ?? Notation.DefaultVariant));
            }
        }

        return alternatives;
    }

    /// <summary>Draws the nodes of <paramref name="conceptKey"/> in <paramref name="variant"/> from now on.</summary>
    /// <param name="conceptKey">A concept the document holds.</param>
    /// <param name="variant">A variant that applies to it, its own or one it inherits.</param>
    /// <remarks>
    /// The choice is one more opinion on top of the one in force, so a document can be read as a table
    /// in one place and as a block in another. Both arguments are checked against the document, because
    /// a menu that offers a variant the notation does not declare would otherwise re-project to
    /// something that looks like nothing happened.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The document holds no node of <paramref name="conceptKey"/>, or it declares no such variant.
    /// </exception>
    public void ChooseVariant(string conceptKey, string variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(variant);

        var concept = SelfAndDescendants(Document)
            .Select(node => node.Concept)
            .FirstOrDefault(candidate => string.Equals(candidate.Key, conceptKey, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                $"The document holds no node of concept '{conceptKey}', so there is nothing to re-project.",
                nameof(conceptKey));

        if (!VariantsOf(concept).Contains(variant, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Concept '{conceptKey}' declares no '{variant}' notation.",
                nameof(variant));
        }

        Choose((Choice as NotationChoice ?? NotationChoice.Default).With(conceptKey, variant));
    }

    /// <summary>Applies <paramref name="action"/> and leaves the session consistent with the result.</summary>
    /// <remarks>
    /// The selection the action asks for - both ends of it - is resolved against the tree the action
    /// produced, so an action may return an address that only exists after it ran, which is exactly what
    /// adding a child needs. An action that reports no change still gets to move the selection, because
    /// that is how the caret moves at all and how Select All selects anything. An action that created
    /// something to be named gets the value the caret landed in selected; see
    /// <see cref="EditAction.SelectsLandedValue"/>.
    /// </remarks>
    public EditResult Apply(EditAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action is EditProgram program)
        {
            return Apply(program);
        }

        // The three gesture actions are answered by the session, which is the only thing holding the sets
        // and the open list. They run before the "any other action closes the list" rule below, because for
        // them the list is the point rather than something to close.
        if (action is OfferAction)
        {
            return ApplyOffers();
        }

        if (action is AcceptOfferAction)
        {
            return AcceptOffer();
        }

        if (action is SelectOfferAction select)
        {
            MoveOfferSelection(select.Delta);

            return EditResult.Unchanged(Selection);
        }

        // Every other action closes an offered list, for the same reason it closes the completion list:
        // the user did something else, and the offer described where they were.
        Offered = null;

        var context = ContextFor(action);

        return ApplyCore(action, context, action.CoalescingKey(context));
    }

    /// <summary>Applies <paramref name="program"/> as one step, or not at all.</summary>
    /// <param name="program">The edits to apply, in order.</param>
    /// <remarks>
    /// <para>
    /// This is the only way to run a program: the tree and the layout are rebuilt between steps, because a
    /// step's addresses describe the model the step before it produced. That is also why the whole program
    /// sits inside one model transaction - the rebuilds are an implementation detail of running the steps,
    /// not edit boundaries the user should see.
    /// </para>
    /// <para>
    /// A rejected step takes the whole program back: the transaction rolls back, the tree is rebuilt to
    /// describe the state that was restored, and the answer is that step's own <see cref="EditOutcome.Rejected"/>
    /// - so a repair that could not finish leaves nothing half-applied and still says why.
    /// </para>
    /// </remarks>
    public EditResult Apply(EditProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (program.Steps.Count == 0)
        {
            return EditResult.Unchanged(Selection);
        }

        var selection = Selection;

        using var transaction = Document.Model.BeginTransaction();

        var result = EditResult.Unchanged(Selection);

        foreach (var step in program.Steps)
        {
            result = ApplyCore(step, ContextFor(step), coalescingKey: null);

            if (result.Outcome == EditOutcome.Rejected)
            {
                // Rolling back restores the model, not the session: the tree and the layout still describe
                // the edits that were just taken back, and the selection still points into them.
                transaction.Dispose();
                Rebuild();
                Settle(selection);

                return result;
            }
        }

        transaction.Complete();

        return result;
    }

    /// <summary>Answers the offers gesture: nothing here, one plain offer, or a list to choose from.</summary>
    /// <remarks>
    /// <para>
    /// The question is <see cref="IntentionList.For(EditorSession)"/>, not "which repairs exist": the user
    /// sees one list, so the host's commands, the language's intentions and the caret line's quick fixes are
    /// all candidates for the same keystroke.
    /// </para>
    /// <para>
    /// One offer is applied immediately when it is plain and can run. The common case is a single cure for a
    /// single finding, and a list with one entry costs the user a keystroke to say what the editor already
    /// knew.
    /// </para>
    /// <para>
    /// One offer that is destructive, or that cannot run at all, opens the list instead. The gesture asks
    /// what is available; it must not also be the keystroke that deletes a node or that does nothing while a
    /// reason exists. A list of one therefore means "this is what is here, do you mean it", which is the
    /// same shape as a choice and a different sentence.
    /// </para>
    /// <para>
    /// Several offers open a list, and the answer is <see cref="EditOutcome.Unchanged"/>: the gesture was
    /// handled, but nothing about the model changed. The list is what the caller reads to draw it - the
    /// same contract the completion list has, which is why the control only has to look at the session
    /// after the gesture.
    /// </para>
    /// <para>
    /// Nothing to offer answers unchanged as well and leaves no list behind. A refused gesture would leak
    /// into the host as a beep for a key that is perfectly legal, and "there is nothing to do here" is an
    /// answer rather than an error.
    /// </para>
    /// </remarks>
    public EditResult ApplyOffers()
    {
        var offered = IntentionList.For(this);

        if (offered.Count == 0)
        {
            Offered = null;

            return EditResult.Unchanged(Selection);
        }

        if (offered.Count == 1 && offered[0] is { Safety: EditSafety.Plain, IsDisabled: false })
        {
            Offered = null;

            return Apply(offered[0]);
        }

        Offered = new OfferList(offered);

        return EditResult.Unchanged(Selection);
    }

    /// <summary>Moves the highlight in the open list, if there is one.</summary>
    /// <param name="delta">How many rows to move, negative towards the front.</param>
    /// <remarks>
    /// Nothing happens without an open list, which is the honest answer: a key that moves a highlight has
    /// no meaning when nothing is highlighted, and inventing one would make Down mean something different
    /// depending on what is on screen.
    /// </remarks>
    private void MoveOfferSelection(int delta)
    {
        if (Offered is { } offered)
        {
            Offered = offered.Move(delta);
        }
    }

    /// <summary>Applies the highlighted offer of the open list, if there is one.</summary>
    /// <remarks>
    /// The list is closed first, so the answer the caller reads afterwards describes what happened rather
    /// than what was on screen: an offer that renames a node rebuilds the tree, and a list of offers for the
    /// old layout would be advice about a document that no longer exists. An entry that came from a quick fix
    /// keeps its finding, so <see cref="Apply(Intention)"/> can route it through the repair's own path.
    /// </remarks>
    private EditResult AcceptOffer()
    {
        if (Offered is not { } offered)
        {
            return EditResult.Unchanged(Selection);
        }

        var pick = offered.SelectedEntry;

        Offered = null;

        return Apply(pick);
    }

    /// <summary>Applies <paramref name="fix"/> as an undo step of its own, or rejects the pick.</summary>
    /// <param name="fix">A fix the caller was handed, which is matched against the current set.</param>
    /// <remarks>
    /// <para>
    /// The pick is joined on (finding, id) against the current <see cref="Fixes"/>, and the <em>current</em>
    /// object is what runs. Everything a fix knows was computed against the model of the moment its set was
    /// published, so running the caller's copy after an undo is how a repair lands on a document that has
    /// moved on.
    /// </para>
    /// <para>
    /// A pick that no longer resolves is the ordinary case rather than an error: the user may have repaired
    /// the finding another way, or the checks may have run again and reported something else. It is
    /// therefore an <see cref="EditOutcome.Rejected"/> with a reason, the same outcome a refused keystroke
    /// gets, so the host can say what happened instead of nothing happening.
    /// </para>
    /// <para>
    /// A fix runs as one step: the coalescing key is cleared for its commits, so it cannot merge into a
    /// typing burst before it, and a keystroke after it starts a step of its own.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fix"/> is <see langword="null"/>.</exception>
    public EditResult Apply(DiagnosticFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);

        if (Current(fix) is not { } current)
        {
            return EditResult.Rejected(
                Selection,
                $"The fix \"{fix.Label}\" is no longer offered for this document.");
        }

        if (current.Availability.Reason is { } reason)
        {
            return EditResult.Rejected(Selection, reason);
        }

        var context = new FixContext(
            Document.Model,
            current.Diagnostic,
            Document.Model.Project.FindNode(current.Diagnostic.Target));

        if (current.Create(context) is not { } action)
        {
            return EditResult.Rejected(
                Selection,
                $"The fix \"{current.Label}\" has nothing to do for this document.");
        }

        return action is EditProgram program
            ? Apply(program)
            : ApplyCore(action, ContextFor(action), coalescingKey: null);
    }

    /// <summary>Applies <paramref name="intention"/> as an undo step of its own, or rejects it.</summary>
    /// <param name="intention">The offer the user picked, as the list handed it over.</param>
    /// <remarks>
    /// <para>
    /// The intention builds its action <em>now</em>, against the model the caret is in, so a long-lived
    /// declaration cannot produce an edit for a document that has moved on. What it answers is applied the
    /// way a keystroke is - with the coalescing key cleared - so the offer is one undo step and a following
    /// keystroke starts another. A repair that needs several edits answers an <see cref="EditProgram"/> and
    /// the session runs it in one transaction, so a step that is refused takes the whole offer back.
    /// </para>
    /// <para>
    /// An offer from the quick-fix catalogue (one carrying a <see cref="Intention.Fix"/>) is routed back
    /// through <see cref="Apply(DiagnosticFix)"/> instead. That path re-resolves the repair against the
    /// current fix set and refuses a pick whose finding is gone - a guarantee the general route does not
    /// need, because a declared intention is derived from the caret it is applied at.
    /// </para>
    /// <para>
    /// An intention that no longer applies - absent by now, or disabled - is refused with its reason rather
    /// than applied, for the same reason a stale fix is: the user's list described a document that may have
    /// been edited since, and an offer is not a promise that it is still true.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="intention"/> is <see langword="null"/>.</exception>
    public EditResult Apply(Intention intention)
    {
        ArgumentNullException.ThrowIfNull(intention);

        // The user chose this offer instead of the ones that were listed, so any open list is about a place
        // they have left.
        Offered = null;

        if (intention.Fix is { } fix)
        {
            return Apply(fix);
        }

        if (intention.Availability.Reason is { } reason)
        {
            return EditResult.Rejected(Selection, reason);
        }

        if (intention.Availability.IsAbsent)
        {
            return EditResult.Rejected(
                Selection,
                $"The intention \"{intention.Label}\" no longer applies at this caret.");
        }

        var action = intention.Create(CreateIntentionContext());

        if (action is null)
        {
            return EditResult.Rejected(
                Selection,
                $"The intention \"{intention.Label}\" has nothing to do for this document.");
        }

        return action is EditProgram program
            ? Apply(program)
            : ApplyCore(action, ContextFor(action), coalescingKey: null);
    }

    /// <summary>The fix in the current set that <paramref name="fix"/> names, or <see langword="null"/>.</summary>
    private DiagnosticFix? Current(DiagnosticFix fix)
    {
        foreach (var candidate in Fixes.For(fix.Diagnostic))
        {
            if (string.Equals(candidate.Id, fix.Id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The context one action sees, built from the tree and layout as they are now.</summary>
    private EditContext ContextFor(EditAction action) =>
        new(Document, Tree, Layout, Measure, PageHeight, Selection, Clipboard, Typing, Triggers)
        {
            Completion = action is CompletionAction ? Completion : null,
        };

    /// <summary>Applies one action and leaves the session consistent with what it did.</summary>
    /// <param name="action">The action to run; not a program.</param>
    /// <param name="context">The context it sees.</param>
    /// <param name="coalescingKey">
    /// The key its commits are grouped under, or <see langword="null"/> when the edit must be a step of its
    /// own. Passed in rather than read from the action so that a fix can force a boundary.
    /// </param>
    private EditResult ApplyCore(EditAction action, EditContext context, string? coalescingKey)
    {
        var previous = History.CoalescingKey;
        var before = Selection.Caret;
        var result = EditResult.Unchanged(Selection);

        try
        {
            History.CoalescingKey = coalescingKey;
            result = action.Apply(context);
        }
        finally
        {
            History.CoalescingKey = previous;
        }

        Completion = context.Completion;

        if (result.Changed)
        {
            Rebuild();
        }

        Settle(result.Selection);

        if (action.SelectsLandedValue)
        {
            SelectLandedValue();
        }

        if (action is not CompletionAction && !Selection.Caret.Equals(before))
        {
            OfferValues();
        }

        return result;
    }

    /// <summary>
    /// Opens the value list when the caret stands in a property whose type names its values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An enumeration is the only kind of slot whose whole answer is known without typing, so arriving in
    /// one offers the values instead of waiting for Ctrl+Space. That also repairs the one property a user
    /// could not retype at all: every prefix of a symbol is an illegal value, so typing over the word was
    /// refused, and the list is what turns those keystrokes into a filter.
    /// </para>
    /// <para>
    /// Only arrival opens a list - the caller has already decided that the caret moved, and no completion
    /// gesture is involved. A keystroke that leaves the caret where it is therefore cannot reopen a list
    /// the user dismissed with Escape, and accepting a value leaves the caret in the same place, so the
    /// list closes and stays closed until the caret leaves the cell and comes back.
    /// </para>
    /// <para>
    /// A selection inside the cell does not suppress the list, because selecting the value is exactly how a
    /// user says "I am about to retype this" - and for an enumeration that is the only gesture left, since
    /// every prefix of a symbol is an illegal value. A selection that spans cells is left alone: the caret
    /// then stands for a range, and a list describing one cell would answer a question nobody asked.
    /// </para>
    /// </remarks>
    private void OfferValues()
    {
        if (Completion is not null
            || !Selection.IsSingleCell
            || CaretNavigation.PlaceAt(Tree, Selection.Caret) is not { Cell: PropertyCell property })
        {
            return;
        }

        Completion = CompletionSource.Values(property);
    }

    /// <summary>Widens the caret to the whole value of the cell it landed in, when that cell has text.</summary>
    /// <remarks>
    /// Read from the tree the edit produced rather than predicted by the action: the coercion has
    /// already decided which cell the caret lands in, so all that is left to say is that the value found
    /// there is the one the user is about to type over. A cell with nothing in it keeps a bare caret,
    /// because selecting an empty range and leaving the caret where it is are the same thing.
    /// </remarks>
    private void SelectLandedValue()
    {
        if (CaretNavigation.PlaceAt(Tree, Selection.Caret) is not { IsEditable: true, Length: > 0 } place)
        {
            return;
        }

        Selection = new Selection(CaretPosition.At(place, 0), CaretPosition.At(place, place.Length));
    }

    /// <summary>Moves the caret, dropping any selection that does not fit the tree.</summary>
    /// <remarks>
    /// Both ends are coerced, so a selection whose anchor was in a deleted node collapses onto the
    /// caret instead of pointing at a cell that no longer exists. Moving the caret also closes any open
    /// completion list, because the proposals describe the place the caret was in - and then offers the
    /// values of the property it arrived in, which is the same arrival <see cref="Apply"/> acts on. A
    /// click is a move the session was told about rather than one an action performed, so it cannot rely
    /// on an action having reported it.
    /// </remarks>
    public void Select(Selection selection)
    {
        var anchor = CaretNavigation.Nearest(Tree, selection.Anchor);
        var caret = CaretNavigation.Nearest(Tree, selection.Caret);

        Selection = new Selection(anchor, caret);
        Completion = null;

        // A caret that moved is no longer the caret that asked for a repair list. The list describes one
        // line's findings, so leaving it up would keep showing them beside a caret on a different line.
        Offered = null;

        OfferValues();
    }

    /// <summary>Undoes one step of the model's history, keeping the caret on the nearest cell.</summary>
    /// <returns>Whether there was a step to undo.</returns>
    /// <remarks>
    /// The caret is not restored to where it was when the change was made. Undo replays model changes,
    /// which say nothing about carets, so the edited cell may not exist; the nearest cell in the tree
    /// the undo produced is the only answer that is not a guess.
    /// </remarks>
    public bool Undo()
    {
        if (!History.Undo())
        {
            return false;
        }

        Refresh();

        return true;
    }

    /// <summary>Redoes one step of the model's history, keeping the caret on the nearest cell.</summary>
    /// <returns>Whether there was a step to redo.</returns>
    public bool Redo()
    {
        if (!History.Redo())
        {
            return false;
        }

        Refresh();

        return true;
    }

    /// <summary>Rebuilds the tree and the layout from the model, then re-resolves the selection.</summary>
    /// <remarks>
    /// For changes the session did not make - a substitution, a background transformation, a test that
    /// wrote to the model directly. Both ends of the selection are resolved against the tree that just
    /// appeared, so a range stays a range when the cells it named survived and collapses onto the caret
    /// when an end was in a node that is gone; there is nothing better to do with a position that names
    /// a cell that no longer exists. An open completion list is closed for the same reason: it names a
    /// slot of the tree that was just replaced.
    /// </remarks>
    public void Refresh()
    {
        Completion = null;
        Rebuild();
        Settle(Selection);
    }

    /// <summary>Stops listening to the model's changes.</summary>
    public void Dispose() => History.Dispose();

    private void Rebuild()
    {
        Tree = _builder.Build(Document);
        Layout = _layouter.Layout(Tree);

        // The offer named a caret line of the layout that has just been replaced.
        Offered = null;
    }

    /// <summary>Puts the selection onto cells of the current tree, keeping a range whole.</summary>
    /// <remarks>
    /// Both ends are resolved, not just the caret, because the selection belongs to the action: Select All
    /// produces a range and a range that lost its anchor on the way out of the session would be a
    /// highlight the user never sees. Every action that reports an empty selection gets the old behaviour
    /// unchanged - its two ends are the same position, so both resolve to the same cell.
    /// </remarks>
    private void Settle(Selection selection)
    {
        Selection = new Selection(
            CaretNavigation.Nearest(Tree, selection.Anchor),
            CaretNavigation.Nearest(Tree, selection.Caret));
    }

    private static IEnumerable<Node> SelfAndDescendants(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private List<string> VariantsOf(Concept concept)
    {
        var variants = new List<string>();

        Collect(concept);
        return variants;

        void Collect(Concept current)
        {
            foreach (var variant in _notations.VariantsOf(current.Key))
            {
                if (!variants.Contains(variant, StringComparer.Ordinal))
                {
                    variants.Add(variant);
                }
            }

            foreach (var super in current.SuperConcepts)
            {
                Collect(super);
            }
        }
    }
}
