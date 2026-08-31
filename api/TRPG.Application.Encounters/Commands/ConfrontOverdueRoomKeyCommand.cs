using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Application.RoomBookings.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ConfrontOverdueRoomKeyCommand
{
    public required Guid WorldId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required Guid BuildingId { get; init; }
}

public record ConfrontOverdueRoomKeyResult(TheftEncounter? Encounter);

internal class ConfrontOverdueRoomKeyCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetTradeWorkstationByBuildingIdQuery, Workstation?> getTradeWorkstation,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
    IQueryHandler<
        GetRoomBookingsForPlayerInBuildingQuery,
        IReadOnlyCollection<RoomBooking>
    > getRoomBookingsForPlayerInBuilding,
    ICommandHandler<DeleteRoomBookingsCommand> deleteRoomBookings
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

        var innkeeper =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = innkeeperId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {innkeeperId} not found.");

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

        await context.SaveChangesAsync(cancellationToken);

        await deleteRoomBookings.Handle(
            new DeleteRoomBookingsCommand
            {
                RoomBookingIds = heldOverdueKeys.Select(key => key.RoomBookingId).ToArray(),
            },
            cancellationToken
        );

        return new ConfrontOverdueRoomKeyResult(encounter);
    }

    private async Task<List<HeldOverdueKey>> GetHeldOverdueKeys(
        ConfrontOverdueRoomKeyCommand command,
        CancellationToken cancellationToken
    )
    {
        var bookings = await getRoomBookingsForPlayerInBuilding.Handle(
            new GetRoomBookingsForPlayerInBuildingQuery
            {
                PlayerId = command.PlayerId,
                BuildingId = command.BuildingId,
            },
            cancellationToken
        );
        var overdueBookings = bookings
            .Where(booking =>
                booking.WorldId == command.WorldId && booking.DueAtPlaytime <= command.Playtime
            )
            .ToList();

        if (overdueBookings.Count == 0)
        {
            return [];
        }

        var keyItemIds = overdueBookings.Select(booking => booking.KeyItemId).ToArray();
        var bookingIdByKeyItemId = overdueBookings.ToDictionary(
            booking => booking.KeyItemId,
            booking => booking.Id
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
                bookingIdByKeyItemId[item.Id]
            ))
            .ToList();
    }

    private sealed record HeldOverdueKey(Guid Id, string Name, int Quantity, Guid RoomBookingId);
}
