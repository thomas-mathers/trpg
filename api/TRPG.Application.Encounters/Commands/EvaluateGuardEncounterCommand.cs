using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateGuardEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateGuardEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<CreateGuardEncounterCommand, GuardEncounter> createGuardEncounter,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounter?>
{
    public async Task<GuardEncounter?> Handle(
        EvaluateGuardEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var guard = await getGuardAtLocation.Handle(
            new GetGuardAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = player!.LocationId,
            },
            cancellationToken
        );
        if (guard == null)
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

        var options = guardEncounterOptions.CurrentValue;
        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId.Value,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );
        if (score > options.ReputationThreshold)
        {
            return null;
        }

        if (Random.Shared.NextDouble() >= options.EncounterChance)
        {
            return null;
        }

        var location =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = player!.LocationId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Location {player!.LocationId} not found.");

        return await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = player!.LocationId,
                LocationName = location.Name,
                GuardCreatureId = guard.Id,
                GuardName = guard.Name,
                CityFactionId = cityFactionId.Value,
                ReputationScore = score,
            },
            cancellationToken
        );
    }
}
