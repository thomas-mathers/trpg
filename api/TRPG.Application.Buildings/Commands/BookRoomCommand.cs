using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Trading.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Commands;

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
    TrpgDbContext context,
    IOptionsSnapshot<InnOptions> innOptions,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<AddGoldCommand> addGold,
    ICommandHandler<ReceivePlayerInventoryCommand> receivePlayerInventory
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

        var spareKey = await FindSpareRoomKey(building.Id, cancellationToken);
        if (spareKey == null)
        {
            return new BookRoomResult(BookRoomOutcome.NoVacancy);
        }

        var rate = innOptions.Value.RoomRatePerNight;
        var playerGold = await GetGoldQuantity(command.PlayerId, cancellationToken);
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
                Owner = new ItemOwnerReference(spareKey.WorkstationId, OwnerType.Workstation),
                WorldId = command.WorldId,
                Amount = rate,
            },
            cancellationToken
        );
        await receivePlayerInventory.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(spareKey.WorkstationId, OwnerType.Workstation),
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Items = [new ItemSelection(spareKey.KeyItemId, 1)],
            },
            cancellationToken
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        context.RoomBookings.Add(
            new RoomBooking
            {
                WorldId = command.WorldId,
                RoomId = spareKey.RoomId,
                KeyItemId = spareKey.KeyItemId,
                PlayerId = command.PlayerId,
                DueAtPlaytime = playtime + GameClock.RealTimePerInGameHour * 24,
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        return new BookRoomResult(BookRoomOutcome.Booked, spareKey.RoomName, rate);
    }

    private async Task<SpareRoomKey?> FindSpareRoomKey(
        Guid buildingId,
        CancellationToken cancellationToken
    )
    {
        var doorsByRoom = await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            join room in context.Rooms.AsNoTracking()
                on connector.DestinationLocationId equals room.LocationId
            where room.BuildingId == buildingId
            select new
            {
                room.Id,
                room.Name,
                DoorConnectorId = door.Id,
            }
        ).ToListAsync(cancellationToken);

        if (doorsByRoom.Count == 0)
        {
            return null;
        }

        var doorConnectorIds = doorsByRoom.Select(d => d.DoorConnectorId).ToArray();

        var spareKeysByDoor = await (
            from doorConnectorKey in context.DoorConnectorKeys.AsNoTracking()
            where doorConnectorIds.AsEnumerable().Contains(doorConnectorKey.DoorConnectorId)
            join item in context.Items.AsNoTracking() on doorConnectorKey.ItemId equals item.Id
            where item.Ownership.OwnerType == OwnerType.Workstation
            select new
            {
                doorConnectorKey.DoorConnectorId,
                KeyItemId = item.Id,
                WorkstationId = item.Ownership.OwnerId,
            }
        ).ToListAsync(cancellationToken);

        var match = doorsByRoom
            .Join(
                spareKeysByDoor,
                d => d.DoorConnectorId,
                k => k.DoorConnectorId,
                (d, k) => new SpareRoomKey(d.Id, d.Name, k.KeyItemId, k.WorkstationId)
            )
            .FirstOrDefault();

        return match;
    }

    private async Task<int> GetGoldQuantity(Guid playerId, CancellationToken cancellationToken) =>
        await context
            .Items.OfType<Gold>()
            .Where(item =>
                item.Ownership.OwnerId == playerId && item.Ownership.OwnerType == OwnerType.Creature
            )
            .Select(item => (int?)item.Quantity)
            .FirstOrDefaultAsync(cancellationToken)
        ?? 0;

    private sealed record SpareRoomKey(
        Guid RoomId,
        string RoomName,
        Guid KeyItemId,
        Guid WorkstationId
    );
}
