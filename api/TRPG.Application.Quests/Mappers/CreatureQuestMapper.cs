using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Mappers;

internal static class CreatureQuestMapper
{
    public static QuestJournalEntry ToJournalEntry(
        this CreatureQuest quest,
        string? giverName,
        IReadOnlyCollection<QuestObjectiveProgress> objectives
    ) =>
        new(
            quest.QuestId,
            quest.Quest.Name,
            quest.Quest.Description,
            giverName,
            quest.Quest.GoldReward,
            quest.Status,
            quest.IsTracked,
            objectives
        );
}
