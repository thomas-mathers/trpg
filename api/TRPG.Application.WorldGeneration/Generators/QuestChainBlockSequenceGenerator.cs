using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Extensions;

namespace TRPG.Application.WorldGeneration.Generators;

internal class BlockSequenceSchema
{
    public List<BlockSelectionSchema> Blocks { get; init; } = [];
}

internal class BlockSelectionSchema
{
    public string Type { get; init; } = "";
    public int NodeCount { get; init; }
}

public class QuestChainBlockSequenceGenerator(
    [FromKeyedServices(LlmRoleKeys.QuestGeneration)] IChatClient client,
    ILogger<QuestChainBlockSequenceGenerator> logger
)
{
    public async Task<IReadOnlyList<QuestChainBlockSelection>> Generate(
        string chainPremise,
        int nodeBudget,
        QuestChainBlockGenerationScope scope,
        CancellationToken cancellationToken = default
    )
    {
        var terminalInstruction =
            scope == QuestChainBlockGenerationScope.ConcludesChain
                ? "End with exactly one Finale or EpilogueHook."
                : "Do not use Finale or EpilogueHook. End with one or more open threads for the next chapter to continue.";
        var systemPrompt = $$"""
            You choose a structural block sequence for a text RPG quest chain. You do not write
            quest prose, NPCs, objectives, facts, rewards, or graph edges.

            The chain has exactly {{nodeBudget}} quest nodes. Choose only from these blocks:
            - IncitingLead: exactly 1 node. It opens the story and must be first.
            - Investigation: 1 to 3 nodes. It advances one open story thread.
            - FactDisclosure: exactly 2 nodes. It creates a fact request and an independent
              supporting quest, which a later Finale must join.
            - ExclusiveApproach: exactly 2 nodes. It creates two alternative approaches; completing
              one will close the other, and a later Finale can accept either.
            - Escalation: 1 to 2 nodes. It raises the threat or urgency on one open story thread.
            - Reversal: 1 to 2 nodes. It changes the player's understanding on one open story thread.
            - Favor: exactly 1 node. It earns a person's aid while advancing one open story thread.
            - ParallelThreads: exactly 2 nodes. It opens two required, simultaneous leads which a
              later terminal block must join.
            - Finale: exactly 1 node. It is last and closes every open thread.
            - EpilogueHook: exactly 1 node. It is last, closes every open thread, and sets up a
              future story instead of resolving the current threat.

            A sequence starts with no open thread. IncitingLead requires none and creates one.
            Investigation, FactDisclosure, ExclusiveApproach, Escalation, Reversal, Favor, and
            ParallelThreads each require exactly one open thread. FactDisclosure and ParallelThreads
            leave two threads. Finale and EpilogueHook require one or more open threads and leave
            none. The selected blocks must consume exactly the node budget.

            {{terminalInstruction}}

            For an eight-node continuing chapter, the JSON shape can be:
            {"blocks":[{"type":"IncitingLead","nodeCount":1},{"type":"Investigation","nodeCount":3},{"type":"Reversal","nodeCount":2},{"type":"ParallelThreads","nodeCount":2}]}

            For an eight-node concluding chapter with a fact disclosure, the JSON shape can be:
            {"blocks":[{"type":"IncitingLead","nodeCount":1},{"type":"Investigation","nodeCount":3},{"type":"Favor","nodeCount":1},{"type":"FactDisclosure","nodeCount":2},{"type":"Finale","nodeCount":1}]}

            Respond with raw JSON only.
            """;
        var userPrompt = $"""
            Premise: {chainPremise}
            Node budget: {nodeBudget}
            """;

        var schema = await client.GetValidatedJson<BlockSequenceSchema>(
            logger,
            systemPrompt,
            userPrompt,
            schema => Validate(schema, nodeBudget, scope),
            cancellationToken,
            options: new ChatOptions { MaxOutputTokens = 1024 }
        );
        return schema
            .Blocks.Select(selection => new QuestChainBlockSelection(
                Enum.Parse<QuestChainBlockType>(selection.Type),
                selection.NodeCount
            ))
            .ToArray();
    }

    internal static string? Validate(
        BlockSequenceSchema schema,
        int nodeBudget,
        QuestChainBlockGenerationScope scope
    )
    {
        if (schema.Blocks.Count == 0)
        {
            return "Choose at least one block.";
        }

        foreach (var selection in schema.Blocks)
        {
            if (!Enum.TryParse<QuestChainBlockType>(selection.Type, out _))
            {
                return $"Unknown block type \"{selection.Type}\".";
            }
        }

        var selections = schema
            .Blocks.Select(selection => new QuestChainBlockSelection(
                Enum.Parse<QuestChainBlockType>(selection.Type),
                selection.NodeCount
            ))
            .ToArray();
        return QuestChainBlockSequenceValidator.Validate(selections, nodeBudget, scope);
    }
}
