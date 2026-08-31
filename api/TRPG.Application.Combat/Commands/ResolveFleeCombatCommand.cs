using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Locations.Commands;
using TRPG.Application.Locations.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class ResolveFleeCombatCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record FleeCombatResult(
    CombatResult CombatResult,
    Guid? DestinationLocationId,
    string? DestinationLocationName
);

internal class ResolveFleeCombatCommandHandler(
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeyItemIdsByOwner
) : ICommandHandler<ResolveFleeCombatCommand, FleeCombatResult?>
{
    private const string OutsideExitLabel = "Outside";

    public async Task<FleeCombatResult?> Handle(
        ResolveFleeCombatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await combatantLoader.Load(command.PlayerId, cancellationToken);
        if (combatants.Count == 0)
            return null;

        var state = combatEngine.ResolveFlee(combatants);
        var combatResult = await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                Combatants = combatants,
                State = state,
            },
            cancellationToken
        );

        var destinationLocationId = await ResolveDestination(command, cancellationToken);
        if (destinationLocationId == null)
        {
            return new FleeCombatResult(combatResult, null, null);
        }

        var destinationLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = destinationLocationId.Value },
            cancellationToken
        );

        return new FleeCombatResult(
            combatResult,
            destinationLocationId.Value,
            destinationLocation?.Name
        );
    }

    private async Task<Guid?> ResolveDestination(
        ResolveFleeCombatCommand command,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        if (player!.PreviousLocationId is { } previousLocationId)
        {
            return previousLocationId;
        }

        var connectors = await getConnectorsByLocationId.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        if (connectors.Count == 0)
        {
            return null;
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var playerKeyItemIds = await getKeyItemIdsByOwner.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );

        var accessibleConnectorIds = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = playerKeyItemIds,
                Playtime = playtime,
                ConnectorIds = connectors.Select(connector => connector.Id).ToArray(),
            },
            cancellationToken
        );

        var openExits = connectors
            .Where(connector => accessibleConnectorIds.Contains(connector.Id))
            .ToArray();

        if (openExits.Length == 0)
        {
            return null;
        }

        var outsideExit = openExits.FirstOrDefault(connector =>
            connector.DestinationLabel == OutsideExitLabel
        );

        return outsideExit?.DestinationLocationId
            ?? openExits[Random.Shared.Next(openExits.Length)].DestinationLocationId;
    }
}
