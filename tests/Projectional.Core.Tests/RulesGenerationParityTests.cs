using System.Reflection;

using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.LanguageExport;
using Projectional.Samples;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The gate <c>docs/architecture/csharp.md</c> states: the interpreter and the generated C# agree on one corpus.
/// </summary>
/// <remarks>
/// <para>
/// Two independent implementations of one meaning, compared on the same documents: the interpreter walks the model,
/// the generator writes a C# fragment, Roslyn compiles it and the method is invoked with the same inputs. A generator
/// that merely round-tripped its own text would prove nothing about what the code means.
/// </para>
/// <para>
/// The corpus is code rather than files because a case carries what no model holds: the signature the host wraps the
/// generated fragment in. The fragment is a method body - the generator says so, because only a language knows how a
/// ruleset becomes a method - so the host supplies the parameters, and the same signature is what the interpreter is
/// bound with. That is what makes the two runs comparable at all.
/// </para>
/// <para>
/// A ruleset with no matching rule is a failure in the interpreter, never a default outcome, so the host's method
/// throws. The corpus states that as <see langword="null"/>, and both sides have to answer it: neither may invent a
/// decision.
/// </para>
/// </remarks>
public sealed class RulesGenerationParityTests
{
    [Fact]
    public void TheInterpreterAndTheCompiledGeneratedCodeAgreeOnEveryCase()
    {
        var cases = Cases();

        Assert.NotEmpty(cases);

        var mismatches = new List<string>();

        for (var index = 0; index < cases.Count; index++)
        {
            var each = cases[index];

            var interpreted = new Interpreter(RulesEvaluation.Registry)
                .Run(each.Model.Roots[0], Bindings(each), cancellationToken: TestContext.Current.CancellationToken);

            var generated = new Generator().Generate(each.Model.Roots[0], RulesTextGenerators.CSharp);

            if (!generated.Succeeded)
            {
                mismatches.Add($"{each.Name}: the generator refused the document");
                continue;
            }

            object? compiled;

            try
            {
                compiled = Invoke(each, generated.Text, index);
            }
            catch (ExportCompilationException exception)
            {
                mismatches.Add($"{each.Name}: the generated fragment does not compile - {exception.Message}");
                continue;
            }
            catch (TargetInvocationException exception)
                when (each.Outcome is null && exception.InnerException is InvalidOperationException)
            {
                // The host's method reports "no rule matched" by throwing, which is the interpreter's failure spelled
                // as C#. Any other exception is a defect in the generated code and is reported below.
                compiled = null;
            }
            catch (Exception exception)
            {
                mismatches.Add($"{each.Name}: the compiled code failed - {exception.GetType().Name}: {exception.Message}");
                continue;
            }

            var interpretedOutcome = interpreted.Succeeded && interpreted.Value is StringValue decision
                ? decision.Content
                : null;

            if (!string.Equals(interpretedOutcome, each.Outcome, StringComparison.Ordinal))
            {
                var answer = interpreted.Succeeded
                    ? Show(interpretedOutcome)
                    : $"failed: {interpreted.Failure?.Message}";

                mismatches.Add($"{each.Name}: the interpreter {answer}, the corpus says {Show(each.Outcome)}");
            }

            if (!string.Equals(compiled as string, each.Outcome, StringComparison.Ordinal))
            {
                mismatches.Add($"{each.Name}: the compiled code decided {Show(compiled as string)}, the corpus says {Show(each.Outcome)}");
            }
        }

        Assert.Empty(mismatches);
    }

    /// <summary>Wraps the generated fragment in the host's method, compiles it and invokes it.</summary>
    private static object? Invoke(ParityCase each, string fragment, int index)
    {
        var signature = string.Join(", ", each.Parameters.Select(parameter => $"{parameter.Type} {parameter.Name}"));

        var source = string.Join(
            '\n',
            "public static class Rules",
            "{",
            $"  public static string Decide({signature})",
            "  {",
            fragment,
            "    throw new System.InvalidOperationException(\"No rule matched, so the ruleset has no outcome.\");",
            "  }",
            "}");

