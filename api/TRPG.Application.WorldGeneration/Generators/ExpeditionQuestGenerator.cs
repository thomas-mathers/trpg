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

        var objective = new GiveItemsObjective
        {
            WorldId = expedition.WorldId,
            QuestId = quest.Id,
            Name = $"Recover {expedition.CompanionName}'s journal",
            Description =
                $"Recover {expedition.CompanionName}'s journal, then bring it to {expedition.SurvivorName}.",
            ItemIds = [expedition.JournalItemId],
            RecipientId = expedition.SurvivorId,
            LocationId = expedition.EntranceLocationId,
        };

        return new QuestGeneratorResult([quest], [objective]);
    }
}
