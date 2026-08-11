namespace TRPG.Data.Models;

public abstract class QuestObjective
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? LocationId { get; init; }
    public string Name { get; init; } = "";
    public Guid QuestId { get; init; }
    public Guid WorldId { get; init; }
}

public sealed class KillCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
}

public sealed class KillCreatureTypeObjective : QuestObjective
{
    public CreatureType CreatureType { get; init; }
    public int RequiredAmount { get; init; }
}

public sealed class CollectItemObjective : QuestObjective
{
    public Guid ItemId { get; init; }
    public int RequiredAmount { get; init; }
}

public sealed class ExploreBuildingObjective : QuestObjective
{
    public Guid BuildingId { get; init; }
}

public sealed class ExploreCityObjective : QuestObjective
{
    public Guid CityId { get; init; }
}

public sealed class SpeakToCreatureObjective : QuestObjective
{
    public Guid CreatureId { get; init; }
}
