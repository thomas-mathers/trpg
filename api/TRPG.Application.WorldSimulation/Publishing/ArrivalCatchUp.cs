using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class ArrivalCatchUp(
    IQueryHandler<
        GetActiveFightCombatantIdsByWorldQuery,
        IReadOnlyCollection<Guid>
    > getActiveFightCombatantIds,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    ICommandHandler<
        RegenerateCreaturesAtLocationCommand,
        IReadOnlyCollection<CreatureVitals>
    > regenerateCreatures
)
{
    public async Task Run(
        Guid worldId,
        Guid locationId,
        IReadOnlyCollection<Guid> creatureIds,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var fightCombatantIds = await getActiveFightCombatantIds.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = worldId },
            cancellationToken
        );
        var catchUpIds = creatureIds.Except(fightCombatantIds).ToArray();
        if (catchUpIds.Length == 0)
        {
            return;
        }

        await advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = worldId,
                CreatureIds = catchUpIds,
                GameTime = now,
            },
            cancellationToken
        );
        await regenerateCreatures.Handle(
            new RegenerateCreaturesAtLocationCommand
            {
                WorldId = worldId,
                LocationId = locationId,
                GameTime = now,
                IncludedCreatureIds = catchUpIds,
            },
            cancellationToken
        );
    }
}
