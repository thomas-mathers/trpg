using TRPG.Application.Common.Navigation;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public sealed class GeneratedJourneySeeder(CreatureGroupGenerator creatureGroupGenerator)
{
    public GeneratedJourneySeed Seed(WorldGeneratorResult world)
    {
        var graph = world.BuildTravelGraph();
        var creatures = new List<Creature>();
        var items = new List<Item>();
        var skills = new List<CreatureSkill>();
        var memberships = new List<FactionMember>();
        var circuits = new List<TravelCircuit>();
        var circuitLegs = new List<TravelCircuitLeg>();
        var journeys = new List<Journey>();
        var journeyLegs = new List<JourneyLeg>();
        var journeyMembers = new List<JourneyMember>();

        foreach (var country in world.Countries)
        {
            var stops = EntranceNodes(world, country.Id);
            var legs = BuildCircuitLegs(graph, stops);
            var capital = world.Cities.FirstOrDefault(city =>
                city.IsCapital && city.CountryId == country.Id
            );
            var faction = world.Factions.FirstOrDefault(faction =>
                faction.Kind == FactionKind.CityGuard && faction.CityId == capital?.Id
            );
            if (legs.Length == 0 || faction is null)
            {
                continue;
            }

            var circuit = new TravelCircuit
            {
                WorldId = world.World.Id,
                Name = $"{country.Name} Road Circuit",
            };
            circuits.Add(circuit);
            circuitLegs.AddRange(legs.Select((leg, index) => CircuitLeg(circuit.Id, index, leg)));
            AddJourney(Profession.Guard, 3, $"Patrolling the roads of {country.Name}.");
            AddJourney(Profession.Cleric, 1, $"Making a pilgrimage through {country.Name}.");
            AddJourney(
                Profession.Ranger,
                1,
                $"Traveling through {country.Name} in search of work, rumors, and adventure."
            );

            void AddJourney(Profession profession, int count, string purpose)
            {
                var first = legs[0];
                var node = world.TravelNodes.Single(candidate => candidate.Id == first.FromNodeId);
                var group = creatureGroupGenerator.Generate(
                    new CreatureGroupGeneratorInput(
                        country.DominantRace,
                        profession,
                        world.World.Id,
                        graph.LocationOf(first.FromNodeId),
                        count,
                        10,
                        35
                    )
                );
                var journey = new Journey
                {
                    WorldId = world.World.Id,
                    TravelCircuitId = circuit.Id,
                    Purpose = purpose,
                    ArrivalActivity = CreatureActivity.Working,
                    Status = JourneyStatus.Traveling,
                    PlannedAt = GameClock.Epoch,
                    DepartureAt = GameClock.Epoch,
                    CheckpointedAt = GameClock.Epoch,
                };
                journeys.Add(journey);
                journeyLegs.AddRange(
                    legs.Select((leg, index) => JourneyLeg(journey.Id, index, leg))
                );
                foreach (var member in group)
                {
                    member.Creature.CurrentTravelNodeId = first.FromNodeId;
                    member.Creature.X = node.Position.X;
                    member.Creature.Y = node.Position.Y;
                    creatures.Add(member.Creature);
                    items.AddRange(member.Items);
                    skills.AddRange(member.Skills);
                    memberships.Add(
                        new FactionMember
                        {
                            WorldId = world.World.Id,
                            FactionId = faction.Id,
                            CreatureId = member.Creature.Id,
                            Role = FactionRole.Member,
                        }
                    );
                    journeyMembers.Add(
                        new JourneyMember
                        {
                            JourneyId = journey.Id,
                            CreatureId = member.Creature.Id,
                        }
                    );
                }
            }
        }

        return new GeneratedJourneySeed(
            creatures,
            items,
            skills,
            memberships,
            circuits,
            circuitLegs,
            journeys,
            journeyLegs,
            journeyMembers
        );
    }

    private static Guid[] EntranceNodes(WorldGeneratorResult world, Guid countryId) =>
        world
            .Cities.Where(city => city.CountryId == countryId)
            .Join(
                world.Districts.Where(district =>
                    district.DistrictType == DistrictType.CityEntrance
                ),
                city => city.Id,
                district => district.CityId,
                (_, district) => district.LocationId
            )
            .Select(locationId =>
                world.TravelNodes.FirstOrDefault(node => node.LocationId == locationId)?.Id
            )
            .OfType<Guid>()
            .ToArray();

    private static DirectedTravelLeg[] BuildCircuitLegs(
        TravelGraph graph,
        IReadOnlyList<Guid> stops
    ) =>
        stops.Count < 2
            ? []
            : stops
                .Zip(stops.Skip(1).Append(stops[0]))
                .SelectMany(pair => graph.FindShortestPath(pair.First, pair.Second))
                .ToArray();

    private static TravelCircuitLeg CircuitLeg(Guid circuitId, int index, DirectedTravelLeg leg) =>
        new()
        {
            TravelCircuitId = circuitId,
            Index = index,
            FromNodeId = leg.FromNodeId,
            ToNodeId = leg.ToNodeId,
            ConnectorId = leg.ConnectorId,
            DwellAfter = TimeSpan.Zero,
        };

    private static JourneyLeg JourneyLeg(Guid journeyId, int index, DirectedTravelLeg leg) =>
        new()
        {
            JourneyId = journeyId,
            Index = index,
            FromNodeId = leg.FromNodeId,
            ToNodeId = leg.ToNodeId,
            ConnectorId = leg.ConnectorId,
            Distance = leg.Distance,
            Path = leg.Path,
            DwellAfter = TimeSpan.Zero,
        };
}