        var assembly = AssemblyCompiler.Compile($"RulesParity{index}", source);

        var type = Assembly.Load(assembly.Bytes).GetType("Rules", throwOnError: true)!;

        return type.GetMethod("Decide")!.Invoke(null, [.. each.Parameters.Select(parameter => (object?)parameter.Value)]);
    }

    /// <summary>The values the interpreter is bound with, in the order the signature declares them.</summary>
    private static Dictionary<string, Value> Bindings(ParityCase each)
    {
        Dictionary<string, Value> bindings = new(StringComparer.Ordinal);

        foreach (var parameter in each.Parameters)
        {
            bindings[parameter.Name] = parameter.Value switch
            {
                int number => new IntValue(number),
                bool truth => new BoolValue(truth),
                string text => new StringValue(text),
                _ => throw new InvalidOperationException(
                    $"The corpus has no interpreter value for '{parameter.Value.GetType().Name}'."),
            };
        }

        return bindings;
    }

    private static string Show(string? outcome) => outcome is null ? "no decision" : $"\"{outcome}\"";

    /// <summary>
    /// The shared corpus: one ruleset per semantic shape the language has, and the inputs either side of a decision.
    /// </summary>
    private static List<ParityCase> Cases()
    {
        var sample = PayrollSample.NewSampleModel();
        var ordered = Ordered();
        var conjunction = Conjunction();

        return
        [
            // One rule, and the two inputs either side of its threshold: the decision, and the gap the interpreter
            // reports rather than inventing an outcome for.
            new("a rule that applies", sample, [new Parameter("int", "amount", 1500)], RuleOutcomes.Accept),
            new("a rule that does not apply", sample, [new Parameter("int", "amount", 500)], null),

            // Arithmetic in the condition: the fragment parenthesises every binary, so the generated line reads the
            // same tree the interpreter walks.
            new("arithmetic binds before the comparison", Arithmetic(), [new Parameter("int", "amount", 950)], RuleOutcomes.Accept),
            new("a comparison that is false", Arithmetic(), [new Parameter("int", "amount", 800)], null),

            // A ruleset is a decision list, so the order of the rules is semantics rather than presentation.
            new("the first rule that applies wins", ordered, [new Parameter("int", "amount", 1500)], RuleOutcomes.Accept),
            new("the second rule decides when the first does not", ordered, [new Parameter("int", "amount", 700)], RuleOutcomes.Review),

            // Conditions that are not a bare comparison: a conjunction, a negation and a conditional expression.
            new("a conjunction", conjunction, [new Parameter("bool", "flag", true), new Parameter("int", "amount", 200)], RuleOutcomes.Review),
            new("a conjunction whose left operand is false", conjunction, [new Parameter("bool", "flag", false), new Parameter("int", "amount", 200)], null),
            new("a negation", Negation(), [new Parameter("bool", "flag", false)], RuleOutcomes.Reject),
            new("a conditional expression", Conditional(), [new Parameter("int", "amount", 1500)], RuleOutcomes.Accept),

            // A ruleset with no rule at all: nothing can match, and neither implementation invents a decision.
            new("a ruleset with no rule", NoRules(), [], null),
        ];
    }

    /// <summary><c>rule Counted when amount + 100 &gt; 1000 then Accept</c>.</summary>
    private static Model Arithmetic()
    {
        var model = NewModel();
        var ruleset = Ruleset(model, "Arithmetic");
        var rule = Rule(model, ruleset, "Counted", RuleOutcomes.Accept);

        var condition = Binary(model, rule, "condition", ">");
        var sum = Binary(model, condition, "left", "+");
        Name(model, sum, "left", "amount");
        Int(model, sum, "right", 100);
        Int(model, condition, "right", 1000);

        return model;
    }

