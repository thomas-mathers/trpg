using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateTrapEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateTrapEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetUnresolvedTrapByLocationIdQuery, Trigger?> getUnresolvedTrapByLocationId,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<CreateTrapEncounterCommand, TrapEncounter> createTrapEncounter,
    ICommandHandler<RecordTrapDiscoveryCommand> recordTrapDiscovery
) : ICommandHandler<EvaluateTrapEncounterCommand, TrapEncounter?>
{
    public async Task<TrapEncounter?> Handle(
        EvaluateTrapEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player == null)
        {
            return null;
        }

        var trigger = await getUnresolvedTrapByLocationId.Handle(
            new GetUnresolvedTrapByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        if (trigger == null)
        {
            return null;
        }

        await recordTrapDiscovery.Handle(
            new RecordTrapDiscoveryCommand
            {
                WorldId = command.WorldId,
                KnowerId = command.PlayerId,
                TriggerId = trigger.Id,
            },
            cancellationToken
        );

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );
        var targetLocation =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = trigger.TargetId!.Value },
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Trap target location {trigger.TargetId} not found."
            );

        return await createTrapEncounter.Handle(
            new CreateTrapEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = player.LocationId,
                LocationName = location?.Name,
                TriggerId = trigger.Id,
                TrapKind = trigger.TrapKind!.Value,
                TargetLocationId = trigger.TargetId!.Value,
                TargetLocationName = targetLocation.Name,
            },
            cancellationToken
        );
    }
}
