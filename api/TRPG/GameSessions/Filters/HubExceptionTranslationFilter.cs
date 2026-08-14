using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Common.Exceptions;

namespace TRPG.GameSessions.Filters;

internal class HubExceptionTranslationFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        object? result;
        try
        {
            result = await next(invocationContext);
        }
        catch (EntityNotFoundException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

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
        catch (EntityNotFoundException) { }
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
            catch (EntityNotFoundException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (InvalidOperationException ex)
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
