using TRPG.Data.Models;

namespace TRPG.Application.Quests;

internal sealed record CreatureKilledQuestEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
);

internal sealed class CreatureKilledQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
{
    public Task Handle(
        CreatureKilledQuestEvent questEvent,
        CancellationToken cancellationToken = default
    ) =>
        questObjectiveAdvancer.Advance(
            questEvent.PlayerId,
            questEvent.WorldId,
            objective =>
                objective switch
                {
                    KillCreatureObjective kill => kill.CreatureId == questEvent.CreatureId,
                    KillCreatureTypeObjective kill => kill.CreatureType == questEvent.CreatureType,
                    _ => false,
                },
            cancellationToken
        );
}
