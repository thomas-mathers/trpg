using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace TRPG.Extensions;

internal class AiClient(OllamaApiClient client, ILoggerFactory loggerFactory) {
    internal AiChat CreateChat<T>(string systemPrompt) {
        var chat = new Chat(client);
        chat.Messages.Add(new Message(ChatRole.System, systemPrompt));
        return new AiChat(chat, loggerFactory.CreateLogger<T>());
    }
}

internal class AiChat(Chat chat, ILogger logger) {
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    internal async Task<T> GetJson<T>(string prompt, CancellationToken cancellationToken) where T : new() {
        for (var attempt = 0; attempt < 3; attempt++) {
            var attemptPrompt = attempt == 0 ? prompt : "The response was not valid JSON. Try again: " + prompt;

            var sb = new StringBuilder();
            await foreach (var token in chat.SendAsync(attemptPrompt, cancellationToken)) {
                sb.Append(token);
            }

            var response = sb.ToString();

            logger.LogTrace("[User] {Prompt}", attemptPrompt);
            logger.LogTrace("[Assistant] {Response}", response);

            var json = ExtractJson(response);
            try {
                var result = JsonSerializer.Deserialize<T>(json, DeserializeOptions);
                if (result is not null) {
                    return result;
                }
            }
            catch (JsonException ex) {
                logger.LogWarning(ex, "Deserialization failed (attempt {Attempt}). Raw response: {Response}", attempt + 1, response);
            }
        }

        return new T();
    }

    private static string ExtractJson(string response) {
        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal)) {
            var start = trimmed.IndexOf('\n', StringComparison.Ordinal) + 1;
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start) {
                trimmed = trimmed[start..end].Trim();
            }
        }

        var lastClose = trimmed.LastIndexOf('}');
        if (lastClose >= 0) {
            var depth = 0;
            for (var i = lastClose; i >= 0; i--) {
                if (trimmed[i] == '}') {
                    depth++;
                }
                else if (trimmed[i] == '{') {
                    if (--depth == 0) {
                        return trimmed[i..(lastClose + 1)];
                    }
                }
            }
        }

        return trimmed;
    }
}