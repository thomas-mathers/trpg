using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateTrespassingEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateTrespassingEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetBuildingOwnersByBuildingIdQuery,
        IReadOnlyCollection<BuildingOwner>
    > getBuildingOwnersByBuildingId,
    IQueryHandler<HasPlayerBrokenIntoBuildingQuery, bool> hasPlayerBrokenIntoBuilding,
    IQueryHandler<GetFrontDoorLockedByBuildingIdQuery, bool?> getFrontDoorLockedByBuildingId,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
    SkillCheckService skillCheckService,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    ICommandHandler<AddBreakingAndEnteringCrimesCommand> addBreakingAndEnteringCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    ICommandHandler<CreateHostileEncounterCommand, HostileEncounter> createHostileEncounter,
    IOptionsMonitor<LockpickingOptions> lockpickingOptions
) : ICommandHandler<EvaluateTrespassingEncounterCommand, HostileEncounter?>
{
    public async Task<HostileEncounter?> Handle(
        EvaluateTrespassingEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = player!.LocationId },
            cancellationToken
        );
        if (building == null)
        {
            return null;
        }

        var owners = await getBuildingOwnersByBuildingId.Handle(
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );
        if (owners.Any(owner => owner.OwnerId == player.Id))
        {
            return null;
        }

        var hasBrokenIn = await hasPlayerBrokenIntoBuilding.Handle(
            new HasPlayerBrokenIntoBuildingQuery { PlayerId = player.Id, BuildingId = building.Id },
            cancellationToken
        );
        if (!hasBrokenIn)
        {
            return null;
        }

        var isFrontDoorLocked = await getFrontDoorLockedByBuildingId.Handle(
            new GetFrontDoorLockedByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );
        if (isFrontDoorLocked != true)
        {
            return null;
        }

        var witnesses = await getLiveHumanoidWitnessesAtLocation.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = player.LocationId,
                ExcludeCreatureId = player.Id,
            },
            cancellationToken
        );
        if (witnesses.Count == 0)
        {
            return null;
        }

        var isDetected = await skillCheckService.Roll(
            player.Id,
            Skill.Sneak,
            LockpickingChanceCalculator.BuildDetectionCurve(lockpickingOptions.CurrentValue),
            cancellationToken
        );
        if (!isDetected)
        {
            return null;
        }

        var confrontingOccupant = witnesses.First();

        var crime = new BreakingAndEnteringCrime
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            BuildingId = building.Id,
            BuildingName = building.Name,
            OwnerFactionId = await getCityFactionForCreature.Handle(
                new GetCityFactionForCreatureQuery { CreatureId = confrontingOccupant.Id },
                cancellationToken
            ),
        };
        await addBreakingAndEnteringCrimes.Handle(
            new AddBreakingAndEnteringCrimesCommand { Crimes = [crime] },
            cancellationToken
        );
        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                CrimeIds = [crime.Id],
                WitnessCreatureIds = [confrontingOccupant.Id],
            },
            cancellationToken
        );

        if (crime.OwnerFactionId is not { } factionId)
        {
            return null;
        }

        var factionsById = await getFactionsByIds.Handle(
            new GetFactionsByIdsQuery { Ids = [factionId] },
            cancellationToken
        );
        if (!factionsById.TryGetValue(factionId, out var faction))
        {
            return null;
        }

        var occupant = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = confrontingOccupant.Id },
            cancellationToken
        );
        if (occupant == null)
        {
            return null;
        }

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );

        return await createHostileEncounter.Handle(
            new CreateHostileEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                PlayerLocationId = player.LocationId,
                LocationName = location?.Name,
                FactionId = faction.Id,
                FactionName = faction.Name,
                Members =
                [
                    new HostileEncounterMemberSnapshot(
                        occupant.Id,
                        occupant.Name,
                        occupant.CreatureType,
                        occupant.Level
                    ),
                ],
            },
            cancellationToken
        );
    }
}
