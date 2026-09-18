namespace TRPG.Application.Quests;

internal static class QuestExclusiveGroupEvaluator
{
    internal static bool ArePrerequisitesSatisfied(
        IReadOnlyCollection<Guid> prerequisiteQuestIds,
        IReadOnlyDictionary<Guid, Guid?> exclusiveGroupIdByPrerequisiteQuestId,
        IReadOnlySet<Guid> completedQuestIds
    ) =>
        prerequisiteQuestIds
            .GroupBy(questId =>
                exclusiveGroupIdByPrerequisiteQuestId.GetValueOrDefault(questId) ?? questId
            )
            .All(group => group.Any(completedQuestIds.Contains));

    internal static bool IsClosedBySiblingCompletion(
        Guid questId,
        Guid? exclusiveGroupId,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> questIdsByExclusiveGroupId,
        IReadOnlySet<Guid> completedQuestIds
    ) =>
        exclusiveGroupId is { } groupId
        && questIdsByExclusiveGroupId.TryGetValue(groupId, out var siblingQuestIds)
        && siblingQuestIds.Any(siblingQuestId =>
            siblingQuestId != questId && completedQuestIds.Contains(siblingQuestId)
        );
}
