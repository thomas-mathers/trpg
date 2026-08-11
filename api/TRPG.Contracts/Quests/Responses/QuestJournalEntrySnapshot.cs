namespace TRPG.Contracts.Quests.Responses;

public record QuestObjectiveProgressSnapshot(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount
);

public record QuestJournalEntrySnapshot(
    Guid Id,
    string Name,
    string Description,
    int GoldReward,
    string Status,
    bool IsTracked,
    IReadOnlyCollection<QuestObjectiveProgressSnapshot> Objectives
);
