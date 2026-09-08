using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateJailbreakEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

// An escapee is caught by walking past a jailer, not at the moment the lock clicks: the room
// through the door has not been simulated yet, so asking who is in it there reads an empty room.
internal class EvaluateJailbreakEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetUnsettledJailbreakQuery, UnsettledJailbreak?> getUnsettledJailbreak,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    ICommandHandler<CreateGuardEncounterCommand, GuardEncounter> createGuardEncounter,
    ILogger<EvaluateJailbreakEncounterCommandHandler> logger
) : ICommandHandler<EvaluateJailbreakEncounterCommand, GuardEncounter?>
{
    public async Task<GuardEncounter?> Handle(
        EvaluateJailbreakEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var jailbreak = await getUnsettledJailbreak.Handle(
            new GetUnsettledJailbreakQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );
        if (jailbreak == null)
        {
            return null;
        }

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
            logger.LogDebug(
                "[jailbreak] skipped: no guard at location {LocationId}",
                player.LocationId
            );
            return null;
        }

        var cityFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = guard.Id },
            cancellationToken
        );
        if (cityFactionId is not { } cityFaction)
        {
            logger.LogDebug("[jailbreak] skipped: guard {GuardId} has no city faction", guard.Id);
            return null;
        }

        logger.LogInformation(
            "[jailbreak] {GuardName} catches the escapee from {BuildingName}",
            guard.Name,
            jailbreak.BuildingName
        );

        // Seeing the escapee is what reports the escape, and the crime is still open to hear it
        // because it is anchored at the jail rather than the cell.
        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                CrimeIds = [jailbreak.CrimeId],
                WitnessCreatureIds = [guard.Id],
            },
            cancellationToken
        );

        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFaction,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );

        return await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = player.LocationId,
                LocationName = location?.Name ?? "",
                GuardCreatureId = guard.Id,
                GuardName = guard.Name,
                CityFactionId = cityFaction,
                ReputationScore = score,
                TriggeringCrimeId = jailbreak.CrimeId,
            },
            cancellationToken
        );
    }
}
