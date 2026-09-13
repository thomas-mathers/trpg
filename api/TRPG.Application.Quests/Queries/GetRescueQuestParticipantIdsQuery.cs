using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

// Every giver and captive ever used by a FreeCreatureObjective rescue quest, in either role — a
// person gets one missing-relative story, not a revolving door of them.
public class GetRescueQuestParticipantIdsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetRescueQuestParticipantIdsQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetRescueQuestParticipantIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetRescueQuestParticipantIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rescueQuests = await context
            .QuestObjectives.AsNoTracking()
            .OfType<FreeCreatureObjective>()
            .Where(objective => objective.WorldId == query.WorldId)
            .Join(
                context.Quests.AsNoTracking(),
                objective => objective.QuestId,
                quest => quest.Id,
                (objective, quest) => new { objective.CreatureId, quest.GiverId }
            )
            .ToArrayAsync(cancellationToken);

        var participantIds = new HashSet<Guid>();
        foreach (var rescueQuest in rescueQuests)
        {
            participantIds.Add(rescueQuest.CreatureId);
            participantIds.Add(rescueQuest.GiverId);
        }

        return participantIds;
    }
}
