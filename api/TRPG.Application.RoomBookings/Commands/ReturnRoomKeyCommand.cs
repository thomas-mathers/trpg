using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.RoomBookings.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class ReturnRoomKeyCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

public enum ReturnRoomKeyOutcome
{
    Returned,
    NoActiveBooking,
}

public record ReturnRoomKeyResult(ReturnRoomKeyOutcome Outcome);

internal class ReturnRoomKeyCommandHandler(
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId,
    IQueryHandler<
        GetTradeWorkstationByLocationIdsQuery,
        Workstation?
    > getTradeWorkstationByLocationIds,
    IQueryHandler<
        GetRoomBookingsForPlayerInBuildingQuery,
        IReadOnlyCollection<RoomBooking>
    > getRoomBookingsForPlayerInBuilding,
    ICommandHandler<TransferPlayerInventoryCommand> transferPlayerInventory,
    ICommandHandler<DeleteRoomBookingsCommand> deleteRoomBookings
) : ICommandHandler<ReturnRoomKeyCommand, ReturnRoomKeyResult>
{
    public async Task<ReturnRoomKeyResult> Handle(
        ReturnRoomKeyCommand command,
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

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var bookings = await getRoomBookingsForPlayerInBuilding.Handle(
            new GetRoomBookingsForPlayerInBuildingQuery
            {
                PlayerId = command.PlayerId,
                BuildingId = building.Id,
            },
            cancellationToken
        );
        var booking = bookings.FirstOrDefault();
        if (booking == null)
        {
            transaction.Complete();
            return new ReturnRoomKeyResult(ReturnRoomKeyOutcome.NoActiveBooking);
        }

        var workstation = await GetTradeWorkstation(building.Id, cancellationToken);
        if (workstation == null)
        {
            throw new InvalidOperationException(
                $"Building {building.Id} has no Trade workstation."
            );
        }

        await transferPlayerInventory.Handle(
            new TransferPlayerInventoryCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                To = new ItemOwnerReference(workstation.Id, OwnerType.Workstation),
                Items = [new ItemSelection(booking.KeyItemId, 1)],
            },
            cancellationToken
        );

        await deleteRoomBookings.Handle(
            new DeleteRoomBookingsCommand { RoomBookingIds = [booking.Id] },
            cancellationToken
        );

        transaction.Complete();
        return new ReturnRoomKeyResult(ReturnRoomKeyOutcome.Returned);
    }

    private async Task<Workstation?> GetTradeWorkstation(
        Guid buildingId,
        CancellationToken cancellationToken
    )
    {
        var rooms = await getRoomsByBuildingId.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = buildingId },
            cancellationToken
        );

        return await getTradeWorkstationByLocationIds.Handle(
            new GetTradeWorkstationByLocationIdsQuery
            {
                LocationIds = rooms.Select(room => room.LocationId).ToArray(),
            },
            cancellationToken
        );
    }
}
