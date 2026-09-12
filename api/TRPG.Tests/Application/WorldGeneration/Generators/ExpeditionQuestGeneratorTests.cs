using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public sealed class ExpeditionQuestGeneratorTests
{
    private readonly DungeonExpedition _expedition = new()
    {
        WorldId = Guid.NewGuid(),
        BuildingId = Guid.NewGuid(),
        SurvivorId = Guid.NewGuid(),
        CompanionId = Guid.NewGuid(),
        EntranceLocationId = Guid.NewGuid(),
        CompanionLocationId = Guid.NewGuid(),
        JournalWorkId = Guid.NewGuid(),
        JournalItemId = Guid.NewGuid(),
        DiscoverySecretId = Guid.NewGuid(),
        SurvivorName = "Alden",
        CompanionName = "Brienne",
    };

    [Fact]
    public void Generate_OffersAQuestFromTheSurvivor_WithAShareSecretObjective()
    {
        // Act
        var result = ExpeditionQuestGenerator.Generate(_expedition);

        // Assert
        var quest = Assert.Single(result.Quests);
        Assert.Equal(_expedition.SurvivorId, quest.GiverId);
        Assert.Equal(_expedition.WorldId, quest.WorldId);
        Assert.True(quest.GoldReward > 0);
        var objective = Assert.IsType<ShareSecretObjective>(Assert.Single(result.Objectives));
        Assert.Equal(quest.Id, objective.QuestId);
        Assert.Equal(_expedition.DiscoverySecretId, objective.SecretId);
        Assert.Equal(_expedition.SurvivorId, objective.RecipientId);
    }

    [Fact]
    public void Generate_RewardsPersonalReputation_WithTheSurvivor()
    {
        // Act
        var result = ExpeditionQuestGenerator.Generate(_expedition);

        // Assert
        var quest = Assert.Single(result.Quests);
        var reward = Assert.Single(quest.ReputationRewards);
        Assert.Equal(_expedition.SurvivorId, reward.TargetId);
        Assert.Equal(ReputationTargetType.Creature, reward.TargetType);
    }
}
