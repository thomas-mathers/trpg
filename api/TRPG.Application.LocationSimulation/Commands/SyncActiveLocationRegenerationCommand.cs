using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncActiveLocationRegenerationCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveLocationRegenerationCommandHandler(
    IQueryHandler<
        GetActiveFightCombatantIdsByWorldQuery,
        IReadOnlyCollection<Guid>
    > getActiveFightCombatantIds,
    ICommandHandler<
        RegenerateCreaturesAtLocationCommand,
        IReadOnlyCollection<CreatureVitals>
    > regenerateCreatures,
    IGameClientEventSink gameEvents
) : ICommandHandler<SyncActiveLocationRegenerationCommand>
{
    public async Task Handle(
        SyncActiveLocationRegenerationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // A fight advances only through submitted actions, so waiting inside one must not heal anyone.
        var fightCombatantIds = await getActiveFightCombatantIds.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = command.WorldId },
            cancellationToken
        );

        foreach (var locationId in command.Players.Select(player => player.LocationId).Distinct())
        {
            var changedVitals = await regenerateCreatures.Handle(
                new RegenerateCreaturesAtLocationCommand
                {
                    WorldId = command.WorldId,
                    LocationId = locationId,
                    GameTime = command.GameTime,
                    ExcludedCreatureIds = fightCombatantIds,
                },
                cancellationToken
            );

            PublishPlayerVitals(command, locationId, changedVitals);
        }
    }

    private void PublishPlayerVitals(
        SyncActiveLocationRegenerationCommand command,
        Guid locationId,
        IReadOnlyCollection<CreatureVitals> changedVitals
    )
    {
        var playerIds = command
            .Players.Where(player => player.LocationId == locationId)
            .Select(player => player.PlayerId)
            .ToHashSet();

        foreach (var vitals in changedVitals.Where(vitals => playerIds.Contains(vitals.CreatureId)))
        {
            gameEvents.Enqueue(new PlayerVitalsChangedEvent(vitals, command.GameTime));
        }
    }
}
