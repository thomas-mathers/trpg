using Microsoft.AspNetCore.SignalR;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Exceptions;

namespace TRPG.GameSessions.Filters;

internal class GameSessionNotFoundHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        var result = await next(invocationContext);
        return result is IAsyncEnumerable<string> stream ? Wrap(stream) : result;
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next
    )
    {
        try
        {
            await next(context, exception);
        }
        catch (GameSessionNotFoundException)
        {
            // The session was already ended (e.g. via the HTTP DELETE endpoint) before the
            // connection closed — nothing left to clean up here.
        }
    }

    private static async IAsyncEnumerable<string> Wrap(IAsyncEnumerable<string> inner)
    {
        await using var enumerator = inner.GetAsyncEnumerator();
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (GameSessionNotFoundException ex)
            {
                throw new HubException(ex.Message);
            }

            if (!hasNext)
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }
}
