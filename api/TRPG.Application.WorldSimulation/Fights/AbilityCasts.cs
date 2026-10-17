using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Fights;

internal sealed class AbilityCasts(
    Guid worldId,
    IServiceScopeFactory serviceScopeFactory,
    TransientCreatureWalkRegistry transientWalks
)
{
    private static readonly TimeSpan UnstartedChaseExpiry = TimeSpan.FromSeconds(2);

    private readonly Dictionary<Guid, ActiveFight> _fights = [];
    private readonly Dictionary<Guid, Chase> _chases = [];
    private readonly Dictionary<Guid, Point> _journeyPositions = [];

    private sealed class Chase(Point destination, GameInstant issuedAt)
    {
        public Point Destination { get; } = destination;
        public GameInstant IssuedAt { get; } = issuedAt;
        public bool Started { get; set; }
    }

    public async Task Cast(
        CastAbilityMessage message,
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await Run(message, poses, now, cancellationToken);
            Track(message.PlayerId, result.Fight);
            message.Completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            message.Completion.TrySetException(exception);
            throw;
        }
    }

    public async Task<IReadOnlyList<LocalMovePlan>> Tick(
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var pursuits = new List<LocalMovePlan>();
        foreach (var fight in _fights.Values.ToArray())
        {
            var fightPursuits = await TickFight(fight, poses, now, cancellationToken);
            pursuits.AddRange(fightPursuits);
        }

        return pursuits;
    }

    public async Task Abandon(Guid playerId, GameInstant now, CancellationToken cancellationToken)
    {
        if (!_fights.ContainsKey(playerId))
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<ICommandHandler<AbandonFightsCommand>>()
            .Handle(
                new AbandonFightsCommand { WorldId = worldId, GameTime = now },
                cancellationToken
            );
        End(playerId);
    }

    public void End(Guid playerId)
    {
        if (_fights.Remove(playerId, out var fight))
        {
            ForgetChases(fight);
        }
    }

    private async Task<IReadOnlyList<LocalMovePlan>> TickFight(
        ActiveFight fight,
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var pose = poses.Find(fight.PlayerId);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        if (pose is null || pose.LocationId != fight.LocationId)
        {
            await scope
                .ServiceProvider.GetRequiredService<ICommandHandler<LeaveFightCommand>>()
                .Handle(new LeaveFightCommand { Fight = fight, GameTime = now }, cancellationToken);
            End(fight.PlayerId);
            return [];
        }

        MarkStartedChases(fight, now);
        await RefreshJourneyPositions(
            scope.ServiceProvider,
            fight.LocationId,
            fight.CombatantIds,
            now,
            cancellationToken
        );
        var result = await scope
            .ServiceProvider.GetRequiredService<
                ICommandHandler<TickFightCommand, FightTickResult>
            >()
            .Handle(
                new TickFightCommand
                {
                    Fight = fight,
                    GameTime = now,
                    PlayerPlacement = new PlayerPlacement(pose.Position, pose.Input.Heading),
                    FindLiveCreaturePosition = creatureId =>
                        FindEnemyPosition(creatureId, fight.LocationId, now),
                },
                cancellationToken
            );
        if (result.Changed)
        {
            await scope
                .ServiceProvider.GetRequiredService<IActionSceneAnnouncer>()
                .AnnounceActionChanges(worldId, fight.PlayerId, now, cancellationToken);
        }

        if (result.Over)
        {
            End(fight.PlayerId);
            return [];
        }

        return await PlanPursuits(
            scope.ServiceProvider,
            fight,
            result.Chases,
            now,
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<LocalMovePlan>> PlanPursuits(
        IServiceProvider services,
        ActiveFight fight,
        IReadOnlyList<ChaseOrder> orders,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var planner = services.GetRequiredService<ChasePathPlanner>();
        var pursuits = new List<LocalMovePlan>();
        foreach (var order in orders)
        {
            if (!NeedsChase(order, fight.LocationId, now))
            {
                continue;
            }

            var path = await planner.Plan(
                fight.LocationId,
                fight.PositionOf(order.CreatureId),
                order.Target,
                order.StopDistance,
                cancellationToken
            );
            if (path is null)
            {
                continue;
            }

            _chases[order.CreatureId] = new Chase(path[^1], now);
            pursuits.Add(
                new LocalMovePlan(
                    order.CreatureId,
                    fight.LocationId,
                    Guid.Empty,
                    CreatureJobAction.Idle,
                    LocalMoveTargetKind.Pursuit,
                    Guid.Empty,
                    path
                )
            );
        }

        return pursuits;
    }

    private bool NeedsChase(ChaseOrder order, Guid locationId, GameInstant now)
    {
        if (!_chases.TryGetValue(order.CreatureId, out var chase))
        {
            return true;
        }

        if (!chase.Started)
        {
            return now - chase.IssuedAt > UnstartedChaseExpiry;
        }

        return transientWalks.Find(order.CreatureId, locationId, now) is null;
    }

    private void MarkStartedChases(ActiveFight fight, GameInstant now)
    {
        foreach (var creatureId in fight.CombatantIds)
        {
            if (
                _chases.TryGetValue(creatureId, out var chase)
                && transientWalks.Find(creatureId, fight.LocationId, now) is not null
            )
            {
                chase.Started = true;
            }
        }
    }

    private async Task RefreshJourneyPositions(
        IServiceProvider services,
        Guid locationId,
        IReadOnlyCollection<Guid> creatureIds,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var positions = await services
            .GetRequiredService<
                IQueryHandler<
                    GetCreatureJourneyPositionsQuery,
                    IReadOnlyDictionary<Guid, CreatureJourneyPosition>
                >
            >()
            .Handle(
                new GetCreatureJourneyPositionsQuery
                {
                    LocationId = locationId,
                    GameTime = now,
                    CreatureIds = creatureIds,
                },
                cancellationToken
            );
        foreach (var creatureId in creatureIds)
        {
            if (positions.TryGetValue(creatureId, out var position))
            {
                _journeyPositions[creatureId] = position.Position;
            }
            else
            {
                _journeyPositions.Remove(creatureId);
            }
        }
    }

    private Point? FindEnemyPosition(Guid creatureId, Guid locationId, GameInstant now)
    {
        var walking = transientWalks.Find(creatureId, locationId, now);
        if (walking is not null)
        {
            return walking.Position;
        }

        if (_chases.TryGetValue(creatureId, out var chase) && chase.Started)
        {
            return chase.Destination;
        }

        return _journeyPositions.TryGetValue(creatureId, out var journeyPosition)
            ? journeyPosition
            : null;
    }

    private void ForgetChases(ActiveFight fight)
    {
        foreach (var creatureId in fight.CombatantIds)
        {
            _chases.Remove(creatureId);
            _journeyPositions.Remove(creatureId);
        }
    }

    private async Task<AbilityCastResult> Run(
        CastAbilityMessage message,
        LivePlayerPoses poses,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var pose = poses.Find(message.PlayerId);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        if (pose is not null)
        {
            await RefreshJourneyPositions(
                scope.ServiceProvider,
                pose.LocationId,
                [message.TargetId],
                now,
                cancellationToken
            );
        }

        var result = await scope
            .ServiceProvider.GetRequiredService<
                ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult>
            >()
            .Handle(
                new CastPlayerAbilityCommand
                {
                    WorldId = worldId,
                    PlayerId = message.PlayerId,
                    TargetId = message.TargetId,
                    AbilityName = message.AbilityName,
                    GameTime = now,
                    Fight = _fights.GetValueOrDefault(message.PlayerId),
                    PlayerPlacement = pose is null
                        ? null
                        : new PlayerPlacement(pose.Position, pose.Input.Heading),
                    FindLiveCreaturePosition = creatureId =>
                        pose is null ? null : FindEnemyPosition(creatureId, pose.LocationId, now),
                },
                cancellationToken
            );
        if (result.Succeeded)
        {
            await scope
                .ServiceProvider.GetRequiredService<IActionSceneAnnouncer>()
                .AnnounceActionChanges(worldId, message.PlayerId, now, cancellationToken);
        }

        return result;
    }

    private void Track(Guid playerId, ActiveFight? fight)
    {
        if (fight is null)
        {
            End(playerId);
            return;
        }

        _fights[playerId] = fight;
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));
}
