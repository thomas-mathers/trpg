namespace TRPG.Domain.Models;

public abstract class QuestObjective
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? LocationId { get; init; }
    public string Name { get; init; } = "";
    public Guid QuestId { get; init; }
    public int RequiredAmount { get; init; } = 1;
    public Guid WorldId { get; init; }
}

public sealed class KillCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
}

public sealed class KillCreatureTypeObjective : QuestObjective
{
    public CreatureType CreatureType { get; init; }
}

public sealed class CollectItemObjective : QuestObjective
{
    public Guid ItemId { get; init; }
}

public sealed class ExploreLocationObjective : QuestObjective { }

public sealed class GiveItemsObjective : QuestObjective
{
    public List<Guid> ItemIds { get; init; } = [];
    public Guid RecipientId { get; init; }
}

// Fungible-material variant of GiveItemsObjective: matches any owned item sharing ItemName rather
// than a fixed set of instance ids, so gathering the same kind of item toward two independently
// seeded quests advances both.
public sealed class GiveItemKindObjective : QuestObjective
{
    public string ItemName { get; init; } = "";
    public Guid RecipientId { get; init; }
}

public sealed class FreeCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
}

public sealed class ClearLocationObjective : QuestObjective
{
    public Guid BuildingId { get; init; }
}

public sealed class DeliverItemObjective : QuestObjective
{
    public Guid ItemId { get; init; }
    public Guid RecipientId { get; init; }
}

// Supporting quests gate or increase disclosure without preventing the NPC from being talked to.
public sealed class LearnFactFromCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
    public Guid FactId { get; init; }
    public Guid? ReasonFactId { get; init; }
    public int BaseWillingness { get; init; }
    public int BribeWillingness { get; init; }
    public int IntimidationWillingness { get; init; }
    public List<Guid> RequiredSupportingQuestIds { get; init; } = [];
    public List<SupportingFactQuestWeight> WeightedSupportingQuestIds { get; init; } = [];
}

public sealed class ReportFactToCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
    public Guid FactId { get; init; }
}

public sealed class InteractWithPropObjective : QuestObjective
{
    public Guid TriggerId { get; init; }
}

public class SupportingFactQuestWeight
{
    public Guid QuestId { get; init; }
    public int Weight { get; init; }
}
