using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Hubs;

internal sealed class ChatHub(
    IServiceProvider serviceProvider,
    EndGameSessionCommandHandler endGameSession,
    GetGameSessionQueryHandler getGameSession,
    WorldConnectionRegistry worldConnections,
    GetCreatureByIdQueryHandler getCreatureById,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    GetCombatantsQueryHandler getCombatants,
    ILogger<ChatHub> logger
) : Hub
{
    private const string SessionIdKey = "SessionId";
    private const string WorldIdKey = "WorldId";

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        Context.Items[SessionIdKey] = sessionId;

        var worldId = await ResolveWorldId(sessionId);
        Context.Items[WorldIdKey] = worldId;

        if (!worldConnections.TryAdd(worldId, Context.ConnectionId))
        {
            throw new HubException("Another connection is already active for this world.");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[WorldIdKey] is Guid worldId)
        {
            worldConnections.Remove(worldId, Context.ConnectionId);
        }

        if (Context.Items[SessionIdKey] is Guid sessionId)
        {
            await endGameSession.Handle(new EndGameSessionCommand { SessionId = sessionId });
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<Guid> ResolveWorldId(Guid sessionId)
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId }
        );
        return snapshot.WorldId;
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken)
    {
        var turnContext = ResolveTurnContext();
        var turnRunner = ResolveTurnRunner();
        var result = new TurnStreamResult();
        return StreamTurn(
            turnRunner.StreamOpeningResponse(result, cancellationToken),
            turnContext,
            result,
            alwaysSendScene: true,
            cancellationToken
        );
    }

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken)
    {
        var turnContext = ResolveTurnContext();
        var turnRunner = ResolveTurnRunner();
        var result = new TurnStreamResult();
        return StreamTurn(
            turnRunner.StreamResponse(message, result, cancellationToken),
            turnContext,
            result,
            alwaysSendScene: false,
            cancellationToken
        );
    }

    public IAsyncEnumerable<string> SendWait(int hours, CancellationToken cancellationToken)
    {
        var turnContext = ResolveTurnContext();
        var turnRunner = ResolveTurnRunner();
        var result = new TurnStreamResult();
        return StreamTurn(
            turnRunner.StreamWaitResponse(hours, result, cancellationToken),
            turnContext,
            result,
            alwaysSendScene: true,
            cancellationToken
        );
    }

    private async IAsyncEnumerable<string> StreamTurn(
        IAsyncEnumerable<string> tokens,
        GameTurnContext turnContext,
        TurnStreamResult result,
        bool alwaysSendScene,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (var token in tokens.WithCancellation(cancellationToken))
        {
            yield return token;
        }

        if (alwaysSendScene || result.DidSceneRefreshThisTurn)
        {
            var sceneWithPlayer = await GetCurrentScene(
                turnContext,
                result.CurrentDate,
                cancellationToken
            );
            var snapshot = ToSnapshot(sceneWithPlayer.Scene, sceneWithPlayer.Player);
            await Clients.Caller.SendAsync("Scene", snapshot, cancellationToken);
        }

        if (result.DidCombatOccurThisTurn)
        {
            var combatants = await getCombatants.Handle(
                new GetCombatantsQuery { SessionId = turnContext.SessionId },
                cancellationToken
            );
            logger.LogInformation(
                "[combat] pushing CombatTurnStatus for session {SessionId}: {Combatants}",
                turnContext.SessionId,
                string.Join(
                    ", ",
                    combatants.Select(c => $"{c.Name}={c.CurrentHp}/{c.MaximumHp}hp")
                )
            );
            var combatSnapshot = ToCombatSnapshot(combatants);
            await Clients.Caller.SendAsync("CombatTurnStatus", combatSnapshot, cancellationToken);
        }
    }

    private async Task<SceneWithPlayer> GetCurrentScene(
        GameTurnContext turnContext,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        var scene = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                RoomId = player!.RoomId,
                DistrictId = player.DistrictId,
                StateId = player.StateId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        return new SceneWithPlayer(scene, player);
    }

    private GameTurnRunner ResolveTurnRunner() =>
        serviceProvider.GetRequiredService<GameTurnRunner>();

    private GameTurnContext ResolveTurnContext()
    {
        var turnContext = serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.SessionId = (Guid)Context.Items[SessionIdKey]!;
        return turnContext;
    }

    private Guid GetSessionIdFromQuery()
    {
        var raw = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var sessionId))
        {
            throw new HubException("A valid sessionId query parameter is required.");
        }

        return sessionId;
    }

    private static SceneSnapshot ToSnapshot(SceneResult scene, Creature player)
    {
        var currentDistrict = scene.City?.Districts.FirstOrDefault(d => d.IsCurrent);
        return new SceneSnapshot(
            StateName: scene.State?.Name ?? "",
            CityName: scene.City?.Name,
            DistrictName: currentDistrict?.Name,
            BuildingName: scene.Building?.Name,
            RoomName: scene.Room?.Name,
            Year: scene.CurrentDate.Year,
            MonthName: scene.CurrentDate.MonthName,
            Day: scene.CurrentDate.Day,
            WeekdayName: scene.CurrentDate.WeekdayName,
            Hour: scene.CurrentDate.Hour,
            PlayerStatus: new CreatureStatusSnapshot(
                Name: player.Name,
                CreatureType: player.CreatureType.ToString(),
                Gender: player.Gender.ToString(),
                Profession: player.Profession?.ToString() ?? "",
                Level: player.Level,
                Age: scene.CurrentDate.Year - player.BirthYear,
                State: player.State.ToString(),
                Gold: player.Gold,
                CurrentHp: player.CurrentHp,
                MaximumHp: player.Attributes.MaximumHp,
                CurrentAp: player.CurrentAp,
                MaximumAp: player.Attributes.MaximumAp,
                CurrentMp: player.CurrentMp,
                MaximumMp: player.Attributes.MaximumMp,
                FactionNames: null,
                Reputation: null
            ),
            NearbyCreatures: scene
                .NearbyPeople.Select(p => new CreatureStatusSnapshot(
                    Name: p.Name,
                    CreatureType: p.CreatureType,
                    Gender: p.Gender,
                    Profession: p.Profession,
                    Level: p.Level,
                    Age: p.Age,
                    State: p.State,
                    Gold: p.Gold,
                    CurrentHp: p.CurrentHp,
                    MaximumHp: p.MaximumHp,
                    CurrentAp: p.CurrentAp,
                    MaximumAp: p.MaximumAp,
                    CurrentMp: p.CurrentMp,
                    MaximumMp: p.MaximumMp,
                    FactionNames: p.FactionNames,
                    Reputation: p.Reputation
                ))
                .ToArray(),
            NearbyDistricts: scene
                .City?.Districts.Select(d => new NearbyDistrictSnapshot(d.Name, d.Type))
                .ToArray() ?? [],
            NearbyBuildings: scene
                .NearbyBuildings.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type))
                .ToArray(),
            NearbyDungeons: scene
                .NearbyDungeons.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type))
                .ToArray(),
            NearbyProps: scene.NearbyProps.Select(p => new NearbyPropSnapshot(p.Name, p.Type))
                .ToArray(),
            Exits: scene
                .Room?.Exits.Select(e => new NearbyExitSnapshot(
                    e.Description,
                    e.DestinationRoomName
                ))
                .ToArray() ?? []
        );
    }

    private static CombatSnapshot ToCombatSnapshot(IReadOnlyList<Combatant> combatants) =>
        new(
            combatants
                .Select(c => new CombatantStatusSnapshot(
                    Name: c.Name,
                    IsPlayer: c.IsPlayer,
                    IsAlive: c.IsAlive,
                    CurrentHp: c.CurrentHp,
                    MaximumHp: c.MaximumHp,
                    CurrentAp: c.CurrentAp,
                    MaximumAp: c.MaximumAp,
                    CurrentMp: c.CurrentMp,
                    MaximumMp: c.MaximumMp,
                    ActiveConditions: c
                        .ActiveConditions.Where(kv => kv.Value > 0)
                        .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    ActiveDots: c
                        .ActiveDots.Select(d => new ActiveDotStatus(
                            d.AbilityName,
                            d.Amount,
                            d.DamageType.ToString(),
                            d.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveHots: c
                        .ActiveHots.Select(h => new ActiveHotStatus(
                            h.AbilityName,
                            h.Amount,
                            h.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveBuffs: c
                        .ActiveBuffs.Select(b => new ActiveBuffStatus(
                            b.Attribute.ToString(),
                            b.Amount,
                            b.AmountType.ToString(),
                            b.RemainingTurns
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
}

internal sealed record SceneWithPlayer(SceneResult Scene, Creature Player);

internal sealed class WorldConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connectionIdsByWorldId = new();

    public bool TryAdd(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryAdd(worldId, connectionId);

    public void Remove(Guid worldId, string connectionId) =>
        _connectionIdsByWorldId.TryRemove(new KeyValuePair<Guid, string>(worldId, connectionId));
}
