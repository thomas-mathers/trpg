using Microsoft.Extensions.Logging;

namespace TRPG.Application.WorldGeneration.Generators;

public class QuestChainBlockBasedGenerator(
    QuestChainBlockSequenceGenerator blockSequenceGenerator,
    QuestChainContentGenerator contentGenerator,
    ILogger<QuestChainBlockBasedGenerator> logger
)
{
    public Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken = default
    ) => Generate(input, QuestChainBlockGenerationScope.ConcludesChain, cancellationToken);

    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        QuestChainBlockGenerationScope scope,
        CancellationToken cancellationToken = default
    )
    {
        var blocks = await blockSequenceGenerator.Generate(
            input.ChainPremise,
            input.ChainLength,
            scope,
            cancellationToken
        );
        logger.LogInformation(
            "Generated {Scope} quest-chain blocks: {Blocks}",
            scope,
            string.Join(", ", blocks.Select(block => $"{block.Type} ({block.NodeCount})"))
        );
        var skeleton = QuestChainStitcher.Stitch(blocks);
        return await contentGenerator.Generate(input, skeleton, scope, cancellationToken);
    }
}
