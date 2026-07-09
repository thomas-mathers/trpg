using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;

namespace TRPG.Extensions;

internal static class OllamaExtensions
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly RequestOptions DefaultOptions = new() { MinP = 0.1f };

    internal static async Task<T> GetJson<T>(
        this IOllamaApiClient client,
        ILogger logger,
        string systemPrompt,
        string userPrompt,
        Func<T, string?>? validate = null,
        CancellationToken cancellationToken = default
    )
        where T : new()
    {
        logger.LogTrace("[System] {SystemPrompt}", systemPrompt);

        var currentPrompt = userPrompt;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var sb = new StringBuilder();
            var request = new GenerateRequest
            {
                System = systemPrompt,
                Prompt = currentPrompt,
                Format = "json",
                Options = DefaultOptions,
            };
            await foreach (var chunk in client.GenerateAsync(request, cancellationToken))
            {
                sb.Append(chunk?.Response);
            }

            var response = sb.ToString();
            logger.LogTrace("[User] {Prompt}", currentPrompt);
            logger.LogTrace("[Assistant] {Response}", response);

            var json = ExtractJson(response);

            T? result = default;
            try
            {
                result = JsonSerializer.Deserialize<T>(json, DeserializeOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "Deserialization failed (attempt {Attempt}). Raw response: {Response}",
                    attempt + 1,
                    response
                );
            }

            if (result is null)
            {
                currentPrompt = "The response was not valid JSON. " + userPrompt;
                continue;
            }

            var error = validate?.Invoke(result);
            if (error is not null)
            {
                logger.LogWarning(
                    "Validation failed (attempt {Attempt}): {Error}",
                    attempt + 1,
                    error
                );
                currentPrompt = error + " " + userPrompt;
                continue;
            }

            return result;
        }

        throw new InvalidOperationException(
            $"Failed to generate valid JSON for {typeof(T).Name} after 5 attempts."
        );
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('\n', StringComparison.Ordinal) + 1;
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start)
            {
                trimmed = trimmed[start..end].Trim();
            }
        }

        var lastClose = trimmed.LastIndexOf('}');
        if (lastClose >= 0)
        {
            var depth = 0;
            for (var i = lastClose; i >= 0; i--)
            {
                if (trimmed[i] == '}')
                {
                    depth++;
                }
                else if (trimmed[i] == '{')
                {
                    if (--depth == 0)
                    {
                        return trimmed[i..(lastClose + 1)];
                    }
                }
            }
        }

        return trimmed;
    }
}
