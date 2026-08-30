using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal sealed record TheftSource(
    Creature Owner,
    Guid LocationId,
    Skill Skill,
    bool IsPickpocketing,
    Guid? WorkstationOccupantId
);

internal sealed record TheftWitness(Guid Id, string Name);

internal class TheftSourceResolver(TrpgDbContext context)
{
    public async Task<TheftSource?> Resolve(
        ItemOwnerReference from,
        Guid worldId,
        CancellationToken cancellationToken
    ) =>
        from.Type switch
        {
            OwnerType.Creature => await GetCreatureTheftSource(from, worldId, cancellationToken),
            OwnerType.Container => await GetContainerTheftSource(from, worldId, cancellationToken),
            OwnerType.Workstation => await GetWorkstationTheftSource(
                from,
                worldId,
                cancellationToken
            ),
            _ => throw new InvalidOperationException(
                $"Owner type {from.Type} is not valid for theft."
            ),
        };

    public async Task<TheftWitness[]> GetLiveWitnesses(
        Guid worldId,
        Guid locationId,
        Guid excludeCreatureId,
        CancellationToken cancellationToken
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == worldId
                && creature.LocationId == locationId
                && creature.State != CreatureState.Dead
                && creature.State != CreatureState.Sleeping
                && creature.Id != excludeCreatureId
                && CreatureTypes.Humanoid.AsEnumerable().Contains(creature.CreatureType)
            )
            .Select(creature => new TheftWitness(creature.Id, creature.Name))
            .ToArrayAsync(cancellationToken);

    public async Task<int> GetEquippedItemCount(
        Guid worldId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                item.WorldId == worldId
                && itemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.EquippedSlot != null
            )
            .CountAsync(cancellationToken);

    private async Task<TheftSource?> GetCreatureTheftSource(
        ItemOwnerReference from,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var owner =
            await context.Creatures.FirstOrDefaultAsync(
                creature => creature.Id == from.Id && creature.WorldId == worldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), from.Id);

        return owner.State == CreatureState.Dead
            ? null
            : new TheftSource(
                Owner: owner,
                LocationId: owner.LocationId,
                Skill: Skill.Pickpocketing,
                IsPickpocketing: true,
                WorkstationOccupantId: null
            );
    }

    private async Task<TheftSource?> GetContainerTheftSource(
        ItemOwnerReference from,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var prop =
            await context.Props.FirstOrDefaultAsync(
                candidate => candidate.Id == from.Id && candidate.WorldId == worldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Prop), from.Id);

        if (prop is not Container container)
        {
            throw new InvalidOperationException(
                $"Owner type {from.Type} does not match prop type {prop.GetType().Name}."
            );
        }

        return await GetOwnedPropTheftSource(
            container,
            worldId,
            workstationOccupantId: null,
            cancellationToken
        );
    }

    private async Task<TheftSource?> GetWorkstationTheftSource(
        ItemOwnerReference from,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var prop =
            await context.Props.FirstOrDefaultAsync(
                candidate => candidate.Id == from.Id && candidate.WorldId == worldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Prop), from.Id);

        if (prop is not Workstation workstation)
        {
            throw new InvalidOperationException(
                $"Owner type {from.Type} does not match prop type {prop.GetType().Name}."
            );
        }

        return await GetOwnedPropTheftSource(
            workstation,
            worldId,
            workstation.OccupantId,
            cancellationToken
        );
    }

    private async Task<TheftSource?> GetOwnedPropTheftSource(
        Prop prop,
        Guid worldId,
        Guid? workstationOccupantId,
        CancellationToken cancellationToken
    )
    {
        if (prop.OwnerCreatureId is not { } ownerId)
        {
            return null;
        }

        var sourceOwner =
            await context.Creatures.FirstOrDefaultAsync(
                creature => creature.Id == ownerId && creature.WorldId == worldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), ownerId);

        return new TheftSource(
            Owner: sourceOwner,
            LocationId: prop.LocationId,
            Skill: Skill.Sneak,
            IsPickpocketing: false,
            WorkstationOccupantId: workstationOccupantId
        );
    }
}
