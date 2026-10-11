using System.Text.RegularExpressions;

namespace TRPG.Tests.Architecture;

public sealed partial class WorldWriteGuardArchitectureTests
{
    private static readonly string ApiRoot = FindApiRoot();

    private static readonly IReadOnlySet<string> ScopeEntryAllowlist = new HashSet<string>
    {
        "TRPG.Application.WorldSimulation/WorldSimulationCoordinator.cs",
        "TRPG/Quests/Jobs/GenerateQuestChainJob.cs",
        "TRPG/Worlds/Jobs/CreateWorldJob.cs",
        "TRPG/Worlds/WorldSimulationStartup.cs",
    };

    private static readonly IReadOnlySet<string> ExemptCommandAllowlist = new HashSet<string>
    {
        "TRPG.Application.Books/Commands/EnsureBookPageCommand.cs",
        "TRPG.Application.Chat/Commands/AppendChatMessagesCommand.cs",
        "TRPG.Application.GameSessions/Commands/CreateGameSessionCommand.cs",
        "TRPG.Application.GameTurns/Commands/PublishSessionStateCommand.cs",
        "TRPG.Application.Inventory/Commands/ProposeTradeCommand.cs",
        "TRPG.Application.Narration/Commands/InvalidateWorldLoreAnchorsCommand.cs",
        "TRPG.Application.Worlds/Commands/DropWorldCommand.cs",
        "TRPG.Application.Worlds/Commands/EnsureDungeonPremiseCommand.cs",
        "TRPG.Application.Worlds/Commands/EnsureDungeonPremisesCommand.cs",
        "TRPG/GameSessions/Commands/EndGameSessionCommand.cs",
    };

    [Fact]
    public void WorldWriteScope_IsEnteredOnlyFromAllowlistedFiles()
    {
        var violations = ProductionSourceFiles()
            .Where(file => ScopeEntryPattern().IsMatch(File.ReadAllText(file)))
            .Select(RelativePath)
            .Except(ScopeEntryAllowlist)
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void WorldWriteGuardExemptions_AreExplicitlyAllowlisted()
    {
        var violations = ProductionSourceFiles()
            .Where(file => ExemptionPattern().IsMatch(File.ReadAllText(file)))
            .Select(RelativePath)
            .Except(ExemptCommandAllowlist)
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void WorldWriteGuardExemptions_EachStateAReason()
    {
        var violations = ProductionSourceFiles()
            .Where(file => ExemptionWithoutReasonPattern().IsMatch(File.ReadAllText(file)))
            .Select(RelativePath)
            .Order()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> ProductionSourceFiles() =>
        Directory
            .GetFiles(ApiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ExcludedPathPattern().IsMatch(path))
            .Where(path =>
                !path.EndsWith("ExemptFromWorldWriteGuardAttribute.cs", StringComparison.Ordinal)
            )
            .Where(path => !path.EndsWith("WorldWriteScope.cs", StringComparison.Ordinal));

    private static string RelativePath(string path) =>
        Path.GetRelativePath(ApiRoot, path).Replace('\\', '/');

    private static string FindApiRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory != null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "api", "TRPG.sln")))
                return Path.Combine(directory.FullName, "api");
        }
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    [GeneratedRegex(@"\bWorldWriteScope\.Enter\(")]
    private static partial Regex ScopeEntryPattern();

    [GeneratedRegex(@"\[ExemptFromWorldWriteGuard\(")]
    private static partial Regex ExemptionPattern();

    [GeneratedRegex(@"\[ExemptFromWorldWriteGuard\(\s*(""""|\))")]
    private static partial Regex ExemptionWithoutReasonPattern();

    [GeneratedRegex(@"[\\/](bin|obj|TRPG\.Tests|\.claude)[\\/]")]
    private static partial Regex ExcludedPathPattern();
}
