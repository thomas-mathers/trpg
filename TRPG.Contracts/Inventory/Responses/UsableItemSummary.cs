using System.ComponentModel;

namespace TRPG.Contracts.Inventory.Responses;

public enum ResourceType
{
    [Description("HP")]
    Hp,
    [Description("AP")]
    Ap,
    [Description("MP")]
    Mp,
}

public record UsableItemSummary(string Name, ResourceType Resource, int Amount);
