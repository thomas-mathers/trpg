using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class ResolveFleeCombatCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record FleeCombatResult(CombatResult CombatResult, string? DestinationLocationName);

internal class ResolveFleeCombatCommandHandler(
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
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

        var destinationLocationId = await ResolveDestination(command.PlayerId, cancellationToken);
        if (destinationLocationId == null)
        {
            return new FleeCombatResult(combatResult, null);
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.PlayerId],
                LocationId = destinationLocationId.Value,
            },
            cancellationToken
        );

        var destinationLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = destinationLocationId.Value },
            cancellationToken
        );

        return new FleeCombatResult(combatResult, destinationLocation?.Name);
    }

    private async Task<Guid?> ResolveDestination(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = playerId },
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

        var doorsByConnectorId = await getDoorConnectorsByConnectorIds.Handle(
            new GetDoorConnectorsByConnectorIdsQuery
            {
                ConnectorIds = connectors.Select(connector => connector.Id).ToArray(),
            },
            cancellationToken
        );

        var openExits = connectors
            .Where(connector =>
                !doorsByConnectorId.TryGetValue(connector.Id, out var door) || !door.IsLocked
            )
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
