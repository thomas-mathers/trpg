using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record TradeStockGeneratorResult(
    IReadOnlyCollection<Item> Items,
    IReadOnlyCollection<RestockPolicy> RestockPolicies
);

public class TradeStockGenerator(ItemGenerator itemGenerator)
{
    // Shops restock before opening, which is a clock time rather than an elapsed one.
    private const string DefaultRestockSchedule = "0 6 * * *";

    public TradeStockGeneratorResult Generate(
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<Building> buildings,
        Guid worldId,
        int playerLevel
    )
    {
        var buildingTypesByLocation = rooms
            .Join(
                buildings,
                room => room.BuildingId,
                building => building.Id,
                (room, building) => new { room.LocationId, building.BuildingType }
            )
            .ToDictionary(x => x.LocationId, x => x.BuildingType);

        var tradeWorkstations = props
            .OfType<Workstation>()
            .Where(workstation =>
                workstation.WorkstationType == WorkstationType.Trade
                && buildingTypesByLocation.ContainsKey(workstation.LocationId)
            )
            .ToArray();

        var items = new List<Item>();
        var restockPolicies = new List<RestockPolicy>();

        foreach (var workstation in tradeWorkstations)
        {
            var buildingType = buildingTypesByLocation[workstation.LocationId];
            var fillResult = TradeStockFiller.Fill(
                itemGenerator,
                buildingType,
                currentItems: [],
                worldId,
                playerLevel
            );

            items.AddRange(
                AssignToWorkstation(
                    [
                        .. fillResult.ItemsToAdd,
                        new Gold
                        {
                            WorldId = worldId,
                            Name = "Gold",
                            Quantity = fillResult.GoldMinimum,
                        },
                    ],
                    workstation.Id
                )
            );
            restockPolicies.Add(
                new RestockPolicy
                {
                    WorldId = worldId,
                    WorkstationId = workstation.Id,
                    Schedule = DefaultRestockSchedule,
                    LastSyncPlaytime = TimeSpan.Zero,
                }
            );
        }

        return new TradeStockGeneratorResult(items, restockPolicies);
    }

    private static IReadOnlyCollection<Item> AssignToWorkstation(
        IReadOnlyCollection<Item> items,
        Guid workstationId
    )
    {
        foreach (var item in items)
        {
            item.Ownership.OwnerId = workstationId;
            item.Ownership.OwnerType = OwnerType.Workstation;
        }

        return items;
    }
}
