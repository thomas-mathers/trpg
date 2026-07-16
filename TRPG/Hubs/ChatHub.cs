using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Game.Commands;
using TRPG.Application.Game.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts;
using TRPG.Data.Models;

namespace TRPG.Hubs;

internal sealed class ChatHub(
    IServiceProvider serviceProvider,
    EndGameSessionCommandHandler endGameSession,
    GetGameSessionQueryHandler getGameSession,
    WorldConnectionRegistry worldConnections,
    GetCreatureByIdQueryHandler getCreatureById,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp
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
        var snapshot = await getGameSession.Handle(new GetGameSessionQuery { SessionId = sessionId });
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
            var scene = await GetCurrentScene(turnContext, result.CurrentDate, cancellationToken);
            await Clients.Caller.SendAsync("Scene", ToSnapshot(scene), cancellationToken);
        }
    }

    private async Task<SceneResult> GetCurrentScene(
        GameTurnContext turnContext,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        return await getSceneWithCatchUp.Handle(
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

    private static SceneSnapshot ToSnapshot(SceneResult scene)
    {
        var currentDistrict = scene.City?.Districts.FirstOrDefault(d => d.IsCurrent);
        return new SceneSnapshot(
            scene.State?.Name ?? "",
            scene.City?.Name,
            currentDistrict?.Name,
            scene.Building?.Name,
            scene.Room?.Name,
            scene.CurrentDate.Year,
            scene.CurrentDate.MonthName,
            scene.CurrentDate.Day,
            scene.CurrentDate.WeekdayName,
            scene.CurrentDate.Hour,
            scene
                .NearbyPeople.Select(p => new NearbyPersonSnapshot(
                    p.Name,
                    p.CreatureType,
                    p.Gender,
                    p.Profession,
                    p.Level,
                    p.Age,
                    p.FactionNames,
                    p.State,
                    p.Reputation
                ))
                .ToArray(),
            scene.City?.Districts.Select(d => new NearbyDistrictSnapshot(d.Name, d.Type)).ToArray()
                ?? [],
            scene.NearbyBuildings.Select(b => new NearbyBuildingSnapshot(b.Name, b.Type)).ToArray(),
            scene.NearbyProps.Select(p => new NearbyPropSnapshot(p.Name, p.Type)).ToArray(),
            scene
                .Room?.Exits.Select(e => new NearbyExitSnapshot(
                    e.Description,
                    e.DestinationRoomName
                ))
                .ToArray()
                ?? []
        );
    }
}
