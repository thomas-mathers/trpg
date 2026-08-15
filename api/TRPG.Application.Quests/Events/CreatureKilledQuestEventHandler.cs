using TRPG.Data.Models;

namespace TRPG.Application.Quests.Events;

public sealed record CreatureKilledQuestEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
);

public sealed class CreatureKilledQuestEventHandler(QuestObjectiveAdvancer questObjectiveAdvancer)
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
