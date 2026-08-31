using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record NpcProfileGeneratorInput(
    IReadOnlyCollection<Creature> Creatures,
    IReadOnlyDictionary<Guid, Location> LocationsById,
    IReadOnlyCollection<FactionMember> FactionMembers,
    IReadOnlyCollection<Faction> Factions,
    IReadOnlyCollection<Relationship> Relationships,
    IReadOnlyCollection<CreatureJob> Jobs,
    IReadOnlyCollection<Room> Rooms,
    IReadOnlyCollection<Building> Buildings,
    IReadOnlyCollection<BuildingOwner> BuildingOwners
);

internal static class NpcProfileGenerator
{
    private static readonly string[] Features =
    [
        "A faded burn mark covers the back of one hand.",
        "Their hands are calloused from years of hard labor.",
        "A chipped front tooth shows whenever they smile.",
        "Ink stains permanently mark their fingertips.",
    ];

    private static readonly string[] Personalities =
    [
        "Disciplined and reserved.",
        "Warm but watchful.",
        "Blunt and practical.",
        "Curious and quick to laugh.",
    ];

    private static readonly string[] SpeechStyles =
    [
        "Clipped, direct sentences.",
        "Slow, deliberate phrasing.",
        "Warm, folksy language.",
        "Precise, measured speech.",
    ];

    private static readonly string[] Hobbies =
    [
        "woodworking",
        "gardening",
        "collecting old coins",
        "fishing",
    ];

    public static NpcProfile[] Generate(NpcProfileGeneratorInput input)
    {
        var creaturesById = input.Creatures.ToDictionary(creature => creature.Id);

        var factionsById = input.Factions.ToDictionary(faction => faction.Id);

        var buildingsById = input.Buildings.ToDictionary(building => building.Id);

        var buildingIdByLocationId = input.Rooms.ToDictionary(
            room => room.LocationId,
            room => room.BuildingId
        );

        var jobsByCreatureId = input
            .Jobs.GroupBy(job => job.CreatureId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var factionsByCreatureId = input
            .FactionMembers.GroupBy(member => member.CreatureId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(member => new NpcFaction(
                            member.FactionId,
                            factionsById[member.FactionId].Name,
                            factionsById[member.FactionId].IsCityFaction
                        ))
                        .ToArray()
            );

        var familyByCreatureId = input
            .Relationships.GroupBy(relationship => relationship.SubjectId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Where(relationship => creaturesById.ContainsKey(relationship.RelativeId))
                        .Select(relationship => new NpcFamilyMember(
                            creaturesById[relationship.RelativeId].Name,
                            relationship.RelationshipType.ToString()
                        ))
                        .ToArray()
            );

        var ownedBuildingIdsByCreatureId = input
            .BuildingOwners.GroupBy(owner => owner.OwnerId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(owner => owner.BuildingId).ToHashSet()
            );

        return input
            .Creatures.Where(creature => CreatureTypes.Humanoid.Contains(creature.CreatureType))
            .Select(creature =>
                CreateProfile(
                    creature,
                    input.LocationsById,
                    jobsByCreatureId,
                    buildingIdByLocationId,
                    buildingsById,
                    factionsByCreatureId,
                    familyByCreatureId,
                    ownedBuildingIdsByCreatureId
                )
            )
            .ToArray();
    }

    private static NpcProfile CreateProfile(
        Creature creature,
        IReadOnlyDictionary<Guid, Location> locationsById,
        IReadOnlyDictionary<Guid, CreatureJob[]> jobsByCreatureId,
        IReadOnlyDictionary<Guid, Guid> buildingIdByLocationId,
        IReadOnlyDictionary<Guid, Building> buildingsById,
        IReadOnlyDictionary<Guid, NpcFaction[]> factionsByCreatureId,
        IReadOnlyDictionary<Guid, NpcFamilyMember[]> familyByCreatureId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> ownedBuildingIdsByCreatureId
    )
    {
        jobsByCreatureId.TryGetValue(creature.Id, out var jobs);

        var workJob = jobs?.FirstOrDefault(job => job.Action == CreatureJobAction.Work);

        var sleepJob = jobs?.FirstOrDefault(job => job.Action == CreatureJobAction.Sleep);

        var workBuilding = ResolveBuilding(workJob, buildingIdByLocationId, buildingsById);

        var homeBuilding = ResolveBuilding(sleepJob, buildingIdByLocationId, buildingsById);

        var daysOff =
            jobs?.Where(job => job.SpecificDay != null)
                .Select(job => job.SpecificDay!.Value.ToString())
                .Distinct()
                .ToArray()
            ?? [];

        var isOwner =
            workBuilding != null
            && ownedBuildingIdsByCreatureId
                .GetValueOrDefault(creature.Id, [])
                .Contains(workBuilding.Id);

        var profession = creature.Profession?.ToString().ToLowerInvariant() ?? "wanderer";

        return new NpcProfile
        {
            WorldId = creature.WorldId,
            CreatureId = creature.Id,
            Description =
                $"A {creature.CreatureType.ToString().ToLowerInvariant()} {profession} with a watchful bearing.",
            Appearance = new NpcAppearance
            {
                DistinguishingFeatures = [Features[Random.Shared.Next(Features.Length)]],
            },
            Behavior = new NpcBehavior
            {
                Personality = Personalities[Random.Shared.Next(Personalities.Length)],
                SpeechStyle = SpeechStyles[Random.Shared.Next(SpeechStyles.Length)],
                Hobby = Hobbies[Random.Shared.Next(Hobbies.Length)],
            },
            PrivateBackground = new NpcPrivateBackground
            {
                Origin = locationsById[creature.BirthLocationId].Name,
                Profession = creature.Profession?.ToString(),
                Factions = factionsByCreatureId.GetValueOrDefault(creature.Id, []),
                Family = familyByCreatureId.GetValueOrDefault(creature.Id, []),
                Home = homeBuilding?.Name,
                Work =
                    workBuilding == null || workJob == null
                        ? null
                        : new NpcWorkBackground
                        {
                            Building = workBuilding.Name,
                            IsOwner = isOwner,
                            Hours =
                                $"{FormatHour(workJob.StartHour)} to {FormatHour(workJob.EndHour)}",
                            DaysOff = daysOff,
                        },
            },
        };
    }

    private static Building? ResolveBuilding(
        CreatureJob? job,
        IReadOnlyDictionary<Guid, Guid> buildingIdByLocationId,
        IReadOnlyDictionary<Guid, Building> buildingsById
    ) =>
        job != null
        && buildingIdByLocationId.TryGetValue(job.LocationId, out var buildingId)
        && buildingsById.TryGetValue(buildingId, out var building)
            ? building
            : null;

    private static string FormatHour(int hour)
    {
        var displayHour = hour % 12 == 0 ? 12 : hour % 12;
        return $"{displayHour}{(hour < 12 ? "am" : "pm")}";
    }
}
