using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal sealed record TheftSource(
    Creature Owner,
    Guid LocationId,
    Skill Skill,
    bool IsPickpocketing,
    Guid? WorkstationOccupantId
);

internal class TheftSourceResolver(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById
)
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

    private async Task<TheftSource?> GetCreatureTheftSource(
        ItemOwnerReference from,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var owner = await GetCreatureOrThrow(from.Id, worldId, cancellationToken);

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
        var prop = await GetPropOrThrow(from.Id, worldId, cancellationToken);

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
        var prop = await GetPropOrThrow(from.Id, worldId, cancellationToken);

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

        var sourceOwner = await GetCreatureOrThrow(ownerId, worldId, cancellationToken);

        return new TheftSource(
            Owner: sourceOwner,
            LocationId: prop.LocationId,
            Skill: Skill.Sneak,
            IsPickpocketing: false,
            WorkstationOccupantId: workstationOccupantId
        );
    }

    private async Task<Creature> GetCreatureOrThrow(
        Guid creatureId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );
        return creature is { } found && found.WorldId == worldId
            ? found
            : throw new EntityNotFoundException(nameof(Creature), creatureId);
    }

    private async Task<Prop> GetPropOrThrow(
        Guid propId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = propId },
            cancellationToken
        );
        return prop is { } found && found.WorldId == worldId
            ? found
            : throw new EntityNotFoundException(nameof(Prop), propId);
    }
}
