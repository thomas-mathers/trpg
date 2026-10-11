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

internal sealed class VitalsPass(
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
            var inputs = await Load(now, cancellationToken);

            return simulator.TickVitals(inputs.Advances, inputs.States, now);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Vitals pass failed in world {WorldId}; retrying when next due",
                worldId
            );

            return [];
        }
    }

    private async Task<VitalsInputs> Load(GameInstant now, CancellationToken cancellationToken)
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
            return VitalsInputs.None;
        }

        // A fight advances only through submitted actions and effect ticks, so neither healing nor the out-of-combat effect path may touch its combatants.
        var fightCombatantIds = await services
            .GetRequiredService<
                IQueryHandler<GetActiveFightCombatantIdsByWorldQuery, IReadOnlyCollection<Guid>>
            >()
            .Handle(
                new GetActiveFightCombatantIdsByWorldQuery { WorldId = worldId },
                cancellationToken
            );
        var locationIds = players.Select(player => player.LocationId).Distinct().ToArray();

        var advances = await services
            .GetRequiredService<
                IQueryHandler<
                    GetCreatureEffectAdvancesQuery,
                    IReadOnlyCollection<CreatureEffectAdvance>
                >
            >()
            .Handle(
                new GetCreatureEffectAdvancesQuery
                {
                    WorldId = worldId,
                    LocationIds = locationIds,
                    GameTime = now,
                    ExcludedCreatureIds = fightCombatantIds,
                },
                cancellationToken
            );
        var states = await services
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
                    LocationIds = locationIds,
                    ExcludedCreatureIds = fightCombatantIds,
                },
                cancellationToken
            );

        return new VitalsInputs(advances, states);
    }

    private sealed record VitalsInputs(
        IReadOnlyCollection<CreatureEffectAdvance> Advances,
        IReadOnlyCollection<CreatureVitalState> States
    )
    {
        public static VitalsInputs None { get; } = new([], []);
    }
}
