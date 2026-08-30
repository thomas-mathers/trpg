using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Trading;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ResolvePlayerRespawnCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record PlayerRespawnFact(
    string DeathLocationName,
    string TempleCityName,
    Guid SanctuaryLocationId,
    int ItemsLeftOnCorpse,
    bool IsClericPresent,
    string? ClericName
);

internal class ResolvePlayerRespawnCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetNearestTempleSanctuaryFromLocationQuery,
        NearestTemple?
    > getNearestTempleSanctuary,
    ICommandHandler<AddCreatureCommand> addCreature,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    InventoryItemTransfer inventoryItemTransfer,
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation
) : ICommandHandler<ResolvePlayerRespawnCommand, PlayerRespawnFact>
{
    public async Task<PlayerRespawnFact> Handle(
        ResolvePlayerRespawnCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        var deathLocationId = player.LocationId;

        var deathLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = deathLocationId },
            cancellationToken
        );

        var nearestTemple =
            await getNearestTempleSanctuary.Handle(
                new GetNearestTempleSanctuaryFromLocationQuery
                {
                    WorldId = command.WorldId,
                    FromLocationId = deathLocationId,
                },
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"No temple could be found to respawn player {command.PlayerId} in world {command.WorldId}."
            );

        var corpse = new Creature
        {
            WorldId = player.WorldId,
            Name = $"{player.Name}'s remains",
            CreatureType = player.CreatureType,
            Gender = player.Gender,
            BirthLocationId = deathLocationId,
            BirthYear = player.BirthYear,
            LocationId = deathLocationId,
            Level = player.Level,
            State = CreatureState.Dead,
            BaseAttributes = player.BaseAttributes with { },
            PlayerCorpseOwnerId = player.Id,
        };

        await addCreature.Handle(new AddCreatureCommand { Creature = corpse }, cancellationToken);

        var playerItems = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(player.Id, OwnerType.Creature),
            },
            cancellationToken
        );

        if (playerItems.Count > 0)
        {
            await inventoryItemTransfer.Transfer(
                new ItemOwnerReference(player.Id, OwnerType.Creature),
                new ItemOwnerReference(corpse.Id, OwnerType.Creature),
                playerItems.Select(item => new ItemSelection(item.Id, item.Quantity)).ToArray(),
                cancellationToken
            );
            await context.SaveChangesAsync(cancellationToken);
        }

        var sanctuaryOccupants = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = nearestTemple.SanctuaryLocationId,
                ExcludingCreatureId = player.Id,
                IncludeDead = false,
            },
            cancellationToken
        );

        var cleric = sanctuaryOccupants.FirstOrDefault(occupant =>
            occupant.Profession == Profession.Cleric
        );

        transaction.Complete();

        return new PlayerRespawnFact(
            deathLocation?.Name ?? "an unknown place",
            nearestTemple.CityName,
            nearestTemple.SanctuaryLocationId,
            playerItems.Count,
            cleric != null,
            cleric?.Name
        );
    }
}
