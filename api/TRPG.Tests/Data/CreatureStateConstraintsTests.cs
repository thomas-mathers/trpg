using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Data;

public sealed class CreatureStateConstraintsTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    public static TheoryData<string, Creature> InvalidCreatures =>
        new()
        {
            {
                "activity requires awake (sleeping)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Sleeping,
                    activity: CreatureActivity.Working
                )
            },
            {
                "activity requires awake (dead)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Dead,
                    activity: CreatureActivity.Eating
                )
            },
            {
                "walking requires awake (sleeping)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Sleeping,
                    movement: CreatureMovement.Walking
                )
            },
            {
                "walking requires awake (dead)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Dead,
                    movement: CreatureMovement.Walking
                )
            },
            {
                "walking requires standing (sitting)",
                Builders.MakeCreature(
                    posture: CreaturePosture.Sitting,
                    movement: CreatureMovement.Walking
                )
            },
            {
                "walking requires standing (lying)",
                Builders.MakeCreature(
                    posture: CreaturePosture.Lying,
                    movement: CreatureMovement.Walking
                )
            },
            {
                "sleeping requires lying (standing)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Sleeping,
                    posture: CreaturePosture.Standing
                )
            },
            {
                "sleeping requires lying (sitting)",
                Builders.MakeCreature(
                    condition: CreatureCondition.Sleeping,
                    posture: CreaturePosture.Sitting
                )
            },
            {
                "lying requires not awake",
                Builders.MakeCreature(
                    condition: CreatureCondition.Awake,
                    posture: CreaturePosture.Lying
                )
            },
            {
                "alerted requires awake (sleeping)",
                Builders.MakeCreature(condition: CreatureCondition.Sleeping, isAlerted: true)
            },
            {
                "alerted requires awake (dead)",
                Builders.MakeCreature(condition: CreatureCondition.Dead, isAlerted: true)
            },
            {
                "sneaking requires awake (sleeping)",
                Builders.MakeCreature(condition: CreatureCondition.Sleeping, isSneaking: true)
            },
            {
                "sneaking requires awake (dead)",
                Builders.MakeCreature(condition: CreatureCondition.Dead, isSneaking: true)
            },
        };

    public static TheoryData<string, Creature> ValidCreatures =>
        new()
        {
            { "awake standing", Builders.MakeCreature() },
            {
                "awake sitting with an activity",
                Builders.MakeCreature(
                    posture: CreaturePosture.Sitting,
                    activity: CreatureActivity.Eating
                )
            },
            { "awake walking", Builders.MakeCreature(movement: CreatureMovement.Walking) },
            { "sleeping lying", Builders.MakeCreature(condition: CreatureCondition.Sleeping) },
            { "dead lying", Builders.MakeCreature(condition: CreatureCondition.Dead) },
            {
                "dead standing",
                Builders.MakeCreature(
                    condition: CreatureCondition.Dead,
                    posture: CreaturePosture.Standing
                )
            },
            {
                "awake alerted and sneaking",
                Builders.MakeCreature(isAlerted: true, isSneaking: true)
            },
        };

    [Theory]
    [MemberData(nameof(InvalidCreatures))]
    public async Task SaveChanges_Throws_WhenTheStateCombinationIsInvalid(
        string scenario,
        Creature creature
    )
    {
        // Arrange
        _context.Creatures.Add(creature);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
        Assert.NotEmpty(scenario);
    }

    [Theory]
    [MemberData(nameof(ValidCreatures))]
    public async Task SaveChanges_Succeeds_WhenTheStateCombinationIsValid(
        string scenario,
        Creature creature
    )
    {
        // Arrange
        _context.Creatures.Add(creature);

        // Act
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await db.ReadCreature(creature.Id);
        Assert.Equal(creature.Condition, saved.Condition);
        Assert.NotEmpty(scenario);
    }
}
