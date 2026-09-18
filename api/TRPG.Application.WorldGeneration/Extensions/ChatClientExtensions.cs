using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.Application.WorldGeneration.Extensions;

internal static class ChatClientExtensions
{
    internal static async Task<T> GetValidatedJson<T>(
        this IChatClient client,
        ILogger logger,
        string systemPrompt,
        string userPrompt,
        Func<T, string?>? validate = null,
        CancellationToken cancellationToken = default,
        ChatOptions? options = null
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
                    options: options,
                    cancellationToken: cancellationToken
                );
                result = ParseResult(response);
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
                currentUserPrompt =
                    "The response was not valid JSON. Respond with only the raw JSON — no markdown code fences, no commentary. "
                    + userPrompt;
                continue;
            }

            var error = validate?.Invoke(result);
            if (error is not null)
            {
                logger.LogWarning(
                    "[perf] Validation failed (attempt {Attempt}) after {ElapsedMs}ms: {Error}. Generated JSON: {GeneratedJson}",
                    attempt + 1,
                    attemptStopwatch.ElapsedMilliseconds,
                    error,
                    JsonSerializer.Serialize(result, AIJsonUtilities.DefaultOptions)
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

    // response.Result throws InvalidOperationException ("did not contain JSON to be deserialized")
    // when it can't find any JSON at all, and JsonException when it finds malformed JSON — both mean
    // "fall back to manually locating the outermost {}/[] in the raw text," not "give up."
    private static T? ParseResult<T>(ChatResponse<T> response)
        where T : class
    {
        try
        {
            return response.Result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var text = response.Text;
            var start = text.IndexOfAny(['{', '[']);
            var end = text.LastIndexOfAny(['}', ']']);
            if (start < 0 || end <= start)
            {
                throw;
            }

            return JsonSerializer.Deserialize<T>(
                text[start..(end + 1)],
                AIJsonUtilities.DefaultOptions
            );
        }
    }
}
