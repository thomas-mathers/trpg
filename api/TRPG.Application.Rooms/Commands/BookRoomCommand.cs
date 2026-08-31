using Microsoft.Extensions.Options;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Rooms.Commands;

public class BookRoomCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid SessionId { get; init; }
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
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetGuestRoomDoorsByBuildingIdQuery,
        IReadOnlyList<GuestRoomDoor>
    > getGuestRoomDoors,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<AddGoldCommand> addGold,
    ICommandHandler<ReceivePlayerInventoryCommand> receivePlayerInventory,
    ICommandHandler<CreateRoomBookingCommand> createRoomBooking
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
        var spareKey = guestRoomDoors.FirstOrDefault(door => door.SpareKeyItemId != null);
        if (spareKey == null)
        {
            return new BookRoomResult(BookRoomOutcome.NoVacancy);
        }

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
                Owner = new ItemOwnerReference(
                    spareKey.WorkstationId!.Value,
                    OwnerType.Workstation
                ),
                WorldId = command.WorldId,
                Amount = rate,
            },
            cancellationToken
        );
        await receivePlayerInventory.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(spareKey.WorkstationId!.Value, OwnerType.Workstation),
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Items = [new ItemSelection(spareKey.SpareKeyItemId!.Value, 1)],
            },
            cancellationToken
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        await createRoomBooking.Handle(
            new CreateRoomBookingCommand
            {
                RoomBooking = new RoomBooking
                {
                    WorldId = command.WorldId,
                    RoomId = spareKey.RoomId,
                    KeyItemId = spareKey.SpareKeyItemId!.Value,
                    PlayerId = command.PlayerId,
                    DueAtPlaytime = playtime + GameClock.RealTimePerInGameHour * 24,
                },
            },
            cancellationToken
        );

        return new BookRoomResult(BookRoomOutcome.Booked, spareKey.RoomName, rate);
    }
}
