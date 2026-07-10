using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.Application.Common.Extensions;

internal static class ChatClientExtensions
{
    internal static async Task<T> GetValidatedJson<T>(
        this IChatClient client,
        ILogger logger,
        string systemPrompt,
        string userPrompt,
        Func<T, string?>? validate = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var currentUserPrompt = userPrompt;
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var attemptStopwatch = Stopwatch.StartNew();
            List<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, currentUserPrompt),
            ];

            T? result = null;
            try
            {
                var response = await client.GetResponseAsync<T>(
                    messages,
                    cancellationToken: cancellationToken
                );
                result = response.Result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "[perf] GetValidatedJson<{Type}> failed (attempt {Attempt}) after {ElapsedMs}ms",
                    typeof(T).Name,
                    attempt + 1,
                    attemptStopwatch.ElapsedMilliseconds
                );
            }

            if (result is null)
            {
                currentUserPrompt = "The response was not valid JSON. " + userPrompt;
                continue;
            }

            var error = validate?.Invoke(result);
            if (error is not null)
            {
                logger.LogWarning(
                    "[perf] Validation failed (attempt {Attempt}) after {ElapsedMs}ms: {Error}",
                    attempt + 1,
                    attemptStopwatch.ElapsedMilliseconds,
                    error
                );
                currentUserPrompt = error + " " + userPrompt;
                continue;
            }

            logger.LogInformation(
                "[perf] GetValidatedJson<{Type}> succeeded on attempt {Attempt} in {ElapsedMs}ms (total {TotalMs}ms)",
                typeof(T).Name,
                attempt + 1,
                attemptStopwatch.ElapsedMilliseconds,
                stopwatch.ElapsedMilliseconds
            );
            return result;
        }

        logger.LogError(
            "[perf] Failed to generate valid JSON for {Type} after 5 attempts in {ElapsedMs}ms",
            typeof(T).Name,
            stopwatch.ElapsedMilliseconds
        );
        throw new InvalidOperationException(
            $"Failed to generate valid JSON for {typeof(T).Name} after 5 attempts."
        );
    }
}
