using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Semantics;
using Projectional.Structures;

namespace Projectional.Web;

/// <summary>
/// The declared sample languages (<c>samples/declared-*</c>) as languages the page can open.
/// </summary>
/// <remarks>
/// <para>
/// A declared language is a folder of models, not an assembly, and a browser has no file system to read it from. The
/// folder therefore travels as embedded resources, which are written into the runtime's in-memory file system once -
/// the same read-only-to-writable move a desktop host makes when it copies examples beside its executable - and
/// <see cref="LanguageProject.Open(string, ILanguageResolver, out string?)"/> loads them exactly as it loads a folder
/// on disk. No lowering is reimplemented here; the sample only supplies the storage.
/// </para>
/// <para>
/// A sample that stands on another (<c>declared-invoice</c> extends Units) is resolved through a resolver over the
/// extracted folders, so opening it opens what it stands on. The merged registries are then what a document is
/// projected with, because a dependent language's notation is spread over its whole chain.
/// </para>
/// </remarks>
internal static class DeclaredSamples
{
    private const string ResourcePrefix = "Examples/";

    private static IReadOnlyList<WebLanguage>? _languages;

    /// <summary>Every declared sample that loaded, in folder order.</summary>
    internal static IReadOnlyList<WebLanguage> All => _languages ??= Load();

    private static List<WebLanguage> Load()
    {
        var root = Path.Combine(Path.GetTempPath(), "projectional-web", "examples");

        try
        {
            Extract(root);
        }
        catch (IOException)
        {
            // A read-only temporary folder is a reason no declared sample is offered, not a reason the page fails:
            // the code-declared languages still work.
            return [];
        }

        var folders = Directory
            .EnumerateDirectories(root)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var byName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var folder in folders)
        {
            if (NameOf(folder) is { } name && !byName.ContainsKey(name))
            {
                byName[name] = folder;
            }
        }

        var resolver = new FolderResolver(byName);
        List<WebLanguage> languages = [];

        foreach (var folder in folders)
        {
            if (resolver.Open(folder) is { } project && LanguageOf(project) is { } language)
            {
                languages.Add(language);
            }
        }

        return languages;
    }

    /// <summary>Writes every embedded sample file into the runtime's file system.</summary>
    private static void Extract(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);

        var assembly = typeof(DeclaredSamples).Assembly;

        foreach (var resource in assembly.GetManifestResourceNames().OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // The manifest name was written with the build machine's separator; a path is not allowed to remember it.
            var relative = resource[ResourcePrefix.Length..].Replace('\\', '/');
            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var input = assembly.GetManifestResourceStream(resource);

            if (input is null)
            {
                continue;
            }

            using var output = File.Create(target);
            input.CopyTo(output);
        }
    }

    /// <summary>The name a folder's <c>language.mps</c> gives it, or <see langword="null"/> when it has none.</summary>
    private static string? NameOf(string folder)
    {
        var file = Path.Combine(folder, LanguageProject.LanguageFile);

        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var model = ModelFile.Load(new Project(StructureLanguage.Concepts), file);

            return model.Roots.Count == 1 ? StructureLowering.ReadLanguage(model.Roots[0]).Name : null;
        }
        catch (ModelFormatException)
        {
            return null;
        }
    }

    private static WebLanguage? LanguageOf(LanguageProject project)
    {
        var chain = ChainOf(project);

        ConceptRegistry concepts;

        try
        {
            concepts = ConceptRegistry.Create([.. chain.SelectMany(language => language.Declarations)]);
        }
        catch (ConceptDefinitionException)
        {
            return null;
        }

        var notations = new NotationRegistry(concepts);

        foreach (var notation in chain.SelectMany(language => language.Notations.Notations))
        {
            // A dependent language may restate a base's notation; the later one wins and the registry refuses a
            // duplicate, so the chain is merged the same way the desktop set merges it.
            if (!notations.TryGet(notation.ConceptKey, notation.Variant, out _))
            {
                notations.Add(notation);
            }
        }

        var constraints = chain.Aggregate(ConstraintSet.Empty, (all, language) => all.With(language.Constraints));
        var types = chain.Aggregate(TypeRegistry.Empty, (all, language) => all.With(language.Types));
        var scopes = chain.Aggregate(ScopeProviderRegistry.Empty, (all, language) => all.With(language.Scopes));

        var sample = SampleModel(project, new Project(concepts));
        var sampleXml = sample is null ? null : ModelXml.Serialize(sample);
        var rootKey = sample?.Roots[0].Concept.Key
            ?? concepts.Concepts.FirstOrDefault(concept => concept.IsRoot && !concept.IsAbstract)?.Key;

        if (rootKey is null)
        {
            return null;
        }

        Func<Model> empty = () =>
        {
            var fresh = new Project(concepts);
            var model = fresh.NewModel(project.Name.ToLowerInvariant());
            model.AddRoot(model.NewNode(rootKey));

            return model;
        };

        return new WebLanguage
        {
            Name = project.Name,
            Concepts = concepts,
            Notations = notations,
            Typing = project.HasTyping ? project.Typing : null,
            Types = types,
            Constraints = constraints,
            Scopes = scopes,
            Intentions = project.HasIntentions ? [project.Intentions] : [],
            NewEmpty = empty,
            NewSample = sampleXml is null ? empty : () => ModelXml.Deserialize(new Project(concepts), sampleXml),
        };
    }

    /// <summary>A language and everything it stands on, bases first.</summary>
    private static List<LanguageProject> ChainOf(LanguageProject project)
    {
        List<LanguageProject> chain = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        Add(project);

        return chain;

        void Add(LanguageProject language)
        {
            if (!seen.Add(language.Directory))
            {
                return;
            }

            foreach (var inherited in language.Inherited)
            {
                Add(inherited);
            }

            foreach (var resolved in language.Resolved)
            {
                Add(resolved);
            }

            chain.Add(language);
        }
    }

    /// <summary>A document the language ships: one saved beside <c>language.mps</c>, or an accessory model.</summary>
    private static Model? SampleModel(LanguageProject project, Project merged)
    {
        foreach (var file in Directory.EnumerateFiles(project.Directory, "*.mps").OrderBy(file => file, StringComparer.Ordinal))
        {
            if (string.Equals(Path.GetFileName(file), LanguageProject.LanguageFile, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                return ModelFile.Load(merged, file);
            }
            catch (ModelFormatException)
            {
                // The folder may hold a document the merged vocabulary cannot read; the next one may work.
            }
        }

        return project.Accessory.Count > 0 ? project.Accessory[0] : null;
    }

    /// <summary>Opens a folder, answering declared dependencies from the other extracted folders.</summary>
    private sealed class FolderResolver(IReadOnlyDictionary<string, string> folders) : ILanguageResolver
    {
        private readonly Dictionary<string, LanguageProject?> _opened = new(StringComparer.OrdinalIgnoreCase);

        public LanguageProject? Find(string name) =>
            folders.TryGetValue(name, out var folder) ? Open(folder) : null;

        internal LanguageProject? Open(string folder)
        {
            if (_opened.TryGetValue(folder, out var cached))
            {
                return cached;
            }

            // Set before opening, so a declared cycle resolves to "not available" instead of recursing forever.
            _opened[folder] = null;

            var project = LanguageProject.Open(folder, this, out _);
            _opened[folder] = project;

            return project;
        }
    }
}
