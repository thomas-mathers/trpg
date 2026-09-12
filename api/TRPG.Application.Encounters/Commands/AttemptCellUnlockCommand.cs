using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public enum CellUnlockOutcome
{
    NothingToUnlock,
    Failed,
    Opened,
}

public record AttemptCellUnlockResult(
    CellUnlockOutcome Outcome,
    HostileEncounter? Encounter = null
);

public class AttemptCellUnlockCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid CellId { get; init; }
}

internal class AttemptCellUnlockCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCellByIdQuery, Cell?> getCellById,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeyItemIdsByOwner,
    SkillCheckService skillCheckService,
    SneakDetectionService sneakDetectionService,
    ICommandHandler<CreateHostileEncounterCommand, HostileEncounter> createHostileEncounter,
    ICommandHandler<UnlockCellCommand> unlockCell,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IDomainEventPublisher<CreatureFreedEvent> creatureFreedEvents,
    IOptionsMonitor<LockpickingOptions> lockpickingOptions
) : ICommandHandler<AttemptCellUnlockCommand, AttemptCellUnlockResult>
{
    public async Task<AttemptCellUnlockResult> Handle(
        AttemptCellUnlockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var cell =
            await getCellById.Handle(
                new GetCellByIdQuery { Id = command.CellId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Cell), command.CellId);

        if (!cell.IsLocked)
        {
            return new AttemptCellUnlockResult(CellUnlockOutcome.NothingToUnlock);
        }

        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        var hasKey =
            cell.KeyItemId != null
            && (
                await getKeyItemIdsByOwner.Handle(
                    new GetKeyItemIdsByOwnerQuery
                    {
                        Owner = new ItemOwnerReference(player.Id, OwnerType.Creature),
                    },
                    cancellationToken
                )
            ).Contains(cell.KeyItemId.Value);

        var opened =
            hasKey
            || await skillCheckService.Roll(
                player.Id,
                Skill.Lockpicking,
                LockpickingChanceCalculator.BuildLockOpenCurve(
                    cell.LockLevel,
                    lockpickingOptions.CurrentValue
                ),
                cancellationToken
            );

        var encounter = hasKey
            ? null
            : await EvaluateGuardDetection(command, cell, player, cancellationToken);

        if (encounter != null)
        {
            return new AttemptCellUnlockResult(CellUnlockOutcome.Failed, encounter);
        }

        if (!opened)
        {
            return new AttemptCellUnlockResult(CellUnlockOutcome.Failed);
        }

        await Free(command, cell, cancellationToken);

        return new AttemptCellUnlockResult(CellUnlockOutcome.Opened);
    }

    // A key holder dealt with the guard already; a lockpick attempt with a living guard nearby
    // risks being noticed, same detection idiom as picking a building's lock.
    private async Task<HostileEncounter?> EvaluateGuardDetection(
        AttemptCellUnlockCommand command,
        Cell cell,
        Creature player,
        CancellationToken cancellationToken
    )
    {
        var groups = await context
            .EncounterGroups.AsNoTracking()
            .Where(group => group.WorldId == command.WorldId && group.LocationId == cell.LocationId)
            .ToArrayAsync(cancellationToken);
        if (groups.Length == 0)
        {
            return null;
        }

        var groupIds = groups.Select(group => group.Id).ToArray();
        var members = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(member => groupIds.AsEnumerable().Contains(member.EncounterGroupId))
            .ToArrayAsync(cancellationToken);

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = members.Select(member => member.CreatureId).ToArray(),
            },
            cancellationToken
        );
        var livingGroup = groups
            .Select(group => new
            {
                Group = group,
                LivingMembers = members
                    .Where(member => member.EncounterGroupId == group.Id)
                    .Select(member => creaturesById.GetValueOrDefault(member.CreatureId))
                    .Where(creature => creature is { State: not CreatureState.Dead })
                    .Select(creature => creature!)
                    .ToArray(),
            })
            .FirstOrDefault(candidate => candidate.LivingMembers.Length > 0);
        if (livingGroup == null)
        {
            return null;
        }

        var isDetected = await sneakDetectionService.RollDetection(
            command.WorldId,
            player.Id,
            player.IsSneaking,
            LockpickingChanceCalculator.BuildDetectionCurve(lockpickingOptions.CurrentValue),
            cancellationToken
        );
        if (!isDetected)
        {
            return null;
        }

        var faction = (
            await getFactionsByIds.Handle(
                new GetFactionsByIdsQuery { Ids = [livingGroup.Group.FactionId] },
                cancellationToken
            )
        )[livingGroup.Group.FactionId];
        var location =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = cell.LocationId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Location {cell.LocationId} not found.");

        return await createHostileEncounter.Handle(
            new CreateHostileEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                PlayerLocationId = cell.LocationId,
                LocationName = location.Name,
                FactionId = faction.Id,
                FactionName = faction.Name,
                Members = livingGroup
                    .LivingMembers.Select(member => new HostileEncounterMemberSnapshot(
                        member.Id,
                        member.Name,
                        member.CreatureType,
                        member.Level
                    ))
                    .ToArray(),
            },
            cancellationToken
        );
    }

    private async Task Free(
        AttemptCellUnlockCommand command,
        Cell cell,
        CancellationToken cancellationToken
    )
    {
        await unlockCell.Handle(new UnlockCellCommand { CellId = cell.Id }, cancellationToken);

        if (cell.CreatureId == null)
        {
            return;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [cell.CreatureId.Value],
                State = CreatureState.Idle,
            },
            cancellationToken
        );

        await creatureFreedEvents.Publish(
            new CreatureFreedEvent(
                PlayerId: command.PlayerId,
                WorldId: command.WorldId,
                CreatureId: cell.CreatureId.Value
            ),
            cancellationToken
        );
    }
}
