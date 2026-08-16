using System.Text.Json.Serialization;

namespace TRPG.Application.Quests.Results;

public record QuestConversationObjectiveResult(string Name, string Description, int RequiredAmount);

public record QuestConversationResult(
    [property: JsonIgnore] Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestConversationObjectiveResult> Objectives
);

public record QuestInteractionsResult(
    IReadOnlyCollection<QuestConversationResult> AvailableQuests,
    IReadOnlyCollection<QuestConversationResult> ReadyToCompleteQuests
);
