namespace TRPG.Contracts.Quests.Responses;

public record QuestObjectiveProgressSnapshot(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount,
    string? LocationName
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
