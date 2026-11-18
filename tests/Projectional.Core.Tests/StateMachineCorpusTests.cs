using System.Text;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples.StateMachine;
using Projectional.Samples.StateMachine.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The state machine sample ships a corpus: a machine the writer produced once, which now has to keep loading,
/// keep saving back to the same bytes and keep drawing the same picture.
/// </summary>
/// <remarks>
/// This is the rule <c>docs/architecture/testing.md</c> states for a language package - a sample with no corpus
/// and no snapshot is documentation that rots. The file lives beside the language rather than in the shared
/// corpus, because the shared one is written in <c>TestLanguage</c> and a second language's document there would
/// tie the framework's format test to a sample that is free to change.
/// </remarks>
public sealed class StateMachineCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "StateMachine");

    [Fact]
    public void TheSampleMachineSavesBackByteIdentically()
    {
        var stored = File.ReadAllBytes(Path.Combine(CorpusDirectory, "release.mps"));

        var model = ModelXml.Deserialize(StateMachineLanguage.NewProject(), Encoding.UTF8.GetString(stored));

        Assert.Equal(stored, Encoding.UTF8.GetBytes(ModelXml.Serialize(model)));
    }

    [Fact]
    public void TheSampleMachineIsCleanUnderItsOwnRule()
    {
        // The shipped document is what a user opens first, so a finding on it would greet them with a warning
        // about the sample itself.
        Assert.Empty(new ConstraintEngine(StateMachineConstraints.All).Check(Load()));
    }

    [Fact]
    public void TheSampleMachineDrawsBothOfItsNotations()
    {
        var machine = Load().Roots[0];
        var notations = StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts);

        Assert.Equal(
            """
            machine Release {
              initial: Idle
              states:
                state Idle
                state Running
                state Done final
              transitions:
                on start from Idle to Running when attempts > 0
                on finish from Running to Done when <empty>
            }
            """,
            LayoutSnapshot.Render(machine, notations, width: 120));

        Assert.Equal(
            """
            machine Release
              initial: Idle
              event  from    to      when
              start  Idle    Running attempts > 0
              finish Running Done    <empty>
            """,
            LayoutSnapshot.Render(
                machine,
                notations,
                width: 120,
                choice: NotationChoice.Default.With(SmConceptKeys.Machine, StateMachineLanguage.TableVariant)));
    }

    private static Model Load() =>
        ModelXml.Deserialize(
            StateMachineLanguage.NewProject(),
            File.ReadAllText(Path.Combine(CorpusDirectory, "release.mps")));
}
