using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Commands;

public class SyncActiveLocationEffectsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveLocationEffectsCommandHandler(
    IQueryHandler<
        GetActiveFightCombatantIdsByWorldQuery,
        IReadOnlyCollection<Guid>
    > getActiveFightCombatantIds,
    IQueryHandler<
        GetCreatureIdsWithActiveEffectsAtLocationQuery,
        IReadOnlyCollection<Guid>
    > getCreatureIdsWithActiveEffects,
    ICommandHandler<AdvanceFightEffectsCommand> advanceFightEffects,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    PlayerVitalsPublisher playerVitalsPublisher
) : ICommandHandler<SyncActiveLocationEffectsCommand, IReadOnlyCollection<TouchedCreatures>>
{
    public async Task<IReadOnlyCollection<TouchedCreatures>> Handle(
        SyncActiveLocationEffectsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // Read before the fight ticks, so a fight a tick ends is still ticked only once this pass.
        var fightCombatantIds = await getActiveFightCombatantIds.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = command.WorldId },
            cancellationToken
        );

        foreach (var player in command.Players)
        {
            await advanceFightEffects.Handle(
                new AdvanceFightEffectsCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }

        var touched = new List<TouchedCreatures>();
        foreach (var locationId in command.Players.Select(player => player.LocationId).Distinct())
        {
            touched.Add(
                await AdvanceLocationEffects(
                    command,
                    locationId,
                    fightCombatantIds,
                    cancellationToken
                )
            );
        }

        return touched;
    }

    private async Task<TouchedCreatures> AdvanceLocationEffects(
        SyncActiveLocationEffectsCommand command,
        Guid locationId,
        IReadOnlyCollection<Guid> fightCombatantIds,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = await getCreatureIdsWithActiveEffects.Handle(
            new GetCreatureIdsWithActiveEffectsAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = locationId,
                ExcludedCreatureIds = fightCombatantIds,
            },
            cancellationToken
        );
        if (creatureIds.Count == 0)
        {
            return new TouchedCreatures(locationId, []);
        }

        var changedVitals = await advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = command.WorldId,
                CreatureIds = creatureIds,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        await playerVitalsPublisher.Publish(
            command.WorldId,
            command
                .Players.Where(player => player.LocationId == locationId)
                .Select(player => player.PlayerId)
                .ToHashSet(),
            changedVitals,
            cancellationToken
        );

        return new TouchedCreatures(
            locationId,
            changedVitals.Select(vitals => vitals.CreatureId).ToArray()
        );
    }
}
