using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using TRPG.Application.Worlds.Generators;

namespace TRPG.Tests.Helpers;

public sealed class FakeChatClient : IChatClient
{
    private static int _nameCounter;

    public string ChatResponseText { get; set; } = "You look around. What do you want to do next?";

    public string? PendingToolCallName { get; set; }
    public IDictionary<string, object?>? PendingToolCallArguments { get; set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var messageList = messages.ToList();
        if (PendingToolCallName != null && !HasFunctionResult(messageList))
        {
            return Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, [ToolCall()]))
            );
        }

        var text = BuildResponseText(messageList);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await Task.Yield();
        var messageList = messages.ToList();

        if (PendingToolCallName != null && !HasFunctionResult(messageList))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [ToolCall()]);
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, BuildResponseText(messageList));
    }

    private FunctionCallContent ToolCall() =>
        new("fake-call-1", PendingToolCallName!, PendingToolCallArguments);

    private static bool HasFunctionResult(IEnumerable<ChatMessage> messages) =>
        messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any());

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private string BuildResponseText(IEnumerable<ChatMessage> messages)
    {
        var text = string.Join(" ", messages.Select(m => m.Text));

        if (text.Contains("unique factions", StringComparison.OrdinalIgnoreCase))
        {
            var count = ExtractCount(text, @"Generate (\d+) unique factions");
            var schema = new FactionListSchema
            {
                Factions = Enumerable
                    .Range(0, count)
                    .Select(_ => new FactionItemSchema
                    {
                        Name = NextName("Faction"),
                        Description = "A fake faction.",
                    })
                    .ToList(),
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("Cities array containing exactly", StringComparison.OrdinalIgnoreCase))
        {
            var count = ExtractCount(text, @"Cities array containing exactly (\d+)");
            var schema = new CityDescriptionListSchema
            {
                Cities = Enumerable
                    .Range(1, count)
                    .Select(i => new CityDescriptionItemSchema
                    {
                        Index = i,
                        Description = "A fake city.",
                    })
                    .ToList(),
            };
            return JsonSerializer.Serialize(schema);
        }

        if (
            text.Contains("Generate the world", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Generate country", StringComparison.OrdinalIgnoreCase)
        )
        {
            var entity = new GeographyEntitySchema
            {
                Name = NextName("Entity"),
                Description = "A fake entity.",
            };
            return JsonSerializer.Serialize(entity);
        }

        return ChatResponseText;
    }

    private static int ExtractCount(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
    }

    private static string NextName(string prefix) =>
        $"{prefix}{Interlocked.Increment(ref _nameCounter)}";
}
