using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class HouseBedroomPacker
{
    internal const int BedsPerBedroom = 2;
    private const string BedroomNamePrefix = "Bedroom";

    internal static int MaximumBedrooms { get; } =
        BuildingTemplateCatalog
            .GetTemplates(BuildingType.House)
            .Max(template =>
                template.Floors.Sum(floor =>
                    floor.Rooms.Count(room =>
                        room.Name.StartsWith(BedroomNamePrefix, StringComparison.Ordinal)
                    )
                )
            );

    internal static int MaximumHouseholdSize => MaximumBedrooms * BedsPerBedroom;

    internal static IReadOnlyList<IReadOnlyList<Guid>> Pack(
        IReadOnlyList<IReadOnlyList<Guid>> preferredGroups
    )
    {
        if (preferredGroups.Any(group => group.Count > BedsPerBedroom))
        {
            throw new InvalidOperationException(
                $"A bedroom group has more than {BedsPerBedroom} occupants."
            );
        }

        if (preferredGroups.Count <= MaximumBedrooms)
        {
            return preferredGroups;
        }

        var bedrooms = preferredGroups
            .Take(MaximumBedrooms)
            .Select(group => group.ToList())
            .ToList();

        foreach (var occupantId in preferredGroups.Skip(MaximumBedrooms).SelectMany(group => group))
        {
            var bedroom =
                bedrooms.Find(candidate => candidate.Count < BedsPerBedroom)
                ?? throw new InvalidOperationException(
                    $"A house holds at most {MaximumHouseholdSize} residents."
                );
            bedroom.Add(occupantId);
        }

        return bedrooms;
    }
}
