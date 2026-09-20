using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainCastingGeneratorTests
{
    [Fact]
    public void Cast_UsesOneEligibleFactionRoster_WhenChainHasAtLeastSixteenNodes()
    {
        var giverFactionId = Guid.NewGuid();
        var antagonistFactionId = Guid.NewGuid();
        var firstGiver = Candidate("First Giver", giverFactionId, FactionKind.Joinable);
        var secondGiver = Candidate("Second Giver", giverFactionId, FactionKind.Joinable);
        var antagonist = Candidate(
            "Cast Antagonist",
            antagonistFactionId,
            FactionKind.Antagonist,
            canGiveQuests: false
        );
        var input = Input(
            [firstGiver, secondGiver, antagonist],
            [new QuestChainFactionStanding(giverFactionId, antagonistFactionId, -40)]
        );
        var generator = new QuestChainCastingGenerator(new Random(42));

        var cast = generator.Cast(Skeleton(16), input);

        Assert.Equal(giverFactionId, cast.GiverFactionId);
        Assert.All(
            cast.GiverIdByNodeId.Values,
            giverId => Assert.Contains(giverId, new[] { firstGiver.Id, secondGiver.Id })
        );
        Assert.Equal(antagonistFactionId, cast.AntagonistFactionId);
        Assert.Equal([antagonist.Id], cast.AntagonistTargetEntityIds);
    }

    [Fact]
    public void Cast_NeverSelectsAnAntagonistTargetAsASingleGiver()
    {
        var giverFactionId = Guid.NewGuid();
        var antagonistFactionId = Guid.NewGuid();
        var giver = Candidate("Only Giver", giverFactionId, FactionKind.Joinable);
        var antagonist = Candidate(
            "Not A Giver",
            antagonistFactionId,
            FactionKind.Antagonist,
            canGiveQuests: false
        );
        var input = Input(
            [giver, antagonist],
            [new QuestChainFactionStanding(giverFactionId, antagonistFactionId, -20)]
        );
        var generator = new QuestChainCastingGenerator(new FixedRandom(0));

        var cast = generator.Cast(Skeleton(4), input);

        Assert.Null(cast.GiverFactionId);
        Assert.All(cast.GiverIdByNodeId.Values, giverId => Assert.Equal(giver.Id, giverId));
    }

    private static QuestChainGeneratorInput Input(
        IReadOnlyList<QuestChainCandidateEntity> entities,
        IReadOnlyList<QuestChainFactionStanding> standings
    ) =>
        new()
        {
            ChainPremise = "Test premise",
            MinimumChainLength = 1,
            MaximumChainLength = 20,
            AvailableEntities = entities,
            FactionStandings = standings,
        };

    private static QuestChainCandidateEntity Candidate(
        string name,
        Guid factionId,
        FactionKind factionKind,
        bool canGiveQuests = true
    ) =>
        new(
            Guid.NewGuid(),
            name,
            QuestChainEntityTypes.Creature,
            FactionId: factionId,
            FactionName: $"{name} Faction",
            FactionKind: factionKind,
            CanGiveQuests: canGiveQuests
        );

    private static IReadOnlyList<QuestChainNodeSkeleton> Skeleton(int count) =>
        Enumerable
            .Range(1, count)
            .Select(index => new QuestChainNodeSkeleton(
                $"node-{index}",
                index == 1 ? [] : [$"node-{index - 1}"],
                null,
                null,
                null,
                index == count ? QuestChainBlockType.Finale : QuestChainBlockType.Investigation
            ))
            .ToArray();
}
