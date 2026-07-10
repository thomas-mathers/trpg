using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Queries;
using TRPG.Contracts;

namespace TRPG.Hubs;

internal sealed class ChatHub(
    IServiceProvider serviceProvider,
    GameSessionStateStore sessionStore
) : Hub
{
    private const string SessionIdKey = "SessionId";

    public override Task OnConnectedAsync()
    {
        var sessionId = GetSessionIdFromQuery();
        Context.Items[SessionIdKey] = sessionId;
        return base.OnConnectedAsync();
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken)
    {
        var state = GetState();
        var turnRunner = ResolveTurnRunner(state);
        return StreamTurn(
            turnRunner.SendOpeningStreaming(cancellationToken),
            state,
            alwaysSendScene: true,
            cancellationToken
        );
    }

    public IAsyncEnumerable<string> SendChat(string message, CancellationToken cancellationToken)
    {
        var state = GetState();
        var turnRunner = ResolveTurnRunner(state);
        return StreamTurn(
            turnRunner.ProcessTurnStreaming(message, cancellationToken),
            state,
            alwaysSendScene: false,
            cancellationToken
        );
    }

    public IAsyncEnumerable<string> SendWait(int hours, CancellationToken cancellationToken)
    {
        var state = GetState();
        var turnRunner = ResolveTurnRunner(state);
        return StreamTurn(
            turnRunner.SendWaitStreaming(hours, cancellationToken),
            state,
            alwaysSendScene: true,
            cancellationToken
        );
    }

    private async IAsyncEnumerable<string> StreamTurn(
        IAsyncEnumerable<string> tokens,
        GameSessionState state,
        bool alwaysSendScene,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (var token in tokens.WithCancellation(cancellationToken))
        {
            yield return token;
        }

        if (
            (alwaysSendScene || state.Session.SceneRefreshedThisTurn)
            && state.Session.LastScene != null
        )
        {
            await Clients.Caller.SendAsync(
                "Scene",
                ToSnapshot(state.Session.LastScene),
                cancellationToken
            );
        }
    }

    private GameTurnRunner ResolveTurnRunner(GameSessionState state)
    {
        serviceProvider.GetRequiredService<CurrentGameSessionStateAccessor>().State = state;
        return serviceProvider.GetRequiredService<GameTurnRunner>();
    }

    private GameSessionState GetState()
    {
        var sessionId = (Guid)Context.Items[SessionIdKey]!;
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            throw new HubException($"Session {sessionId} not found.");
        }

        return state;
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
