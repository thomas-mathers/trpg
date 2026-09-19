using Microsoft.Extensions.Logging;

namespace TRPG.Application.WorldGeneration.Generators;

public class QuestChainTreatmentFirstGenerator(QuestChainGlobalGraphPipeline globalGraphPipeline)
{
    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken = default
    )
    {
        return await globalGraphPipeline.Generate(input, cancellationToken);
    }
}
