using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

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
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<ResolveExitConnectorCommand, Guid?> resolveExitConnector
) : ICommandHandler<ResolveFleeCombatCommand, FleeCombatResult?>
{
    public async Task<FleeCombatResult?> Handle(
        ResolveFleeCombatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await combatantLoader.Load(command.PlayerId, cancellationToken);
        if (combatants.Count == 0)
            return null;

        var state = combatEngine.ProcessRound(combatants, new ResolvedFleeAction());
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

        if (state.Outcome != CombatOutcome.Fled)
        {
            return new FleeCombatResult(combatResult, null, null);
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var destinationLocationId = await resolveExitConnector.Handle(
            new ResolveExitConnectorCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                Playtime = playtime,
            },
            cancellationToken
        );
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
}
