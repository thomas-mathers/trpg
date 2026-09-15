using TRPG.Application.Quests.Queries;
using TRPG.Quests.Responses;

namespace TRPG.Quests.Mappers;

internal static class QuestJournalEntryMapper
{
    public static QuestJournalEntrySnapshot ToSnapshot(this QuestJournalEntry entry) =>
        new(
            entry.Id,
            entry.Name,
            entry.Description,
            entry.GiverName,
            entry.GoldReward,
            entry.Status.ToString(),
            entry.IsTracked,
            entry.Objectives.Select(objective => objective.ToSnapshot()).ToArray()
        );
}
