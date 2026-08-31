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

        var result = BarracksGuardDutyAssigner.Generate(
            new BarracksGuardDutyAssignerInput(
                _worldId,
                _cityFactionId,
                _groundFloorLocationId,
                _gateLocationId,
                [_waypointA, _waypointB, _waypointC],
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
}
