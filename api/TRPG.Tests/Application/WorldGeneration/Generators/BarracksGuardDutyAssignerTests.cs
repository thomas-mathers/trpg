using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BarracksGuardDutyAssignerTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _cityFactionId = Guid.NewGuid();
    private readonly Guid _groundFloorLocationId = Guid.NewGuid();
    private readonly Guid _gateLocationId = Guid.NewGuid();
    private readonly Guid _waypointA = Guid.NewGuid();
    private readonly Guid _waypointB = Guid.NewGuid();
    private readonly Guid _waypointC = Guid.NewGuid();

    private (IReadOnlyList<Creature> Guards, BarracksGuardDutyAssignerResult Result) Generate(
        int guardCount
    )
    {
        var guards = Enumerable
            .Range(0, guardCount)
            .Select(_ => Builders.MakeCreature(_worldId))
            .ToList();
        var beds = guards
            .Select(guard => new Bed
            {
                WorldId = _worldId,
                LocationId = Guid.NewGuid(),
                AssignedCreatureId = guard.Id,
            })
            .ToList();
        var districts = new[]
        {
            Builders.MakeDistrict(Guid.NewGuid(), worldId: _worldId, locationId: _waypointA),
            Builders.MakeDistrict(
                Guid.NewGuid(),
                DistrictType.Residential,
                worldId: _worldId,
                locationId: _waypointB
            ),
            Builders.MakeDistrict(
                Guid.NewGuid(),
                DistrictType.Scientific,
                worldId: _worldId,
                locationId: _waypointC
            ),
        };
        var connectors = new[]
        {
            Connector(_waypointA, _waypointB),
            Connector(_waypointB, _waypointA),
            Connector(_waypointA, _waypointC),
            Connector(_waypointC, _waypointA),
        };

        var result = BarracksGuardDutyAssigner.Generate(
            new BarracksGuardDutyAssignerInput(
                _worldId,
                "Test City",
                _cityFactionId,
                _groundFloorLocationId,
                _gateLocationId,
                districts,
                connectors,
                connectors
                    .Select(connector => new TravelConnector
                    {
                        WorldId = _worldId,
                        ConnectorId = connector.Id,
                        Distance = 15,
                    })
                    .ToArray(),
                PatrolDwellHours: 0.5,
                beds,
                guards
            )
        );

        return (guards, result);
    }

    [Fact]
    public void Generate_EveryGuardBecomesACityFactionMember()
    {
        // Act
        var (guards, result) = Generate(7);

        // Assert
        Assert.Equal(
            guards.Select(g => g.Id).ToHashSet(),
            result
                .FactionMembers.Where(fm =>
                    fm.FactionId == _cityFactionId && fm.Role == FactionRole.Member
                )
                .Select(fm => fm.CreatureId)
                .ToHashSet()
        );
    }

    [Fact]
    public void Generate_PlacesEveryGuardAtTheGroundFloor()
    {
        // Act
        var (guards, _) = Generate(7);

        // Assert
        Assert.All(guards, guard => Assert.Equal(_groundFloorLocationId, guard.LocationId));
    }

    [Fact]
    public void Generate_OfficerGetsNoIdleJob()
    {
        // Act
        var (guards, result) = Generate(7);

        // Assert — guards have no household, so unlike shop staff they never get an Idle job
        var officerJobs = result.Jobs.Where(j => j.CreatureId == guards[0].Id);
        Assert.DoesNotContain(officerJobs, j => j.Action == CreatureJobAction.Idle);
    }

    [Fact]
    public void Generate_DayGateGuard_WorksTheGateDuringDayShift()
    {
        // Act
        var (guards, result) = Generate(7);

        // Assert
        var workJob = result.Jobs.Single(j =>
            j.CreatureId == guards[1].Id && j.Action == CreatureJobAction.Work
        );
        Assert.Equal(_gateLocationId, workJob.LocationId);
        Assert.Equal(6, workJob.StartHour);
        Assert.Equal(18, workJob.EndHour);
    }

    [Fact]
    public void Generate_NightGateGuard_WorksTheGateDuringNightShift()
    {
        // Act
        var (guards, result) = Generate(7);

        // Assert
        var workJob = result.Jobs.Single(j =>
            j.CreatureId == guards[2].Id && j.Action == CreatureJobAction.Work
        );
        Assert.Equal(_gateLocationId, workJob.LocationId);
        Assert.Equal(18, workJob.StartHour);
        Assert.Equal(6, workJob.EndHour);
    }

    [Fact]
    public void Generate_DayPatrolGuardsRotateThroughDifferentFirstWaypoints()
    {
        // Act — guards 3 and 4 both patrol by day, offset by one rotation step
        var (guards, result) = Generate(7);

        // Assert
        var guard3FirstStop = result
            .Jobs.Where(j => j.CreatureId == guards[3].Id && j.Action == CreatureJobAction.Work)
            .OrderBy(j => j.StartHour)
            .First();
        var guard4FirstStop = result
            .Jobs.Where(j => j.CreatureId == guards[4].Id && j.Action == CreatureJobAction.Work)
            .OrderBy(j => j.StartHour)
            .First();
        Assert.NotEqual(guard3FirstStop.LocationId, guard4FirstStop.LocationId);
    }

    [Fact]
    public void Generate_PatrolRoutesVisitAndLingerInEveryDistrict()
    {
        var (_, result) = Generate(7);

        Assert.NotEmpty(result.Routes);
        foreach (var route in result.Routes)
        {
            var steps = result.RouteSteps.Where(step => step.RouteId == route.Id).ToArray();
            Assert.Equal(RouteTraversal.Cyclic, route.Traversal);
            Assert.Equal(
                new HashSet<Guid> { _waypointA, _waypointB, _waypointC },
                steps.Where(step => step.DwellHours > 0).Select(step => step.LocationId).ToHashSet()
            );
        }
    }

    private LocationConnector Connector(Guid originLocationId, Guid destinationLocationId) =>
        new()
        {
            WorldId = _worldId,
            OriginLocationId = originLocationId,
            DestinationLocationId = destinationLocationId,
            DestinationLabel = "District",
        };
}
