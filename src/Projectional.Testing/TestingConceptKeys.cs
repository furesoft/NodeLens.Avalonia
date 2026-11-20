namespace Projectional.Testing;

/// <summary>The keys of the test language: the concepts a language's own tests are written in.</summary>
/// <remarks>
/// <para>
/// Three concepts, and the closed set is the point: a test says what document it is about and what it asserts
/// about it, and the assertion is one of a handful of bodies the framework knows how to run. A test that could
/// compute would be a program, and a program in the tests folder would be a second way to write code - the
/// reason the constraint language has four bodies and the template language twelve parts.
/// </para>
/// <para>
/// The tests folder sits beside the aspect folders of a language project, one test per file, so that file-name
/// order is the order the tests run in and a diff of one test is a diff of one definition.
/// </para>
/// </remarks>
public static class TestingConceptKeys
{
    /// <summary>One test: the document it is about and the one assertion it makes.</summary>
    public const string Test = "Testing.Test";

    /// <summary>What a test asserts, abstract because the kinds are the closed set below.</summary>
    public const string Assertion = "Testing.Assertion";

    /// <summary>The document is written and read back to the same bytes.</summary>
    public const string RoundTrips = "Testing.RoundTrips";

    /// <summary>The document draws as the text the test spells out, one line per entry.</summary>
    public const string Draws = "Testing.Draws";

    /// <summary>One line of a drawn expectation, exactly as the renderer writes it.</summary>
    public const string Line = "Testing.Line";

    /// <summary>The document reports exactly the findings the test lists.</summary>
    public const string Findings = "Testing.Findings";

    /// <summary>One expected finding: its code, its severity and the slot it points at.</summary>
    public const string ExpectedFinding = "Testing.ExpectedFinding";
}
