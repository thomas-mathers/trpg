using Microsoft.EntityFrameworkCore;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ConfrontOverdueRoomKeyCommand
{
    public required Guid WorldId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required Guid BuildingId { get; init; }
}

public record ConfrontOverdueRoomKeyResult(TheftEncounter? Encounter);

internal class ConfrontOverdueRoomKeyCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetTradeWorkstationByBuildingIdQuery, Workstation?> getTradeWorkstation,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner
) : ICommandHandler<ConfrontOverdueRoomKeyCommand, ConfrontOverdueRoomKeyResult>
{
    public async Task<ConfrontOverdueRoomKeyResult> Handle(
        ConfrontOverdueRoomKeyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var heldOverdueKeys = await GetHeldOverdueKeys(command, cancellationToken);
        if (heldOverdueKeys.Count == 0)
        {
            return new ConfrontOverdueRoomKeyResult(null);
        }

        var workstation = await getTradeWorkstation.Handle(
            new GetTradeWorkstationByBuildingIdQuery { BuildingId = command.BuildingId },
            cancellationToken
        );
        if (workstation?.OwnerCreatureId is not { } innkeeperId)
        {
            return new ConfrontOverdueRoomKeyResult(null);
        }

        var innkeeper = await context
            .Creatures.AsNoTracking()
            .FirstAsync(creature => creature.Id == innkeeperId, cancellationToken);

        var sourceOwner = new ItemOwnerReference(workstation.Id, OwnerType.Workstation);

        var crime = new TheftCrime
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.LocationId,
            OwnerCreatureId = innkeeper.Id,
            OwnerFactionId = await getCityFactionForCreature.Handle(
                new GetCityFactionForCreatureQuery { CreatureId = innkeeper.Id },
                cancellationToken
            ),
            OwnerName = innkeeper.Name,
            SourceOwnerId = sourceOwner.Id,
            SourceOwnerType = sourceOwner.Type,
            Items = heldOverdueKeys
                .Select(key => new TheftCrimeItem(key.Name, key.Quantity))
                .ToList(),
        };
        context.Crimes.Add(crime);

        var encounter = new TheftEncounter
        {
            TheftCrimeId = crime.Id,
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.LocationId,
            ConfrontingCreatureId = innkeeper.Id,
            ConfrontingName = innkeeper.Name,
            SourceOwnerId = sourceOwner.Id,
            SourceOwnerType = sourceOwner.Type,
            ItemIds = heldOverdueKeys.Select(key => key.Id).ToList(),
            ItemNames = heldOverdueKeys.Select(key => key.Name).ToList(),
            ItemSelections = heldOverdueKeys
                .Select(key => new TheftEncounterItem(key.Id, key.Quantity))
                .ToList(),
            WitnessCreatureIds = [],
        };
        context.Encounters.Add(encounter);

        var overdueRoomIds = heldOverdueKeys.Select(key => key.RoomId).ToArray();
        await context
            .RoomBookings.Where(booking =>
                booking.PlayerId == command.PlayerId
                && overdueRoomIds.AsEnumerable().Contains(booking.RoomId)
            )
            .ExecuteDeleteAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new ConfrontOverdueRoomKeyResult(encounter);
    }

    private async Task<List<HeldOverdueKey>> GetHeldOverdueKeys(
        ConfrontOverdueRoomKeyCommand command,
        CancellationToken cancellationToken
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var overdueBookings = await context
            .RoomBookings.AsNoTracking()
            .Where(booking =>
                booking.WorldId == command.WorldId
                && booking.PlayerId == command.PlayerId
                && booking.DueAtPlaytime <= playtime
            )
            .Join(
                context.Rooms.AsNoTracking().Where(room => room.BuildingId == command.BuildingId),
                booking => booking.RoomId,
                room => room.Id,
                (booking, room) => booking
            )
            .ToListAsync(cancellationToken);

        if (overdueBookings.Count == 0)
        {
            return [];
        }

        var keyItemIds = overdueBookings.Select(booking => booking.KeyItemId).ToArray();
        var roomIdByKeyItemId = overdueBookings.ToDictionary(
            booking => booking.KeyItemId,
            booking => booking.RoomId
        );

        var heldKeys = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                OwnerId = command.PlayerId,
                OwnerType = OwnerType.Creature,
                ItemIds = keyItemIds,
            },
            cancellationToken
        );

        return heldKeys
            .Select(item => new HeldOverdueKey(
                item.Id,
                item.Name,
                item.Quantity,
                roomIdByKeyItemId[item.Id]
            ))
            .ToList();
    }

    private sealed record HeldOverdueKey(Guid Id, string Name, int Quantity, Guid RoomId);
}
