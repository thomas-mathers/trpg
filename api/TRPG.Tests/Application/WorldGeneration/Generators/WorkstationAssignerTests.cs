using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class WorkstationAssignerTests
{
    [Fact]
    public void Assign_GivesEachWorkerADedicatedWorkstationAtTheirWorkLocation()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var firstCreatureId = Guid.NewGuid();
        var secondCreatureId = Guid.NewGuid();
        var jobs = new[] { Work(firstCreatureId, locationId), Work(secondCreatureId, locationId) };
        var workstations = new[]
        {
            new Workstation { LocationId = locationId },
            new Workstation { LocationId = locationId },
        };

        // Act
        WorkstationAssigner.Assign(jobs, workstations);

        // Assert
        Assert.Equal(
            [firstCreatureId, secondCreatureId],
            workstations.Select(workstation => workstation.AssignedCreatureId)
        );
    }

    private static CreatureJob Work(Guid creatureId, Guid locationId) =>
        new()
        {
            CreatureId = creatureId,
            LocationId = locationId,
            Action = CreatureJobAction.Work,
        };
}
