using System.Text.Json.Serialization;

namespace TRPG.Application.Quests.Results;

public record QuestConversationObjectiveResult(
    string Name,
    string Description,
    int RequiredAmount,
    IReadOnlyCollection<string>? ItemNames = null
);

public record QuestConversationResult(
    [property: JsonIgnore] Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestConversationObjectiveResult> Objectives
);

public record QuestInteractionsResult(
    IReadOnlyCollection<QuestConversationResult> AvailableQuests,
    IReadOnlyCollection<QuestConversationResult> ActiveQuests,
    IReadOnlyCollection<QuestConversationResult> ReadyToCompleteQuests,
    IReadOnlyCollection<QuestConversationResult> CompletedQuests
);
