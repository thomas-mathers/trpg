using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Helpers;

public sealed class FakeChatClient : IChatClient
{
    private static int _nameCounter;

    public string ChatResponseText { get; set; } = "You look around. What do you want to do next?";

    public string? PendingToolCallName { get; set; }
    public string? TextBeforeToolCall { get; set; }
    public IDictionary<string, object?>? PendingToolCallArguments { get; set; }

    // Overrides the canned content-stage quest-chain response below, for tests that need to script
    // a specific multi-node/multi-objective-type set of quest content (or a deliberately invalid
    // one, to exercise the generator's retry-exhaustion failure path). The story and block-graph
    // stages that precede content generation always get a minimal two-node (IncitingLead + Finale)
    // canned graph, so an override here must supply exactly two content nodes.
    internal QuestChainContentSchema? QuestChainContentSchemaOverride { get; set; }

    // Overrides the canned two-block (IncitingLead + Finale) graph below, for tests whose content
    // override needs a differently shaped skeleton (e.g. a FactDisclosure block).
    internal QuestChainBlockGraphSchema? QuestChainBlockGraphSchemaOverride { get; set; }

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
            if (TextBeforeToolCall != null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, TextBeforeToolCall);
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [ToolCall()]);
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, BuildResponseText(messageList));
    }

    private FunctionCallContent ToolCall() =>
        new("fake-call-1", PendingToolCallName!, PendingToolCallArguments);

    private static bool HasFunctionResult(List<ChatMessage> messages)
    {
        var lastUserIndex = messages.FindLastIndex(m => m.Role == ChatRole.User);
        var currentTurnMessages = lastUserIndex >= 0 ? messages.Skip(lastUserIndex + 1) : messages;
        return currentTurnMessages.Any(m => m.Contents.OfType<FunctionResultContent>().Any());
    }

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

        if (text.Contains("compact complete RPG quest story", StringComparison.OrdinalIgnoreCase))
        {
            var chapterCount = ExtractCount(text, @"Chapter count: (\d+)");
            var schema = new QuestChainStorySchema
            {
                Summary = "A fake story summary.",
                Chapters = Enumerable
                    .Range(0, chapterCount)
                    .Select(_ => new QuestChainStoryChapterSchema
                    {
                        Description = "A fake chapter.",
                        Turns = ["A fake turn.", "Another fake turn."],
                    })
                    .ToList(),
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("named quest blocks", StringComparison.OrdinalIgnoreCase))
        {
            if (QuestChainBlockGraphSchemaOverride != null)
            {
                return JsonSerializer.Serialize(QuestChainBlockGraphSchemaOverride);
            }

            // Otherwise the minimal feasible graph: a Finale requires one open thread, and only
            // IncitingLead can open one from nothing, so every test-sized chain gets exactly these
            // two nodes regardless of the requested node budget.
            var schema = new QuestChainBlockGraphSchema
            {
                Blocks =
                [
                    new QuestChainBlockGraphBlockSchema
                    {
                        Id = "block-1",
                        BlockType = nameof(QuestChainBlockType.IncitingLead),
                        NodeCount = 1,
                        DependsOnBlockIds = [],
                    },
                    new QuestChainBlockGraphBlockSchema
                    {
                        Id = "block-2",
                        BlockType = nameof(QuestChainBlockType.Finale),
                        NodeCount = 1,
                        DependsOnBlockIds = ["block-1"],
                    },
                ],
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("fixed text RPG quest graph", StringComparison.OrdinalIgnoreCase))
        {
            if (QuestChainContentSchemaOverride != null)
            {
                return JsonSerializer.Serialize(QuestChainContentSchemaOverride);
            }

            var entityId = ExtractFirstEntityId(text);
            var schema = new QuestChainContentSchema
            {
                Nodes = Regex
                    .Matches(text, @"- node-\d+:")
                    .Select(_ => new QuestChainContentNodeSchema
                    {
                        Name = NextName("Quest"),
                        Description = "A fake quest node.",
                        GiverEntityId = entityId,
                        Objectives =
                        [
                            new QuestChainContentObjectiveSchema
                            {
                                Name = NextName("Objective"),
                                Description = "A fake objective.",
                                ObjectiveType = nameof(GeneratedObjectiveType.KillCreature),
                                TargetEntityId = entityId,
                            },
                        ],
                    })
                    .ToList(),
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("blocking fact-disclosure quest", StringComparison.OrdinalIgnoreCase))
        {
            var entityId = ExtractFirstEntityId(text);
            var schema = new FactDisclosureRepairSchema
            {
                ReasonFact = new QuestChainContentFactSchema
                {
                    FactKey = $"fake-reason-{NextName("")}".ToLowerInvariant(),
                    Subject = "A fake reason.",
                    Value = "A fake reason value.",
                },
                SupportQuest = new QuestChainContentNodeSchema
                {
                    Name = NextName("Support Quest"),
                    Description = "A fake support quest.",
                    GiverEntityId = entityId,
                    Objectives =
                    [
                        new QuestChainContentObjectiveSchema
                        {
                            Name = NextName("Objective"),
                            Description = "A fake objective.",
                            ObjectiveType = nameof(GeneratedObjectiveType.KillCreature),
                            TargetEntityId = entityId,
                        },
                    ],
                },
            };
            return JsonSerializer.Serialize(schema);
        }

        return ChatResponseText;
    }

    private static string? ExtractFirstEntityId(string text)
    {
        var match = Regex.Match(text, @"id=([0-9a-fA-F-]{36})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int ExtractCount(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
    }

    private static string NextName(string prefix) =>
        $"{prefix}{Interlocked.Increment(ref _nameCounter)}";
}
