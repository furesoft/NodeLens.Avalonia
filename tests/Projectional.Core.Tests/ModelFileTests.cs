using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The file side of persistence: that a saved model comes back, and that a save never leaves the
/// target in a state a reader could trip over.
/// </summary>
public sealed class ModelFileTests
{
    [Fact]
    public void ASavedModelLoadsAgain()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("Payroll" + ModelFile.Extension);

        var project = TestLanguage.NewProject();
        var model = project.NewModel("Payroll");
        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", "Salary");
        model.NewNode(TestLanguage.Rule).SetReference("tag", tag);

        ModelFile.Save(model, path);

        Assert.True(File.Exists(path));

        var loaded = ModelFile.Load(TestLanguage.NewProject(), path);

        Assert.Equal(ModelXml.Serialize(model), ModelXml.Serialize(loaded));
    }

    [Fact]
    public void ASaveLeavesNoTemporaryFileBehind()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("Payroll" + ModelFile.Extension);
        var model = TestLanguage.NewModel();

        ModelFile.Save(model, path);

        Assert.Empty(Directory.GetFiles(directory.Root, "*.tmp"));
    }

    [Fact]
    public void ASaveReplacesTheFileThatWasThere()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("Payroll" + ModelFile.Extension);

        var first = TestLanguage.NewModel();
        first.NewNode(TestLanguage.Tag).Set("label", "Salary");
        ModelFile.Save(first, path);

        var second = TestLanguage.NewProject().NewModel("Payroll");
        second.NewNode(TestLanguage.Note).Set("text", "replaced");
        ModelFile.Save(second, path);

        var loaded = ModelFile.Load(TestLanguage.NewProject(), path);

        Assert.Equal(ModelXml.Serialize(second), ModelXml.Serialize(loaded));
        Assert.Equal("replaced", Assert.Single(loaded.Roots).Get("text"));
    }

    [Fact]
    public void ASaveCreatesTheDirectoryItIsPointedAt()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine(Path.Combine("nested", "deeper", "Payroll" + ModelFile.Extension));

        ModelFile.Save(TestLanguage.NewModel(), path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ASaveThatIsRefusedLeavesNeitherTheTargetNorATemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("Payroll" + ModelFile.Extension);

        var model = TestLanguage.NewModel();
        using var transaction = model.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => ModelFile.Save(model, path));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(directory.Root, "*.tmp"));
    }

    [Fact]
    public void LoadingSomethingThatIsNotAModelFileSaysSo()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("broken" + ModelFile.Extension);
        File.WriteAllText(path, "not a model");

        var project = TestLanguage.NewProject();

        Assert.Throws<ModelFormatException>(() => ModelFile.Load(project, path));
        Assert.Empty(project.Models);
    }
}
