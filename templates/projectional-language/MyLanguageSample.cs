using Projectional.Core;

namespace MyLanguage;

/// <summary>A small document of this language, so the tests and a first run have something to show.</summary>
/// <remarks>
/// Keep it in the language project rather than in the tests: it is the example a reader opens first, and a fixture the
/// tests build themselves is a fixture that can drift from the one a human sees.
/// </remarks>
public static class MyLanguageSample
{
    /// <summary>A board with two items.</summary>
    public static Model NewSampleModel()
    {
        var model = MyLanguageConcepts.NewProject().NewModel("sample");
        var board = model.NewNode(MyLanguageConcepts.Board);

        board.Set("title", "Shopping");

        Add(board, "milk");
        Add(board, "bread");

        return model;
    }

    private static void Add(Node board, string text) =>
        board.Model.NewChild(board, MyLanguageConcepts.ItemsSlot, MyLanguageConcepts.Item).Set("text", text);
}