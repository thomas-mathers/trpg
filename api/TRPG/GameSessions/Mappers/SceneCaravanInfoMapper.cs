using TRPG.Application.GameTurns.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneCaravanInfoMapper
{
    public static NearbyCaravanSnapshot ToSnapshot(this SceneCaravanInfo caravan) =>
        new(
            caravan.CaravanId,
            caravan.RouteName,
            caravan.TicketFeeGold,
            caravan.MinutesUntilDeparture,
            caravan.Destinations.Select(destination => destination.ToSnapshot()).ToArray()
        );

    private static CaravanDestinationSnapshot ToSnapshot(
        this SceneCaravanDestination destination
    ) =>
        new(
            destination.LocationId,
            destination.LocationName,
            destination.TravelTimeHours,
            destination.HasTicket
        );
}
