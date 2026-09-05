using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class BookRoomCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required Guid LocationId { get; init; }
}

public enum BookRoomOutcome
{
    Booked,
    NoVacancy,
    InsufficientGold,
}

public record BookRoomResult(
    BookRoomOutcome Outcome,
    string? RoomName = null,
    int? GoldCharged = null
);

internal class BookRoomCommandHandler(
    IOptionsSnapshot<InnOptions> innOptions,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetGuestRoomDoorsByBuildingIdQuery,
        IReadOnlyList<GuestRoomDoor>
    > getGuestRoomDoors,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    IQueryHandler<
        GetWorkstationOwnedItemIdsQuery,
        IReadOnlyDictionary<Guid, Guid>
    > getWorkstationOwnedItemIds,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<AddGoldCommand> addGold,
    ICommandHandler<ReceivePlayerInventoryCommand> receivePlayerInventory,
    ICommandHandler<CreateRoomBookingCommand> createRoomBooking,
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<SetBedAssignmentCommand> setBedAssignment
) : ICommandHandler<BookRoomCommand, BookRoomResult>
{
    public async Task<BookRoomResult> Handle(
        BookRoomCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.LocationId },
            cancellationToken
        );
        if (building is not { BuildingType: BuildingType.Inn })
        {
            throw new InvalidOperationException(
                $"Location {command.LocationId} is not inside an Inn."
            );
        }

        var guestRoomDoors = await getGuestRoomDoors.Handle(
            new GetGuestRoomDoorsByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );
        var candidateKeyItemIds = guestRoomDoors
            .SelectMany(door => door.CandidateKeyItemIds)
            .ToArray();
        var workstationIdsByItemId = await getWorkstationOwnedItemIds.Handle(
            new GetWorkstationOwnedItemIdsQuery { ItemIds = candidateKeyItemIds },
            cancellationToken
        );
        var spareKey = FindSpareKeyDoor(guestRoomDoors, workstationIdsByItemId);
        if (spareKey == null)
        {
            return new BookRoomResult(BookRoomOutcome.NoVacancy);
        }
        var (spareKeyDoor, spareKeyItemId) = spareKey.Value;
        var workstationId = workstationIdsByItemId[spareKeyItemId];

        var bed =
            await getBedByLocationId.Handle(
                new GetBedByLocationIdQuery { LocationId = spareKeyDoor.LocationId },
                cancellationToken
            )
            ?? throw new InvalidOperationException($"Guest room {spareKeyDoor.RoomId} has no bed.");

        var rate = innOptions.Value.RoomRatePerNight;
        var playerGold = await getGoldQuantity.Handle(
            new GetGoldQuantityQuery
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        if (playerGold < rate)
        {
            return new BookRoomResult(BookRoomOutcome.InsufficientGold);
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await removeGold.Handle(
            new RemoveGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                Amount = rate,
            },
            cancellationToken
        );
        await addGold.Handle(
            new AddGoldCommand
            {
                Owner = new ItemOwnerReference(workstationId, OwnerType.Workstation),
                WorldId = command.WorldId,
                Amount = rate,
            },
            cancellationToken
        );
        await receivePlayerInventory.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(workstationId, OwnerType.Workstation),
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Items = [new ItemSelection(spareKeyItemId, 1)],
            },
            cancellationToken
        );

        await createRoomBooking.Handle(
            new CreateRoomBookingCommand
            {
                RoomBooking = new RoomBooking
                {
                    WorldId = command.WorldId,
                    RoomId = spareKeyDoor.RoomId,
                    KeyItemId = spareKeyItemId,
                    PlayerId = command.PlayerId,
                    DueAtPlaytime = command.Playtime + GameClock.RealTimePerInGameHour * 24,
                },
            },
            cancellationToken
        );

        await setBedAssignment.Handle(
            new SetBedAssignmentCommand
            {
                BedIds = [bed.Id],
                AssignedCreatureId = command.PlayerId,
            },
            cancellationToken
        );

        transaction.Complete();

        return new BookRoomResult(BookRoomOutcome.Booked, spareKeyDoor.RoomName, rate);
    }

    private static (GuestRoomDoor Door, Guid KeyItemId)? FindSpareKeyDoor(
        IReadOnlyList<GuestRoomDoor> guestRoomDoors,
        IReadOnlyDictionary<Guid, Guid> workstationIdsByItemId
    )
    {
        foreach (var door in guestRoomDoors)
        {
            foreach (var keyItemId in door.CandidateKeyItemIds)
            {
                if (workstationIdsByItemId.ContainsKey(keyItemId))
                {
                    return (door, keyItemId);
                }
            }
        }

        return null;
    }
}
