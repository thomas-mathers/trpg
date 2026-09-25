using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public sealed class CreatureRouteScheduleGeneratorTests
{
    [Fact]
    public void Generate_CreatesWeeklyRoutesThatDepartInTimeForTheNextJob()
    {
        var worldId = Guid.NewGuid();
        var homeLocationId = Guid.NewGuid();
        var middleLocationId = Guid.NewGuid();
        var workLocationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, locationId: homeLocationId);
        creature.MovementSpeed = 5;
        var sleep = CreatureJobGenerator.GenerateSleep(
            creature.Id,
            homeLocationId,
            worldId,
            new HourWindow(0, 9)
        );
        var work = CreatureJobGenerator.GenerateWork(
            creature.Id,
            workLocationId,
            worldId,
            new HourWindow(9, 17)
        );
        var idle = CreatureJobGenerator.GenerateIdle(
            creature.Id,
            homeLocationId,
            worldId,
            new HourWindow(17, 0)
        );
        var homeToMiddle = Builders.MakeLocationConnector(
            homeLocationId,
            middleLocationId,
            worldId
        );
        var middleToWork = Builders.MakeLocationConnector(
            middleLocationId,
            workLocationId,
            worldId
        );
        var workToMiddle = Builders.MakeLocationConnector(
            workLocationId,
            middleLocationId,
            worldId
        );
        var middleToHome = Builders.MakeLocationConnector(
            middleLocationId,
            homeLocationId,
            worldId
        );

        var result = CreatureRouteScheduleGenerator.Generate(
            worldId,
            [creature],
            [sleep, work, idle],
            [homeToMiddle, middleToWork, workToMiddle, middleToHome],
            [
                Builders.MakeTravelConnector(homeToMiddle.Id, 5, worldId: worldId),
                Builders.MakeTravelConnector(middleToWork.Id, 5, worldId: worldId),
                Builders.MakeTravelConnector(workToMiddle.Id, 5, worldId: worldId),
                Builders.MakeTravelConnector(middleToHome.Id, 5, worldId: worldId),
            ]
        );

        var outboundSchedules = result
            .Schedules.Where(schedule => schedule.DestinationCreatureJobId == work.Id)
            .ToArray();
        Assert.Equal(7, outboundSchedules.Length);
        Assert.All(outboundSchedules, schedule => Assert.Equal(7, schedule.DepartureHour));
        Assert.All(outboundSchedules, schedule => Assert.Equal(2, schedule.DurationHours));
        var outboundRouteId = Assert.Single(
            outboundSchedules.Select(schedule => schedule.RouteId).Distinct()
        );
        Assert.Equal(
            [homeLocationId, middleLocationId, workLocationId],
            result
                .Steps.Where(step => step.RouteId == outboundRouteId)
                .OrderBy(step => step.SequenceIndex)
                .Select(step => step.LocationId)
        );
        var homeboundSchedules = result
            .Schedules.Where(schedule => schedule.DestinationCreatureJobId == idle.Id)
            .ToArray();
        Assert.Equal(7, homeboundSchedules.Length);
        Assert.All(
            homeboundSchedules,
            schedule => Assert.InRange(schedule.DepartureHour, 17, 17.5 - double.Epsilon)
        );
        Assert.True(
            homeboundSchedules.Select(schedule => schedule.DepartureHour).Distinct().Count() > 1
        );
    }

    [Fact]
    public void Generate_JittersLunchDeparturesAndKeepsReturnsOnTime()
    {
        var worldId = Guid.NewGuid();
        var homeLocationId = Guid.NewGuid();
        var workLocationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, locationId: homeLocationId);
        creature.MovementSpeed = 5;
        var sleep = CreatureJobGenerator.GenerateSleep(
            creature.Id,
            homeLocationId,
            worldId,
            new HourWindow(0, 9)
        );
        var work = CreatureJobGenerator.GenerateWork(
            creature.Id,
            workLocationId,
            worldId,
            new HourWindow(9, 17)
        );
        var meal = CreatureJobGenerator.GenerateMeal(
            creature.Id,
            homeLocationId,
            worldId,
            new HourWindow(12, 13)
        );
        var idle = CreatureJobGenerator.GenerateIdle(
            creature.Id,
            homeLocationId,
            worldId,
            new HourWindow(17, 0)
        );
        var homeToWork = Builders.MakeLocationConnector(homeLocationId, workLocationId, worldId);
        var workToHome = Builders.MakeLocationConnector(workLocationId, homeLocationId, worldId);

        var result = CreatureRouteScheduleGenerator.Generate(
            worldId,
            [creature],
            [sleep, work, meal, idle],
            [homeToWork, workToHome],
            [
                Builders.MakeTravelConnector(homeToWork.Id, 5, worldId: worldId),
                Builders.MakeTravelConnector(workToHome.Id, 5, worldId: worldId),
            ]
        );

        var lunchDepartures = result
            .Schedules.Where(schedule => schedule.DestinationCreatureJobId == meal.Id)
            .ToArray();
        var lunchReturns = result
            .Schedules.Where(schedule => schedule.OriginCreatureJobId == meal.Id)
            .ToArray();
        Assert.Equal(7, lunchDepartures.Length);
        Assert.All(
            lunchDepartures,
            schedule => Assert.InRange(schedule.DepartureHour, 10 + 2.0 / 3, 11 + 1.0 / 3)
        );
        Assert.True(
            lunchDepartures.Select(schedule => schedule.DepartureHour).Distinct().Count() > 1
        );
        Assert.Equal(7, lunchReturns.Length);
        Assert.All(lunchReturns, schedule => Assert.Equal(12, schedule.DepartureHour));
    }
}
