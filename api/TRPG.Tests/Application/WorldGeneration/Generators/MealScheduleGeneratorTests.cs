using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public sealed class MealScheduleGeneratorTests
{
    [Fact]
    public void Generate_AddsMealWithEnoughTimeToTravelHomeAndReturn()
    {
        var worldId = Guid.NewGuid();
        var workLocationId = Guid.NewGuid();
        var homeLocationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(
            worldId,
            profession: Profession.Baker,
            locationId: workLocationId
        );
        creature.MovementSpeed = 50;
        var jobs = new CreatureJob[]
        {
            CreatureJobGenerator.GenerateSleep(creature.Id, homeLocationId, worldId),
            CreatureJobGenerator.GenerateWork(
                creature.Id,
                workLocationId,
                worldId,
                new HourWindow(8, 20)
            ),
        };
        var outbound = Builders.MakeLocationConnector(workLocationId, homeLocationId, worldId);
        var inbound = Builders.MakeLocationConnector(homeLocationId, workLocationId, worldId);

        var meals = MealScheduleGenerator.Generate(
            worldId,
            [creature],
            jobs,
            [outbound, inbound],
            [
                Builders.MakeTravelConnector(outbound.Id, distance: 50, worldId: worldId),
                Builders.MakeTravelConnector(inbound.Id, distance: 50, worldId: worldId),
            ]
        );

        var meal = Assert.Single(meals);
        Assert.Equal(CreatureJobAction.Eat, meal.Action);
        Assert.Equal(homeLocationId, meal.LocationId);
        Assert.Equal(13, meal.StartHour);
        Assert.Equal(14, meal.EndHour);
        Assert.True(meal.Priority > jobs[1].Priority);
    }

    [Fact]
    public void Generate_DoesNotAddMealForGuard()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, profession: Profession.Guard);
        var jobs = new CreatureJob[]
        {
            CreatureJobGenerator.GenerateSleep(creature.Id, locationId, worldId),
            CreatureJobGenerator.GenerateWork(creature.Id, locationId, worldId),
        };

        var meals = MealScheduleGenerator.Generate(worldId, [creature], jobs, [], []);

        Assert.Empty(meals);
    }

    [Fact]
    public void Generate_DoesNotAddMealForOvernightBartender()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, profession: Profession.Bartender);
        var jobs = new CreatureJob[]
        {
            CreatureJobGenerator.GenerateSleep(creature.Id, locationId, worldId),
            CreatureJobGenerator.GenerateWork(
                creature.Id,
                locationId,
                worldId,
                new HourWindow(16, 4)
            ),
        };

        var meals = MealScheduleGenerator.Generate(worldId, [creature], jobs, [], []);

        Assert.Empty(meals);
    }

    [Fact]
    public void Generate_DoesNotAddMealForInnkeeper()
    {
        var worldId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, profession: Profession.Innkeeper);
        var jobs = new CreatureJob[]
        {
            CreatureJobGenerator.GenerateSleep(creature.Id, locationId, worldId),
            CreatureJobGenerator.GenerateWork(
                creature.Id,
                locationId,
                worldId,
                new HourWindow(6, 18)
            ),
        };

        var meals = MealScheduleGenerator.Generate(worldId, [creature], jobs, [], []);

        Assert.Empty(meals);
    }
}
