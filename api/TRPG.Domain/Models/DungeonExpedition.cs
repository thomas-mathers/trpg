namespace TRPG.Domain.Models;

public class DungeonExpedition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid BuildingId { get; init; }
    public Guid SurvivorId { get; init; }
    public Guid CompanionId { get; init; }
    public Guid EntranceLocationId { get; init; }
    public Guid CompanionLocationId { get; init; }
    public Guid JournalWorkId { get; init; }
    public Guid JournalItemId { get; init; }
    public Guid DiscoverySecretId { get; init; }
    public string SurvivorName { get; init; } = "";
    public string CompanionName { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string Separation { get; init; } = "";
    public string FinalExperience { get; init; } = "";
    public string Discovery { get; init; } = "";
    public List<Guid> KnownRouteLocationIds { get; init; } = [];
}
