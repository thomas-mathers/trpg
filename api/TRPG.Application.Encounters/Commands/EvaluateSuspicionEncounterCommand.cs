using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateSuspicionEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateSuspicionEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<CreateSuspicionEncounterCommand, SuspicionEncounter> createSuspicionEncounter,
    SneakDetectionService sneakDetectionService,
    IOptionsMonitor<SuspicionOptions> suspicionOptions
) : ICommandHandler<EvaluateSuspicionEncounterCommand, SuspicionEncounter?>
{
    public async Task<SuspicionEncounter?> Handle(
        EvaluateSuspicionEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (!player!.IsSneaking)
        {
            return null;
        }

        var guard = await getGuardAtLocation.Handle(
            new GetGuardAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );
        if (guard == null)
        {
            return null;
        }

        var curve = SuspicionDetectionChanceCalculator.BuildCurve(suspicionOptions.CurrentValue);
        var isDetected = await sneakDetectionService.RollDetection(
            command.WorldId,
            command.PlayerId,
            player.IsSneaking,
            curve,
            cancellationToken
        );
        if (!isDetected)
        {
            return null;
        }

        var cityFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = guard.Id },
            cancellationToken
        );
        if (cityFactionId == null)
        {
            throw new InvalidOperationException(
                $"Guard {guard.Id} has no city faction membership."
            );
        }

        var location =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = player.LocationId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Location {player.LocationId} not found.");

        return await createSuspicionEncounter.Handle(
            new CreateSuspicionEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = player.LocationId,
                LocationName = location.Name,
                GuardCreatureId = guard.Id,
                GuardName = guard.Name,
                CityFactionId = cityFactionId.Value,
                Cause = SuspicionCause.Sneaking,
            },
            cancellationToken
        );
    }
}
