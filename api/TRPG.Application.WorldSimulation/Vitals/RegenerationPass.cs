using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Vitals;

internal sealed class RegenerationPass(
    Guid worldId,
    SimulationCadence cadence,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
)
{
    private readonly DueEntry _due = new(cadence.ActiveLocationPass);

    public async Task<IReadOnlyList<SimEvent>> Run(
        CreatureSimulator simulator,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        if (!_due.TakeIfDue(now))
        {
            return [];
        }

        try
        {
            var states = await LoadStates(cancellationToken);

            return simulator.Regenerate(states, now);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Regeneration pass failed in world {WorldId}; retrying when next due",
                worldId
            );

            return [];
        }
    }

    private async Task<IReadOnlyCollection<CreatureVitalState>> LoadStates(
        CancellationToken cancellationToken
    )
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var players = await services
            .GetRequiredService<
                IQueryHandler<
                    GetActiveLocationPlayersQuery,
                    IReadOnlyCollection<ActiveLocationPlayer>
                >
            >()
            .Handle(new GetActiveLocationPlayersQuery { WorldId = worldId }, cancellationToken);
        if (players.Count == 0)
        {
            return [];
        }

        // A fight advances only through submitted actions and effect ticks, so waiting inside one must not heal anyone.
        var fightCombatantIds = await services
            .GetRequiredService<
                IQueryHandler<GetActiveFightCombatantIdsByWorldQuery, IReadOnlyCollection<Guid>>
            >()
            .Handle(
                new GetActiveFightCombatantIdsByWorldQuery { WorldId = worldId },
                cancellationToken
            );

        return await services
            .GetRequiredService<
                IQueryHandler<
                    GetRegeneratingCreaturesQuery,
                    IReadOnlyCollection<CreatureVitalState>
                >
            >()
            .Handle(
                new GetRegeneratingCreaturesQuery
                {
                    WorldId = worldId,
                    LocationIds = [.. players.Select(player => player.LocationId).Distinct()],
                    ExcludedCreatureIds = fightCombatantIds,
                },
                cancellationToken
            );
    }
}
