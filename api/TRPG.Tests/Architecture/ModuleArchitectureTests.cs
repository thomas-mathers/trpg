using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TRPG.Tests.Architecture;

public sealed partial class ModuleArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ApiRoot = Path.Combine(RepositoryRoot, "api");

    private static readonly IReadOnlySet<string> PersistenceFreeProjects = new HashSet<string>
    {
        "TRPG.Application.Abilities",
        "TRPG.Application.Combat",
        "TRPG.Application.CreatureFormulas",
        "TRPG.Application.Effects",
        "TRPG.Application.WorldGeneration",
    };

    private static readonly IReadOnlySet<string> ForeignContextAllowlist = new HashSet<string>
    {
        "TRPG.Application.Caravans/Commands/BeginCaravanInteractionCommand.cs:IRoutingDbContext",
        "TRPG.Application.Caravans/Commands/BoardCaravanCommand.cs:IRoutingDbContext",
        "TRPG.Application.Caravans/Commands/EndCaravanInteractionCommand.cs:IRoutingDbContext",
        "TRPG.Application.Caravans/Commands/PurchaseCaravanTicketCommand.cs:IRoutingDbContext",
        "TRPG.Application.LocationSimulation/Commands/SeedLlmQuestChainCommand.cs:IFactionsDbContext",
        "TRPG.Application.Quests/Commands/AcceptQuestCommand.cs:IFactionsDbContext",
        "TRPG.Application.Quests/Commands/CompleteQuestCommand.cs:IFactionsDbContext",
        "TRPG.Application.Quests/Queries/GetQuestInteractionsForGiverQuery.cs:IFactionsDbContext",
        "TRPG.Application.Quests/Queries/GetQuestMarkersForCreaturesQuery.cs:IFactionsDbContext",
    };

    private static readonly IReadOnlySet<string> ConcreteContextAllowlist = new HashSet<string>
    {
        "TRPG.Application.Creatures/Commands/DeleteCreaturesCommand.cs:TrpgDbContext",
        "TRPG.Application.Worlds/Commands/BootstrapWorldCommand.cs:TrpgDbContext",
        "TRPG.Application.Worlds/Commands/DropWorldCommand.cs:TrpgDbContext",
    };

    [Fact]
    public void ProductionProjectGraph_HasNoCycles()
    {
        var projects = LoadProjects();
        var cycles = FindCycles(projects);

        Assert.True(cycles.Count == 0, string.Join(Environment.NewLine, cycles));
    }

    [Fact]
    public void CompiledDependencies_HaveDirectProjectReferences()
    {
        var projects = LoadProjects();
        var missing = projects
            .Values.SelectMany(project => FindMissingReferences(project, projects))
            .Order()
            .ToArray();

        Assert.True(missing.Length == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void ForeignModuleContexts_AreExplicitlyAllowlisted()
    {
        var violations = FindContextUses()
            .Where(use => use.Owner != use.Module)
            .Select(use => use.AllowlistKey)
            .Except(ForeignContextAllowlist)
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ConcreteDataContextUses_AreExplicitlyAllowlisted()
    {
        var violations = FindConcreteContextUses()
            .Except(ConcreteContextAllowlist)
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PersistenceFreeProjects_DoNotReferenceData()
    {
        var projects = LoadProjects();
        var violations = PersistenceFreeProjects
            .Where(name => projects[name].References.Contains("TRPG.Data"))
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static IReadOnlyDictionary<string, ProjectInfo> LoadProjects() =>
        Directory
            .GetFiles(ApiRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains("TRPG.Tests", StringComparison.Ordinal))
            .Select(ProjectInfo.Load)
            .ToDictionary(project => project.Name);

    private static IReadOnlyList<string> FindCycles(
        IReadOnlyDictionary<string, ProjectInfo> projects
    )
    {
        var cycles = new List<string>();
        foreach (var project in projects.Keys)
        {
            FindCycles(project, projects, [], [], cycles);
        }
        return cycles.Distinct().ToArray();
    }

    private static void FindCycles(
        string project,
        IReadOnlyDictionary<string, ProjectInfo> projects,
        HashSet<string> visited,
        List<string> path,
        List<string> cycles
    )
    {
        if (path.Contains(project))
        {
            cycles.Add(
                string.Join(" -> ", path.SkipWhile(name => name != project).Append(project))
            );
            return;
        }
        if (!visited.Add(project) || !projects.TryGetValue(project, out var info))
            return;

        path.Add(project);
        foreach (var reference in info.References)
            FindCycles(reference, projects, visited, path, cycles);
        path.RemoveAt(path.Count - 1);
    }

    private static IEnumerable<string> FindMissingReferences(
        ProjectInfo project,
        IReadOnlyDictionary<string, ProjectInfo> projects
    )
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{project.Name}.dll");
        if (!File.Exists(assemblyPath))
            yield break;

        var declared = project.References;
        foreach (var dependency in Assembly.LoadFrom(assemblyPath).GetReferencedAssemblies())
        {
            if (projects.ContainsKey(dependency.Name!) && !declared.Contains(dependency.Name!))
                yield return $"{project.Name} uses {dependency.Name} without a ProjectReference";
        }
    }

    private static IEnumerable<ContextUse> FindContextUses()
    {
        foreach (var file in ApplicationSourceFiles())
        {
            var module = ModuleName(file);
            var relativePath = RelativeApplicationPath(file);
            foreach (Match match in ModuleContextPattern().Matches(File.ReadAllText(file)))
                yield return new ContextUse(module, match.Groups["owner"].Value, relativePath);
        }
    }

    private static IEnumerable<string> FindConcreteContextUses()
    {
        foreach (var file in ApplicationSourceFiles())
        {
            if (
                File.ReadLines(file)
                    .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    .Any(line => ConcreteContextPattern().IsMatch(line))
            )
                yield return $"{RelativeApplicationPath(file)}:TrpgDbContext";
        }
    }

    private static IEnumerable<string> ApplicationSourceFiles() =>
        Directory
            .GetFiles(ApiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => ModuleDirectoryPattern().IsMatch(path));

    private static string ModuleName(string path) =>
        ModuleDirectoryPattern().Match(path).Groups["module"].Value;

    private static string RelativeApplicationPath(string path) =>
        Path.GetRelativePath(ApiRoot, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory != null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "api", "TRPG.sln")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private sealed record ContextUse(string Module, string Owner, string RelativePath)
    {
        public string AllowlistKey => $"{RelativePath}:I{Owner}DbContext";
    }

    private sealed record ProjectInfo(string Name, IReadOnlySet<string> References)
    {
        public static ProjectInfo Load(string path)
        {
            var document = XDocument.Load(path);
            var references = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>()
                .Select(reference => Path.GetFileNameWithoutExtension(reference.Replace('\\', '/')))
                .ToHashSet();
            return new ProjectInfo(Path.GetFileNameWithoutExtension(path), references);
        }
    }

    [GeneratedRegex(@"TRPG\.Application\.(?<module>[^\\/]+)[\\/]")]
    private static partial Regex ModuleDirectoryPattern();

    [GeneratedRegex(@"\bI(?<owner>[A-Za-z]+)DbContext\b")]
    private static partial Regex ModuleContextPattern();

    [GeneratedRegex(@"(?<![A-Za-z])TrpgDbContext\b")]
    private static partial Regex ConcreteContextPattern();
}
