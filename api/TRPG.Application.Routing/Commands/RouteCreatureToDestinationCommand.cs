using TRPG.Application.Common.Commands;

namespace TRPG.Application.Routing.Commands;

public class RouteCreatureToDestinationCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid DestinationLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required string Purpose { get; init; }
}

public record RouteCreatureToDestinationResult(Guid? RouteTravelerId, bool IsAlreadyAtDestination);

internal class RouteCreatureToDestinationCommandHandler(
    ICommandHandler<
        RouteCreaturesToDestinationsCommand,
        IReadOnlyDictionary<Guid, RouteCreatureResult>
    > routeCreaturesToDestinations
) : ICommandHandler<RouteCreatureToDestinationCommand, RouteCreatureToDestinationResult>
{
    public async Task<RouteCreatureToDestinationResult> Handle(
        RouteCreatureToDestinationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var results = await routeCreaturesToDestinations.Handle(
            new RouteCreaturesToDestinationsCommand
            {
                Routes =
                [
                    new CreatureRouteRequest(
                        command.CreatureId,
                        command.DestinationLocationId,
                        command.Playtime,
                        command.Purpose
                    ),
                ],
            },
            cancellationToken
        );
        var result = results[command.CreatureId];
        return new RouteCreatureToDestinationResult(
            result.RouteTravelerId,
            result.IsAlreadyAtDestination
        );
    }
}
