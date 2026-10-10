using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class CreatureTravelNodeAssigner
{
    internal static void Assign(
        IEnumerable<Creature> creatures,
        IReadOnlyCollection<TravelNode> travelNodes
    )
    {
        foreach (var creature in creatures)
        {
            creature.CurrentTravelNodeId = travelNodes
                .Where(node => node.LocationId == creature.LocationId)
                .MinBy(node => Distance(creature, node))
                ?.Id;
        }
    }

    private static double Distance(Creature creature, TravelNode node) =>
        Math.Sqrt(
            Math.Pow(node.Position.X - creature.X, 2) + Math.Pow(node.Position.Y - creature.Y, 2)
        );
}
