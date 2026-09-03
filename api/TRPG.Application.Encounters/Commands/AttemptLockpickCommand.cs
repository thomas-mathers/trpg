using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Encounters;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Mappers;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public enum LockpickAttemptOutcome
{
    NothingToPick,
    Failed,
    Opened,
}

public record AttemptLockpickResult(
    LockpickAttemptOutcome Outcome,
    GuardEncounter? GuardEncounter = null,
    HostileEncounter? HostileEncounter = null
);

public class AttemptLockpickCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required Guid DestinationLocationId { get; init; }
}

internal class AttemptLockpickCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    SkillCheckService skillCheckService,
    ICommandHandler<SetDoorConnectorLockedCommand> setDoorConnectorLocked,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    ICommandHandler<AddBreakingAndEnteringCrimesCommand> addBreakingAndEnteringCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    ICommandHandler<
        EvaluateTrespassingEncounterCommand,
        HostileEncounter?
    > evaluateTrespassingEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IOptionsMonitor<LockpickingOptions> lockpickingOptions,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<AttemptLockpickCommand, AttemptLockpickResult>
{
    private const int RecentOffenseLimit = 3;

    public async Task<AttemptLockpickResult> Handle(
        AttemptLockpickCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var doorsByConnectorId = await getDoorConnectorsByConnectorIds.Handle(
            new GetDoorConnectorsByConnectorIdsQuery { ConnectorIds = [command.ConnectorId] },
            cancellationToken
        );
        if (!doorsByConnectorId.TryGetValue(command.ConnectorId, out var door) || !door.IsLocked)
        {
            transaction.Complete();
            return new AttemptLockpickResult(LockpickAttemptOutcome.NothingToPick);
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var opened = await skillCheckService.Roll(
            player!.Id,
            Skill.Lockpicking,
            LockpickingChanceCalculator.BuildLockOpenCurve(
                door.LockLevel,
                lockpickingOptions.CurrentValue
            ),
            cancellationToken
        );

        if (opened)
        {
            await setDoorConnectorLocked.Handle(
                new SetDoorConnectorLockedCommand
                {
                    ConnectorId = command.ConnectorId,
                    IsLocked = false,
                },
                cancellationToken
            );
            await adjustCreatureSkills.Handle(
                new AdjustCreatureSkillsCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = player.Id,
                    UsageCounts = new Dictionary<Skill, int> { [Skill.Lockpicking] = 1 },
                },
                cancellationToken
            );
        }

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );

        GuardEncounter? guardEncounter = null;
        HostileEncounter? hostileEncounter = null;

        if (currentLocation!.RoomId == null)
        {
            guardEncounter = await EvaluateExteriorDetection(
                command,
                player,
                currentLocation,
                cancellationToken
            );
        }
        else
        {
            hostileEncounter = await evaluateTrespassingEncounter.Handle(
                new EvaluateTrespassingEncounterCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.Id,
                },
                cancellationToken
            );
        }

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = player.Id,
                Encounter = guardEncounter ?? (Encounter?)hostileEncounter,
            },
            cancellationToken
        );

        transaction.Complete();

        return new AttemptLockpickResult(
            opened ? LockpickAttemptOutcome.Opened : LockpickAttemptOutcome.Failed,
            guardEncounter,
            hostileEncounter
        );
    }

    private async Task<GuardEncounter?> EvaluateExteriorDetection(
        AttemptLockpickCommand command,
        Creature player,
        Location currentLocation,
        CancellationToken cancellationToken
    )
    {
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

        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.DestinationLocationId },
            cancellationToken
        );

        var cityFactionId =
            await getCityFactionForCreature.Handle(
                new GetCityFactionForCreatureQuery { CreatureId = guard.Id },
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Guard {guard.Id} has no city faction membership."
            );

        var crime = new BreakingAndEnteringCrime
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            BuildingId = building?.Id ?? Guid.Empty,
            BuildingName = building?.Name ?? "",
            OwnerFactionId = cityFactionId,
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
                WitnessCreatureIds = [guard.Id],
            },
            cancellationToken
        );

        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = player.Id,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = player.Id,
                Targets = [new ReputationLogTarget(cityFactionId, ReputationTargetType.Faction)],
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        var options = guardEncounterOptions.CurrentValue;
        var encounter = new GuardEncounter
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            LocationName = currentLocation.Name,
            GuardCreatureId = guard.Id,
            CityFactionId = cityFactionId,
            GuardName = guard.Name,
            ReputationScore = score,
            FineAmount = GuardEncounterCalculator.ComputeFineGold(score, options),
            JailHours = GuardEncounterCalculator.ComputeJailHours(score, options),
            RecentOffenses = recentOffenses
                .Select(entry => entry.Detail ?? entry.Reason.ToDisplayText())
                .ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
