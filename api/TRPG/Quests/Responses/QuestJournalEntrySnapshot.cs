namespace TRPG.Quests.Responses;

public record QuestObjectiveItemProgressSnapshot(string Name, int Amount, int RequiredAmount);

public record QuestObjectiveProgressSnapshot(
    string Name,
    string Description,
    int Amount,
    int RequiredAmount,
    string? LocationName,
    IReadOnlyCollection<QuestObjectiveItemProgressSnapshot>? Items
);

public record QuestJournalEntrySnapshot(
    Guid Id,
    string Name,
    string Description,
    string? GiverName,
    int GoldReward,
    string Status,
    bool IsTracked,
    IReadOnlyCollection<QuestObjectiveProgressSnapshot> Objectives
);