    /// <summary>Two rules, of which the second applies when the first does not.</summary>
    private static Model Ordered()
    {
        var model = NewModel();
        var ruleset = Ruleset(model, "Ordered");

        var first = Rule(model, ruleset, "Approved", RuleOutcomes.Accept);
        var above = Binary(model, first, "condition", ">");
        Name(model, above, "left", "amount");
        Int(model, above, "right", 1000);

        var second = Rule(model, ruleset, "Manual", RuleOutcomes.Review);
        var aboveLower = Binary(model, second, "condition", ">");
        Name(model, aboveLower, "left", "amount");
        Int(model, aboveLower, "right", 500);

        return model;
    }

    /// <summary><c>rule Reviewed when flag &amp;&amp; amount &gt; 100 then Review</c>.</summary>
    private static Model Conjunction()
    {
        var model = NewModel();
        var ruleset = Ruleset(model, "Conjunction");
        var rule = Rule(model, ruleset, "Reviewed", RuleOutcomes.Review);

        var condition = Binary(model, rule, "condition", "&&");
        Name(model, condition, "left", "flag");
        var comparison = Binary(model, condition, "right", ">");
        Name(model, comparison, "left", "amount");
        Int(model, comparison, "right", 100);

        return model;
    }

    /// <summary><c>rule Blocked when !flag then Reject</c>.</summary>
    private static Model Negation()
    {
        var model = NewModel();
        var ruleset = Ruleset(model, "Negation");
        var rule = Rule(model, ruleset, "Blocked", RuleOutcomes.Reject);

        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.Unary);
        condition.Set("operator", "!");
        Name(model, condition, "operand", "flag");

        return model;
    }

    /// <summary><c>rule Approved when amount &gt; 1000 ? true : false then Accept</c>.</summary>
    private static Model Conditional()
    {
        var model = NewModel();
        var ruleset = Ruleset(model, "Conditional");
        var rule = Rule(model, ruleset, "Approved", RuleOutcomes.Accept);

        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.If);
        var comparison = Binary(model, condition, "condition", ">");
        Name(model, comparison, "left", "amount");
        Int(model, comparison, "right", 1000);
        Bool(model, condition, "then", true);
        Bool(model, condition, "else", false);

        return model;
    }

    /// <summary>A ruleset with no rule, so nothing can match.</summary>
    private static Model NoRules()
    {
        var model = NewModel();
        Ruleset(model, "Empty");

        return model;
    }

    private static Model NewModel() => RulesLanguage.NewProject().NewModel("parity");

    private static Node Ruleset(Model model, string name)
    {
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", name);
        model.AddRoot(ruleset);

        return ruleset;
    }

    private static Node Rule(Model model, Node ruleset, string name, string outcome)
    {
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", name);
        rule.Set("outcome", outcome);

        return rule;
    }

    private static Node Binary(Model model, Node parent, string slot, string op)
    {
        var binary = model.NewChild(parent, slot, ExpressionsConceptKeys.Binary);
        binary.Set("operator", op);

        return binary;
    }

    private static Node Name(Model model, Node parent, string slot, string name)
    {
        var node = model.NewChild(parent, slot, ExpressionsConceptKeys.Name);
        node.Set("name", name);

        return node;
    }

    private static Node Int(Model model, Node parent, string slot, int value)
    {
        var node = model.NewChild(parent, slot, ExpressionsConceptKeys.IntLiteral);
        node.Set("value", value);

        return node;
    }

    private static Node Bool(Model model, Node parent, string slot, bool value)
    {
        var node = model.NewChild(parent, slot, ExpressionsConceptKeys.BoolLiteral);
        node.Set("value", value);

        return node;
    }

    /// <summary>One document, the signature the host wraps its generated body in, and the decision both must reach.</summary>
    /// <param name="Name">What the case is, used in a failure message.</param>
    /// <param name="Model">The document both implementations read.</param>
    /// <param name="Parameters">The method's parameters, which are also the interpreter's bindings.</param>
    /// <param name="Outcome">The decision, or <see langword="null"/> when no rule matches.</param>
    private sealed record ParityCase(
        string Name,
        Model Model,
        IReadOnlyList<Parameter> Parameters,
        string? Outcome);

    /// <summary>One parameter of the host's method: its C# type, its name and the value both runs are given.</summary>
    private sealed record Parameter(string Type, string Name, object Value);
}
