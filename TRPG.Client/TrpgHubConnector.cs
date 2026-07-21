using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TRPG.Client;

internal sealed class TrpgHubConnector(HttpClient httpClient, ILoggerFactory loggerFactory)
{
    public async Task<GameHub> Connect(Guid sessionId, CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(httpClient.BaseAddress!)
        {
            Path = "/hubs/chat",
            Query = $"sessionId={sessionId}",
        }.Uri;

        var builder = new HubConnectionBuilder().WithUrl(uri).WithAutomaticReconnect();
        builder.Services.AddSingleton(loggerFactory);
        var connection = builder.Build();
        await connection.StartAsync(cancellationToken);

        return new GameHub(connection);
    }
}
