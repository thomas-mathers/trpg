namespace TRPG.Application.WorldGeneration.Generators;

public class QuestChainGlobalGraphPipeline(
    QuestChainStoryBibleGenerator storyBibleGenerator,
    QuestChainBlockGraphComposer graphComposer,
    QuestChainCastingGenerator castingGenerator,
    QuestChainContentGenerator contentGenerator
)
{
    private const int MaximumContentSliceNodeCount = 8;

    public async Task<QuestChainGeneratedResult> Generate(
        QuestChainGeneratorInput input,
        CancellationToken cancellationToken = default
    )
    {
        var entityNamesById = input.AvailableEntities.ToDictionary(
            entity => entity.Id,
            entity => entity.Name
        );
        var graph = graphComposer.Compose(input.MinimumChainLength, input.MaximumChainLength);
        var skeleton = QuestChainBlockGraphStitcher.Stitch(graph);
        var cast = castingGenerator.Cast(skeleton, input);
        var storyBible = await storyBibleGenerator.Generate(
            input.ChainPremise,
            cast.AntagonistFactionName,
            cancellationToken
        );
        var prerequisitesByNodeId = skeleton.ToDictionary(
            node => node.NodeId,
            node => node.PrerequisiteNodeIds
        );
        var results = new List<QuestChainGeneratedResult>();
        foreach (var slice in CreateSlices(graph, skeleton))
        {
            var sliceNodeIds = slice.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
            var contentSlice = slice
                .Select(node =>
                    node with
                    {
                        PrerequisiteNodeIds = node
                            .PrerequisiteNodeIds.Where(sliceNodeIds.Contains)
                            .ToArray(),
                    }
                )
                .ToArray();
            var content = await contentGenerator.Generate(
                new QuestChainGeneratorInput
                {
                    ChainPremise = $"{input.ChainPremise}\nStory bible: {storyBible}",
                    MinimumChainLength = slice.Count,
                    MaximumChainLength = slice.Count,
                    AvailableEntities = input.AvailableEntities,
                },
                contentSlice,
                slice.Last().BlockType
                    is QuestChainBlockType.Finale
                        or QuestChainBlockType.EpilogueHook
                    ? QuestChainBlockGenerationScope.ConcludesChain
                    : QuestChainBlockGenerationScope.ContinuesChain,
                ExtractCommittedEntities(results, entityNamesById),
                cast,
                cancellationToken
            );
            results.Add(
                new QuestChainGeneratedResult(
                    content.Facts,
                    content
                        .Nodes.Select(node =>
                            node with
                            {
                                PrerequisiteNodeIds = prerequisitesByNodeId[node.NodeId],
                            }
                        )
                        .ToArray()
                )
            );
        }
        return new QuestChainGeneratedResult(
            results.SelectMany(result => result.Facts).ToArray(),
            results.SelectMany(result => result.Nodes).ToArray(),
            cast.GiverFactionId,
            cast.AntagonistFactionId
        );
    }

    private static IReadOnlyList<QuestChainCommittedEntity> ExtractCommittedEntities(
        IReadOnlyList<QuestChainGeneratedResult> priorResults,
        IReadOnlyDictionary<Guid, string> entityNamesById
    )
    {
        var priorNodes = priorResults.SelectMany(result => result.Nodes).ToArray();
        var objectiveCommitments = priorNodes.SelectMany(node =>
            node.Objectives.SelectMany(objective =>
                new[] { objective.TargetEntityId, objective.RecipientEntityId }
                    .Where(entityId => entityId != null)
                    .Select(entityId => new QuestChainCommittedEntity(
                        entityId!.Value,
                        entityNamesById.GetValueOrDefault(
                            entityId.Value,
                            entityId.Value.ToString()
                        ),
                        objective.ObjectiveType.ToString(),
                        node.Name
                    ))
            )
        );
        var giverCommitments = priorNodes.Select(node => new QuestChainCommittedEntity(
            node.GiverEntityId,
            entityNamesById.GetValueOrDefault(node.GiverEntityId, node.GiverEntityId.ToString()),
            "Giver",
            node.Name
        ));

        return objectiveCommitments
            .Concat(giverCommitments)
            .DistinctBy(entity => (entity.EntityId, entity.Role))
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<QuestChainNodeSkeleton>> CreateSlices(
        QuestChainBlockGraph graph,
        IReadOnlyList<QuestChainNodeSkeleton> skeleton
    )
    {
        var slices = new List<IReadOnlyList<QuestChainNodeSkeleton>>();
        var current = new List<QuestChainNodeSkeleton>();
        var nextNodeIndex = 0;
        foreach (var block in graph.Blocks)
        {
            var nodes = skeleton.Skip(nextNodeIndex).Take(block.NodeCount).ToArray();
            nextNodeIndex += nodes.Length;
            if (current.Count > 0 && current.Count + nodes.Length > MaximumContentSliceNodeCount)
            {
                slices.Add(current.ToArray());
                current = [];
            }
            current.AddRange(nodes);
        }
        if (current.Count > 0)
            slices.Add(current.ToArray());
        return slices;
    }
}
