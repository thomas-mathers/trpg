using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum PropPlacementRule
{
    Corner,
    Wall,
    Anchor,
    Center,
    Free,
}

internal record PropFootprintSpec(
    double Width,
    double Depth,
    PropPlacementRule Rule,
    double FrontClearance
)
{
    public Footprint Footprint => new(Width, Depth);
}

internal static class PropFootprintCatalog
{
    private static readonly Dictionary<string, PropFootprintSpec> Specs = new()
    {
        ["prop.bed.basic"] = new(
            Width: 1.0,
            Depth: 2.0,
            PropPlacementRule.Corner,
            FrontClearance: 0.6
        ),
        ["prop.cell.basic"] = new(
            Width: 2.0,
            Depth: 2.0,
            PropPlacementRule.Corner,
            FrontClearance: 0.8
        ),
        ["prop.sign.basic"] = new(
            Width: 0.5,
            Depth: 0.2,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        ["prop.container.basic"] = new(
            Width: 0.8,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.container.barrel"] = new(
            Width: 0.6,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        ["prop.container.chest"] = new(
            Width: 0.9,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.container.crate"] = new(
            Width: 0.7,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        ["prop.container.footlocker"] = new(
            Width: 0.9,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.3
        ),
        ["prop.container.strongbox"] = new(
            Width: 0.8,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.container.weapon_rack"] = new(
            Width: 1.2,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.seat.basic"] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Anchor,
            FrontClearance: 0.3
        ),
        ["prop.seat.chair"] = new(
            Width: 0.5,
            Depth: 0.5,
            PropPlacementRule.Anchor,
            FrontClearance: 0.3
        ),
        ["prop.seat.pew"] = new(
            Width: 2.0,
            Depth: 0.6,
            PropPlacementRule.Center,
            FrontClearance: 0.5
        ),
        ["prop.seat.throne"] = new(
            Width: 0.9,
            Depth: 0.9,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.seat.bench"] = new(
            Width: 1.5,
            Depth: 0.5,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        ["prop.seat.stone_bench"] = new(
            Width: 1.6,
            Depth: 0.6,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        ["prop.seat.low_wall"] = new(
            Width: 2.0,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.4
        ),
        ["prop.trap.mechanical"] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        ["prop.trap.collapse"] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        ["prop.trap.slope"] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        ["prop.trap.water"] = new(
            Width: 1.5,
            Depth: 1.5,
            PropPlacementRule.Center,
            FrontClearance: 0.0
        ),
        ["prop.trigger.basic"] = new(
            Width: 0.4,
            Depth: 0.4,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        ["prop.trigger.lever"] = new(
            Width: 0.3,
            Depth: 0.3,
            PropPlacementRule.Wall,
            FrontClearance: 0.5
        ),
        ["prop.workstation.alchemy"] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.workstation.armorsmithing"] = new(
            Width: 0.8,
            Depth: 0.8,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        ["prop.workstation.carpentry"] = new(
            Width: 1.6,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.workstation.cooking"] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.workstation.enchanting"] = new(
            Width: 1.2,
            Depth: 1.2,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        ["prop.workstation.jewelcrafting"] = new(
            Width: 1.2,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.workstation.prayer"] = new(
            Width: 1.2,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.workstation.reading"] = new(
            Width: 1.0,
            Depth: 0.7,
            PropPlacementRule.Wall,
            FrontClearance: 0.6
        ),
        ["prop.workstation.tailoring"] = new(
            Width: 1.4,
            Depth: 0.8,
            PropPlacementRule.Wall,
            FrontClearance: 0.8
        ),
        ["prop.workstation.trade"] = new(
            Width: 1.8,
            Depth: 0.8,
            PropPlacementRule.Center,
            FrontClearance: 0.8
        ),
        ["prop.workstation.weaponsmithing"] = new(
            Width: 1.4,
            Depth: 1.2,
            PropPlacementRule.Wall,
            FrontClearance: 1.0
        ),
    };

    internal static IReadOnlyCollection<string> Keys => Specs.Keys;

    internal static bool Contains(string assetKey) => Specs.ContainsKey(assetKey);

    internal static PropFootprintSpec Get(string assetKey) =>
        Specs.TryGetValue(assetKey, out var spec)
            ? spec
            : throw new InvalidOperationException($"No footprint is cataloged for '{assetKey}'.");
}
