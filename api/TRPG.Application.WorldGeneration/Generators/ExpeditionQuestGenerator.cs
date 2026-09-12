using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// The survivor's own story is the reward for pursuing it: finding out what happened to their
// companion and telling them earns the same gold-and-reputation payoff any other quest does.
public static class ExpeditionQuestGenerator
{
    private const int GoldReward = 75;
    private const int SurvivorReputationReward = 20;

    public static QuestGeneratorResult Generate(DungeonExpedition expedition)
    {
        var quest = new Quest
        {
            WorldId = expedition.WorldId,
            GiverId = expedition.SurvivorId,
            Name = $"What Happened to {expedition.CompanionName}",
            Description =
                $"Find out what became of {expedition.CompanionName} and tell {expedition.SurvivorName}.",
            GoldReward = GoldReward,
        };
        quest.ReputationRewards.Add(
            new QuestReputationReward
            {
                WorldId = expedition.WorldId,
                QuestId = quest.Id,
                TargetId = expedition.SurvivorId,
                TargetType = ReputationTargetType.Creature,
                Score = SurvivorReputationReward,
            }
        );

        var objective = new ShareSecretObjective
        {
            WorldId = expedition.WorldId,
            QuestId = quest.Id,
            Name = $"Tell {expedition.SurvivorName} what happened to {expedition.CompanionName}",
            Description =
                $"Share the account of {expedition.CompanionName}'s final days with {expedition.SurvivorName}.",
            SecretId = expedition.DiscoverySecretId,
            RecipientId = expedition.SurvivorId,
            LocationId = expedition.EntranceLocationId,
        };

        return new QuestGeneratorResult([quest], [objective]);
    }
}
